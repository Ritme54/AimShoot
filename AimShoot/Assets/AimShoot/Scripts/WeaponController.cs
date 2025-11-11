using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class WeaponController : MonoBehaviour
{
    public WeaponData data;             // 인스펙터에서 세팅
    public GunController gun;           // 발사 실행부 할당
    public Transform aimSource;         // 발사 origin(예: muzzle), 없으면 Camera.main 사용
    public CrosshairController cross;   // optional: aim 계산용(없으면 카메라+마우스)
    public bool autoReloadWhenEmpty = true;

    [Header("Runtime (read-only)")]
    public int curAmmo;
    public int reserveMags;

    [Header("Events")]
    public UnityEvent<int, int, int> OnAmmoChanged; // cur, magSize, reserve
    public UnityEvent OnReloadStarted;
    public UnityEvent OnReloadFinished;

    AudioSource audioSrc;
    float lastFire = -999f;
    bool reloading = false;
    Coroutine reloadCr = null;

    public bool IsReloading => reloading;

    void Awake()
    {
        if (data == null) { Debug.LogWarning("WeaponData not set on " + name); data = new WeaponData(); }
        if (gun == null) gun = GetComponentInChildren<GunController>();
        if (audioSrc == null) audioSrc = GetComponent<AudioSource>();

        curAmmo = Mathf.Clamp(curAmmo == 0 ? data.magSize : curAmmo, 0, data.magSize);
        reserveMags = Mathf.Max(0, data.reserveMags);
        BroadcastAmmo();
    }

    void Update()
    {
        if (gun != null)
        {
            Vector3 aimPoint = GetAimPoint(); // 기존 방식 (cross.GetWorldAimPoint 등)
            gun.AimTowards(aimPoint);
        }

        // 자동 재장전
        if (!reloading && curAmmo <= 0 && reserveMags > 0 && autoReloadWhenEmpty)
        {
            StartReload();
        }
    }

    // 외부 호출: 발사 시도 (Input은 WeaponManager에서 호출 권장)
    public void TryFire()
    {
        if (reloading) return;
        if (Time.time < lastFire + data.fireRate) return;
        if (curAmmo <= 0)
        {
            // 빈 총 소리/피드백 가능
            return;
        }

        DoFire();
        lastFire = Time.time;
    }

    void DoFire()
    {
        // 기본 상태 점검(필수 체크만)
        if (data == null || gun == null) return;

        curAmmo = Mathf.Max(0, curAmmo - 1);
        BroadcastAmmo();

        // origin 및 aim 계산
        Vector3 origin = (aimSource != null) ? aimSource.position
                      : (Camera.main != null ? Camera.main.transform.position : transform.position);
        Vector3 aimPoint = GetAimPoint();

        // spread 적용 (간단하고 안정적)
        Vector3 finalAim = aimPoint;
        if (data.spreadRadius > 0f)
        {
            Vector3 dirCenter = (aimPoint - origin).normalized;
            if (dirCenter.sqrMagnitude > 1e-6f)
            {
                Vector3 up = (Camera.main != null && Mathf.Abs(Vector3.Dot(dirCenter, Vector3.up)) > 0.95f)
                             ? Camera.main.transform.up : Vector3.up;
                Vector3 right = Vector3.Cross(dirCenter, up).normalized;
                Vector3 forward = Vector3.Cross(right, dirCenter).normalized;

                float r = data.spreadRadius * Mathf.Sqrt(Random.value);
                float theta = Random.value * Mathf.PI * 2f;
                Vector3 offset = right * (Mathf.Cos(theta) * r) + forward * (Mathf.Sin(theta) * r);
                finalAim = aimPoint + offset;
            }
        }

        // muzzle FX와 소리 (GunController가 실제 위치에서 처리)
        gun.PlayMuzzleFx(data.muzzleFx, data.muzzleFxLocalPos, data.muzzleFxScale, data.useMuzzlePooling);
        gun?.PlayMuzzleFx(data.muzzleFx, data.muzzleFxLocalPos, data.muzzleFxScale, data.useMuzzlePooling, 0.12f);

        if (audioSrc != null && data.fireSfx != null) audioSrc.PlayOneShot(data.fireSfx);

        // 실제 발사(검사와 임팩트 처리는 GunController.Fire 내부에서 담당)
        gun.Fire(origin, finalAim, data);

        // 트레이서/탄흔 생성: impactFx가 있으면 그걸 사용, 없으면 간단한 라인으로 시각화
        Vector3 tracerDir = (finalAim - origin).normalized;
        Ray tracerRay = new Ray(origin, tracerDir);
        RaycastHit hit;
        bool isHit = Physics.Raycast(tracerRay, out hit, data.range, data.hitMask);

        Vector3 tracerPoint = isHit ? hit.point : tracerRay.GetPoint(data.range);

        if (data.impactFx != null)
        {
            if (gun.pool != null && data.useImpactPooling)
            {
                var fx = gun.pool.Get(data.impactFx);
                fx.transform.position = tracerPoint;
                fx.transform.rotation = Quaternion.LookRotation(isHit ? hit.normal : tracerDir);
                fx.transform.localScale = Vector3.one * data.impactFxScale;
                fx.SetActive(true);
            }
            else
            {
                var fx = Instantiate(data.impactFx, tracerPoint, Quaternion.LookRotation(isHit ? hit.normal : tracerDir));
                fx.transform.localScale = Vector3.one * data.impactFxScale;
                Destroy(fx, data.impactFxLifetime);
            }
        }
        else
        {
            // Debug tracer: 간단한 LineRenderer 표시 (개발 빌드에서만)
#if UNITY_EDITOR
            StartCoroutine(SpawnTempTracer(origin, tracerPoint, 0.12f, Color.yellow));
#endif

        }

    }

    Vector3 GetAimPoint()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position + transform.forward * data.range;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // aimMask: 기본 hitMask에서 Targets/Head 레이어만 제외 (환경만 보도록)
        int exclude = LayerMask.GetMask("Targets", "Head"); // 제외할 레이어 이름들
        int aimMask = data.hitMask.value & ~exclude;       // 환경 레이어만 남음

        if (Physics.Raycast(ray, out RaycastHit hitAim, data.range, aimMask))
        {
            return hitAim.point;
        }

        // 환경에 맞는 것이 없다면 고정 거리 지점 반환
        return ray.GetPoint(data.range);
    }
    public void StartReload()
    {
        if (reloading) return;
        if (curAmmo >= data.magSize) return;
        if (reserveMags <= 0) return;
        if (!gameObject.activeInHierarchy || !enabled) return; // 안전 체크

        reloadCr = StartCoroutine(ReloadRoutine());
    }

    public void CancelReload()
    {
        if (!reloading) return;
        if (reloadCr != null) StopCoroutine(reloadCr);
        reloadCr = null;
        reloading = false;
        OnReloadFinished?.Invoke(); // 취소 시에도 후처리 필요하면 조정
        BroadcastAmmo();
    }

    IEnumerator ReloadRoutine()
    {
        reloading = true;
        OnReloadStarted?.Invoke();
        if (audioSrc != null && data.reloadSfx != null) audioSrc.PlayOneShot(data.reloadSfx);

        float t = 0f;
        while (t < data.reloadTime)
        {
            t += Time.deltaTime;
            yield return null;
        }

        reserveMags = Mathf.Max(0, reserveMags - 1);
        curAmmo = data.magSize;

        reloading = false;
        reloadCr = null;
        OnReloadFinished?.Invoke();
        BroadcastAmmo();
    }

    public void BroadcastAmmo()
    {
        OnAmmoChanged?.Invoke(curAmmo, data.magSize, reserveMags);
    }

    public void OnUnequip()
    {
        // 무기 교체 시 재장전 취소 정책
        if (reloading) CancelReload();
    }

    // 임시 트레이서(LineRenderer) 생성 및 삭제 코루틴
IEnumerator SpawnTempTracer(Vector3 from, Vector3 to, float life, Color col)
{
    // 간단한 LineRenderer를 동적으로 생성해 표시
    GameObject lrObj = new GameObject("TempTracer");
    var lr = lrObj.AddComponent<LineRenderer>();
    lr.positionCount = 2;
    lr.SetPosition(0, from);
    lr.SetPosition(1, to);
    lr.startWidth = lr.endWidth = 0.02f;
    lr.material = new Material(Shader.Find("Sprites/Default")); // 간단한 셰이더 사용
    lr.startColor = lr.endColor = col;
    // 카메라 정렬 등 세부 조정 필요 시 추가

    yield return new WaitForSeconds(life);
    Destroy(lrObj);
}
}