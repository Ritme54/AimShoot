using System.Collections;
using UnityEngine;

// 단순 좌우 패트롤 컴포넌트 (로컬 X축 기준)
[DisallowMultipleComponent]
public class PatrolMovement : MonoBehaviour
{
    [Header("Patrol Settings")]
    public float speed = 1.2f;
    public float patrolRange = 2f;            // startPos ± patrolRange
    public Transform pointA;                  // optional
    public Transform pointB;                  // optional
    public float obstacleDetectDistance = 0.5f;
    public LayerMask obstacleMask;
    public float waitOnTurn = 0.15f;
    public bool useLocalRight = true;

    [Header("Optional Override (set if this movement type has unique stats)")]
    public int overrideHP = 0;
    public int overrideScore = 0;

    // 내부
    bool movingEnabled = false;
    bool applied = false;
    Vector3 startPos;
    Vector3 targetPos;
    int dir = 1;
    bool isWaiting = false;
    Targets targets;

    void Awake()
    {
        targets = GetComponent<Targets>();
        startPos = transform.position;

        if (pointA == null || pointB == null)
        {
            // 자동 포인트 생성(프리팹 편집시 권장 수동 설정)
            pointA = new GameObject(name + "_A").transform;
            pointB = new GameObject(name + "_B").transform;
            pointA.position = startPos - transform.right * patrolRange;
            pointB.position = startPos + transform.right * patrolRange;
            pointA.parent = transform; pointB.parent = transform;
        }

        targetPos = pointB.position;
    }

    void Update()
    {
        if (!movingEnabled || isWaiting) return;

        Vector3 dirVec = useLocalRight ? transform.right * dir : Vector3.right * dir;
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, dirVec, obstacleDetectDistance, obstacleMask))
        {
            if (waitOnTurn > 0f) StartCoroutine(DoWaitAndTurn());
            else DoTurnImmediate();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            DoTurnImmediate();
        }
    }

    IEnumerator DoWaitAndTurn()
    {
        isWaiting = true;
        yield return new WaitForSeconds(waitOnTurn);
        DoTurnImmediate();
        isWaiting = false;
    }

    void DoTurnImmediate()
    {
        dir = -dir;
        targetPos = (dir == 1) ? pointB.position : pointA.position;
    }

    // Apply: 이동 활성화 + override 설정(옵션)
    public void Apply(bool on)
    {
        if (on && !applied)
        {
            if (targets != null)
            {
                targets.SetHPOverride(overrideHP > 0 ? (int?)overrideHP : targets.GetEffectiveMaxHP());
                //'Targets'에는 'GetEffectiveMaxHP'에 대한 정의가 포함되어 있지 않고, 'Targets' 형식의 첫 번째 인수를 허용하는 액세스 가능한 확장 메서드 'GetEffectiveMaxHP'이(가) 없습니다. using 지시문 또는 어셈블리 참조가 있는지 확인하세요.

                targets.SetScoreOverride(overrideScore > 0 ? (int?)overrideScore : null);
            }
            EnableMovement(true);
            applied = true;
        }
        else if (!on && applied)
        {
            EnableMovement(false);
            // ResetTarget에서 override 해제되므로 여기서는 간단히 상태만
            applied = false;
        }
    }

    public void EnableMovement(bool on)
    {
        movingEnabled = on;
        if (on)
        {
            // 재초기화
            targetPos = (dir == 1) ? pointB.position : pointA.position;
        }
    }

    public void ResetMovement()
    {
        EnableMovement(false);
    }
}