using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Header("SpawnPoint Settings")]
    public float spawnHeightOffset = 0.0f;      // 필요시 Y 오프셋
    public float checkRadius = 0.5f;            // IsAvailable에서 사용할 충돌 반경
    public LayerMask blockMask;                 // 점유 검사 레이어
    [HideInInspector] public bool isOccupied = false;

    // 점유 상태 설정
    public void Occupy()
    {
        isOccupied = true;
    }

    public void Free()
    {
        isOccupied = false;
    }

    // 외부에서 사용: 스폰 가능 여부 판단
    public bool IsAvailable(LayerMask occupancyMask)
    {
        if (isOccupied) return false;
        // 지정 레이어를 사용하여 특정 오브젝트가 있으면 사용 불가
        Collider[] hits = Physics.OverlapSphere(GetSpawnPosition(), checkRadius, occupancyMask);
        return hits.Length == 0;
    }

    // 핵심: Spawn 위치 반환 (여기서 높이 보정, 랜덤 오프셋 등을 적용)
    public Vector3 GetSpawnPosition()
    {
        Vector3 pos = transform.position;
        pos.y += spawnHeightOffset;
        return pos;
    }

    // 에디터용: 기즈모 표시
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawSphere(GetSpawnPosition(), 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetSpawnPosition(), checkRadius);
    }
#endif
}