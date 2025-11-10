using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GunController : MonoBehaviour
{
    public Transform muzzle;            // 발사 시작 위치(있으면 사용)
    public AudioSource audioSrc;
    public PoolManager pool;            // optional

    [Header("Aiming")]
    public Transform pivot;             // 실제 회전의 기준이 되는 Transform(예: 총 루트 또는 회전 관절)
    public float aimSpeed = 720f;       // degrees per second (회전 속도)
    public bool onlyYaw = false;        // Y 축(수평)만 회전시키는 옵션
    public bool smooth = true;          // 부드럽게 회전할지
    public float maxPitch = 60f;        // 위로 올릴 수 있는 최대 각도 (deg)
    public float minPitch = -30f;       // 아래로 내릴 수 있는 최소 각도 (deg)
    public float maxYaw = 180f;         // 좌우 허용 범위(절반) 또는 180 무제한 등(선택적 제한)

    void Awake()
    {
        if (audioSrc == null) audioSrc = GetComponent<AudioSource>();
        if (pivot == null) pivot = transform; // 기본: 자신을 pivot으로 사용
    }

    // 기존 Fire 구현은 그대로 유지(생략)...
    public void Fire(Vector3 origin, Vector3 aimPoint, WeaponData data)
    {
        // 기존 히트스캔/이펙트/데미지 로직 유지
        // ...
    }

    // aimPoint를 받아 pivot을 조정하는 공용 API
    public void AimTowards(Vector3 aimPoint)
    {
        if (pivot == null) return;

        // 1) 목표 방향
        Vector3 dir = (aimPoint - pivot.position).normalized;
        if (dir.sqrMagnitude < 1e-6f) return;

        // 2) 목표 회전 계산(월드)
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

        // 3) 로컬 축 제한을 위해 targetRot을 pivot의 로컬 회전으로 변환
        Quaternion localTarget = Quaternion.Inverse(pivot.parent != null ? pivot.parent.rotation : Quaternion.identity) * targetRot;

        // Extract Euler angles (local)
        Vector3 euler = localTarget.eulerAngles;
        // Convert to -180..180 range
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);
        euler.z = NormalizeAngle(euler.z);

        // If onlyYaw: zero out pitch (x) rotation
        if (onlyYaw)
        {
            // Keep yaw (y), zero pitch and roll
            euler.x = 0f;
            euler.z = 0f;
            // Optionally clamp yaw to [-maxYaw, maxYaw]
            if (maxYaw < 180f)
                euler.y = Mathf.Clamp(euler.y, -maxYaw, maxYaw);
        }
        else
        {
            // Clamp pitch to [minPitch, maxPitch]
            euler.x = Mathf.Clamp(euler.x, minPitch, maxPitch);
            // Optionally clamp yaw as well
            if (maxYaw < 180f)
                euler.y = Mathf.Clamp(euler.y, -maxYaw, maxYaw);
            // Keep roll zero
            euler.z = 0f;
        }

        // Rebuild local rotation
        Quaternion clampedLocal = Quaternion.Euler(euler);

        // Convert back to world target rotation
        Quaternion finalTargetWorld = (pivot.parent != null ? pivot.parent.rotation : Quaternion.identity) * clampedLocal;

        // 4) Apply rotation (smooth or immediate)
        if (smooth)
        {
            float maxDeg = aimSpeed * Time.deltaTime;
            pivot.rotation = Quaternion.RotateTowards(pivot.rotation, finalTargetWorld, maxDeg);
        }
        else
        {
            pivot.rotation = finalTargetWorld;
        }
    }

    // Helper: normalize angle 0..360 -> -180..180
    float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }
}