using UnityEngine;

// SpawnPoint: 개별 스폰 지점 컴포넌트
// - 점유 상태(occupied)를 관리하고, 주변 충돌로 중복 스폰도 감지합니다.
[RequireComponent(typeof(Transform))]
public class SpawnPoint : MonoBehaviour
{
    public float radius = 0.5f; // 충돌 검사 반경(인스펙터에서 조정)
    private bool occupied = false;

    // 사용 가능 여부를 반환(occupied 플래그와 물리 검사 결합)
    public bool IsAvailable(LayerMask targetMask)
    {
        if (occupied) return false;

        // 주변에 활성 타깃이 있는지 검사(겹침 방지)
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, targetMask);
        foreach (var h in hits)
        {
            if (h != null && h.gameObject.activeInHierarchy) return false;
        }
        return true;
    }

    // 점유 설정
    public void Occupy()
    {
        occupied = true;
    }

    // 점유 해제
    public void Free()
    {
        occupied = false;
    }

    // 점유 상태 조회
    public bool IsOccupied()
    {
        return occupied;
    }

    // (편의) 씬에서 시각화용 OnDrawGizmos
    void OnDrawGizmosSelected()
    {
        Gizmos.color = occupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}