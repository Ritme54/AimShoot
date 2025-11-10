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
            Vector3 aimPoint = GetAimPoint(); // 기존 GetAimPoint 로직 사용
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
        curAmmo = Mathf.Max(0, curAmmo - 1);
        BroadcastAmmo();

        Vector3 origin = (aimSource != null) ? aimSource.position : (Camera.main != null ? Camera.main.transform.position : transform.position);
        Vector3 aimPoint = GetAimPoint();

        gun?.Fire(origin, aimPoint, data);

        if (audioSrc != null && data.fireSfx != null) audioSrc.PlayOneShot(data.fireSfx);
    }

    Vector3 GetAimPoint()
    {
        if (cross != null) return cross.GetWorldAimPoint(data.range, data.hitMask);
        Camera c = Camera.main;
        if (c == null) return transform.position + transform.forward * data.range;
        Ray ray = c.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit h, data.range, data.hitMask)) return h.point;
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
}