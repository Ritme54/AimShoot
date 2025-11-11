using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GunController : MonoBehaviour
{
    public Transform muzzle;            // 발사 시작 위치(있으면 사용)
    public AudioSource audioSrc;
    public PoolManager pool;            // optional

    [Header("Aiming")]
    public Transform pivot;            // 이제 AimPivot(프리팹에서 생성한 회전 기준)
    public float aimSpeed = 720f;      // degrees/sec
    public bool smooth = true;
    public float maxYaw = 45f;         // 좌우 최대 허용 각도 (deg)
    public float maxPitch = 45f;       // 위/아래 제한 (deg)
    public float minPitch = -30f;

    [Header("Position Lock (camera child)")]
    public bool lockToCorner = true;   // 우하단 고정 사용 여부
    public Vector3 lockedLocalPos = new Vector3(0.5f, -0.5f, 0.7f); // 예시: 카메라 로컬 좌표 (조정 필요)
    public bool useCanvasWeapon = false; // UI(Overlay) 기반 무기라면 true 사용

    // 유틸: 각도 정규화
    float NormalizeAngle(float a) { if (a > 180f) a -= 360f; return a; }

    void Awake()
    {
        if (audioSrc == null) audioSrc = GetComponent<AudioSource>();
        if (pivot == null) pivot = transform; // 기본: 자신을 pivot으로 사용
    }

    public void Fire(Vector3 origin, Vector3 aimPoint, WeaponData data)
    {
        if (data == null) return;
        if (audioSrc == null) audioSrc = GetComponent<AudioSource>();

        float originOffset = 0f; // 필요 시 조정
        Vector3 dir = (aimPoint - origin).normalized;
        if (dir.sqrMagnitude < 1e-6f) return;
        Vector3 rayOrigin = origin + dir * originOffset;

        // 1) muzzle FX: 총구에서만 재생 (muzzleFx 사용)
        if (muzzle != null && data.muzzleFx != null)
        {
            if (pool != null && data.useMuzzlePooling)
            {
                var inst = pool.Get(data.muzzleFx);
                inst.transform.SetPositionAndRotation(muzzle.position + muzzle.TransformDirection(data.muzzleFxLocalPos), muzzle.rotation);
                inst.transform.localScale = Vector3.one * data.muzzleFxScale;
                inst.transform.SetParent(muzzle, true);
                inst.SetActive(true);
                var ps = inst.GetComponent<ParticleSystem>();
                if (ps != null) { ps.Clear(); ps.Play(); }
                StartCoroutine(DisableAndReleaseAfter(inst, data.muzzleFxLifetime));
            }
            else
            {
                var inst = Instantiate(data.muzzleFx, muzzle.position + muzzle.TransformDirection(data.muzzleFxLocalPos), muzzle.rotation);
                inst.transform.localScale = Vector3.one * data.muzzleFxScale;
                var ps = inst.GetComponent<ParticleSystem>();
                if (ps != null) { ps.Clear(); ps.Play(); }
                Destroy(inst, Mathf.Max(0.05f, data.muzzleFxLifetime));
            }
        }

        // 2) hitscan 명중 판정 (타겟 포함)
        RaycastHit hit;
        bool isHit = Physics.Raycast(rayOrigin, dir, out hit, data.range, data.hitMask);

        if (isHit)
        {
            // 헤드샷 판정: WeaponData 설정에 따라 태그 또는 레이어 방식 사용
            bool isHead = false;
            if (data.headIsTag)
            {
                if (hit.collider != null) isHead = hit.collider.CompareTag(data.headTag);
            }
            else
            {
                int headLayer = LayerMask.NameToLayer("Head");
                if (hit.collider != null) isHead = (hit.collider.gameObject.layer == headLayer);
            }

            // 데미지 계산
            int dmg = isHead ? Mathf.CeilToInt(data.baseDamage * data.headMult) : data.baseDamage;

            // impact FX 재생 (명중 시만)
            if (data.impactFx != null)
            {
                if (pool != null && data.useImpactPooling)
                {
                    var fx = pool.Get(data.impactFx);
                    fx.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
                    fx.transform.localScale = Vector3.one * data.impactFxScale;
                    fx.transform.SetParent(null, true);
                    fx.SetActive(true);
                    StartCoroutine(DisableAndReleaseAfter(fx, data.impactFxLifetime));
                }
                else
                {
                    var fx = Instantiate(data.impactFx, hit.point, Quaternion.LookRotation(hit.normal));
                    fx.transform.localScale = Vector3.one * data.impactFxScale;
                    Destroy(fx, Mathf.Max(0.1f, data.impactFxLifetime));
                }
            }

            // Targets 처리: 위치/노멀 포함된 OnHit 호출(패치한 Targets에 맞음)
            var tgt = hit.collider.GetComponentInParent<Targets>();
            if (tgt != null)
            {
                tgt.OnHit(isHead, dmg, hit.point, hit.normal);
            }
        }
        // (선택) 미적중일 때 트레이서 시각화 처리하면 여기에 추가

        // 3) 발사 사운드
        if (audioSrc != null && data.fireSfx != null) audioSrc.PlayOneShot(data.fireSfx);
    }

    // 보조 코루틴(이미 GunController에 있으면 재사용)
    System.Collections.IEnumerator DisableAndReleaseAfter(GameObject inst, float delay)
    {
        if (inst == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        if (pool != null) pool.Release(inst);
        else
        {
            if (inst.activeInHierarchy) inst.SetActive(false);
            else Destroy(inst);
        }
    }

    // aimPoint를 받아 pivot을 조정하는 공용 API
    public void AimTowards(Vector3 aimPoint)
    {
        if (pivot == null) return;

        // calculate direction from pivot to aim point
        Vector3 dir = (aimPoint - pivot.position).normalized;
        if (dir.sqrMagnitude < 1e-6f) return;

        // target rotation in world space
        Quaternion targetWorld = Quaternion.LookRotation(dir, Vector3.up);

        // convert to local rotation relative to pivot's parent (so we can clamp local euler)
        Quaternion parentRot = pivot.parent != null ? pivot.parent.rotation : Quaternion.identity;
        Quaternion localTarget = Quaternion.Inverse(parentRot) * targetWorld;
        Vector3 euler = localTarget.eulerAngles;
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);
        euler.z = NormalizeAngle(euler.z);

        // clamp yaw and pitch
        euler.x = Mathf.Clamp(euler.x, minPitch, maxPitch); // pitch
        euler.y = Mathf.Clamp(euler.y, -maxYaw, maxYaw);    // yaw
        euler.z = 0f;

        Quaternion clampedLocal = Quaternion.Euler(euler);
        Quaternion finalWorld = parentRot * clampedLocal;

        if (smooth)
        {
            float maxDeg = aimSpeed * Time.deltaTime;
            pivot.rotation = Quaternion.RotateTowards(pivot.rotation, finalWorld, maxDeg);
        }
        else
        {
            pivot.rotation = finalWorld;
        }

        // optional: lock weapon transform to camera corner (if enabled)
        if (lockToCorner && pivot.root != null)
        {
            // if weapon object is child of camera, set local position. else set pivot.root local pos if appropriate
            Transform root = pivot.root;
            if (root != null && root.GetComponent<Camera>() == null)
            {
             
             
                root.localPosition = lockedLocalPos;
            }
        }
    }
    // GunController 내부에 추가
    // GunController 내부에 추가/대체
    public void PlayMuzzleFx(GameObject prefab, Vector3 localOffset, float scale = 1f, bool usePooling = true, float lifetime = 0.12f)
    {
        if (prefab == null || muzzle == null) return;
        Vector3 pos = muzzle.position + muzzle.TransformDirection(localOffset);
        Quaternion rot = muzzle.rotation;
        SpawnEffect(prefab, pos, rot, scale, usePooling, lifetime, parentTo: muzzle);
    }

    public void PlayImpactFx(GameObject prefab, Vector3 worldPos, Vector3 normal, float scale = 1f, bool usePooling = true, float lifetime = 1f)
    {
        if (prefab == null) return;
        Quaternion rot = Quaternion.LookRotation(normal != Vector3.zero ? normal : (Camera.main != null ? Camera.main.transform.forward : Vector3.up));
        SpawnEffect(prefab, worldPos, rot, scale, usePooling, lifetime, parentTo: null);
    }

    // 공통 생성/재생/정리 유틸
    void SpawnEffect(GameObject prefab, Vector3 pos, Quaternion rot, float scale, bool usePooling, float lifetime, Transform parentTo = null)
    {
        GameObject inst = null;
        if (pool != null && usePooling)
        {
            inst = pool.Get(prefab);
            if (inst == null) return;
            inst.transform.SetPositionAndRotation(pos, rot);
            inst.transform.localScale = Vector3.one * scale;
            inst.transform.SetParent(parentTo, true);
            inst.SetActive(true);
        }
        else
        {
            inst = Instantiate(prefab, pos, rot);
            inst.transform.localScale = Vector3.one * scale;
            if (parentTo != null) inst.transform.SetParent(parentTo, true);
        }

        // 파티클 재생을 안전하게 시도
        var ps = inst.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }

        // 수명 처리: 풀 있으면 Release, 없으면 Destroy 또는 SetActive(false)
        StartCoroutine(DisableOrDestroyAfter(inst, lifetime, usePooling));
    }

    System.Collections.IEnumerator DisableOrDestroyAfter(GameObject inst, float delay, bool usedPooling)
    {
        if (inst == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        if (usedPooling && pool != null)
        {
            pool.Release(inst);
        }
        else
        {
            // 부모가 muzzle에 붙어 있으면 부모에서 분리 후 비활성화/삭제
            inst.transform.SetParent(pool != null ? pool.poolRoot : null, true);
            if (pool != null)
                inst.SetActive(false);
            else
                Destroy(inst);
        }
    }

}