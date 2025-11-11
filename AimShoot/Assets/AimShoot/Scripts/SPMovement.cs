using System.Collections;
using UnityEngine;

/// <summary>
/// PatrolMovement
/// - 중심점(centerTransform 또는 centerPosition)을 기준으로 좌/우(patrolRange) 왕복
/// - 장애물 감지 시 즉시 또는 wait 후 반전
/// - 인스펙터로 overrideHP / overrideScore / overrideMaterial / 
/// 
/// 
/// / Audio 를 지정 가능
/// - Apply(true)로 Targets에 override를 설정, Apply(false) 또는 ResetMovement로 해제
/// - SpawnManager는 ResetTarget() -> comp.Apply(flag) -> Targets.FinalizeStatsAfterModifiers() 순서로 호출해야 안전
/// </summary>
[DisallowMultipleComponent]
public class SPMovement : MonoBehaviour
{
    [Header("Center")]
    public Transform centerTransform = null;         // 우선 사용될 중심 Transform
    public Vector3 centerPosition = Vector3.zero;   // Transform 없을 때 사용

    [Header("Patrol")]
    public float patrolRange = 2f;                  // 중심에서 ± 범위
    public float speed = 1.2f;                      // 이동 속도
    public bool useLocalRight = true;               // 로컬 X축 기준 이동
    public int startDirection = 1;                  // 1 == right, -1 == left

    [Header("Obstacle Detection")]
    public float obstacleDetectDistance = 0.5f;
    public LayerMask obstacleMask = ~0;
    public float waitOnTurn = 0.15f;                // 0 => 즉시 반전

    [Header("Optional Overrides (per-unit unique stats)")]
    [Tooltip("0 이면 사용 안 함(override 하지 않음)")]
    public int overrideHP = 0;                      // 프리팹 단위로 고유 HP 지정 가능
    [Tooltip("0 이면 사용 안 함(override 하지 않음)")]
    public int overrideScore = 0;                   // 프리팹 단위로 고유 점수 지정 가능
    public Material overrideMaterial = null;        // 유닛 구분용 머티리얼
     public AudioClip overrideAudio = null;          // 적용 시 재생(옵션)
    [Range(0f, 1f)] public float overrideAudioVolume = 1f;

    [Header("Runtime Options")]
    public bool autoInitializeCenterOnAwake = true;

    // 내부 상태
    Vector3 runtimeCenter;
    Vector3 leftPoint, rightPoint;
    Vector3 targetPos;
    int dir;
    bool movingEnabled = false;
    bool isWaiting = false;
    bool applied = false;
    Targets targets;
    AudioSource audioSrc;

    void Awake()
    {
        dir = (startDirection >= 0) ? 1 : -1;
        targets = GetComponent<Targets>();
        audioSrc = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;

        if (autoInitializeCenterOnAwake) InitializeCenter();
    }

    void OnValidate()
    {
        if (patrolRange < 0f) patrolRange = 0f;
        if (startDirection == 0) startDirection = 1;
    }

    void InitializeCenter()
    {
        runtimeCenter = (centerTransform != null) ? centerTransform.position : centerPosition;
        Vector3 rightDir = useLocalRight ? transform.right : Vector3.right;
        leftPoint = runtimeCenter - rightDir * patrolRange;
        rightPoint = runtimeCenter + rightDir * patrolRange;
        targetPos = (dir == 1) ? rightPoint : leftPoint;
    }

    void Update()
    {
        if (!movingEnabled || isWaiting) return;

        Vector3 rayDir = (dir == 1) ? (useLocalRight ? transform.right : Vector3.right) : (useLocalRight ? -transform.right : Vector3.left);
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, rayDir, obstacleDetectDistance, obstacleMask))
        {
            if (waitOnTurn > 0f) StartCoroutine(DoWaitAndTurnCoroutine());
            else DoTurnImmediate();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.05f)
        {
            DoTurnImmediate();
        }
    }

    IEnumerator DoWaitAndTurnCoroutine()
    {
        isWaiting = true;
        yield return new WaitForSeconds(waitOnTurn);
        DoTurnImmediate();
        isWaiting = false;
    }

    void DoTurnImmediate()
    {
        dir = -dir;
        targetPos = (dir == 1) ? rightPoint : leftPoint;
    }

    // Apply: SpawnManager에서 스폰 타입에 따라 true/false로 호출
    // on==true : override가 있으면 Targets에 설정하고 VFX/Audio 활성, 이동 시작
    // on==false: 해제(ResetTarget에서 ClearOverrides 권장)
    public void Apply(bool on)
    {
        if (on && !applied)
        {
            if (targets != null)
            {
                // hp override: 0은 "사용 안함" 규약
                targets.SetHPOverride(overrideHP > 0 ? (int?)overrideHP : null);
                targets.SetScoreOverride(overrideScore > 0 ? (int?)overrideScore : null);

                // 머티리얼 적용(인스턴스화)
                if (overrideMaterial != null) targets.ApplyMaterialToRenderers(overrideMaterial);
            }

            if (overrideAudio != null && audioSrc != null) audioSrc.PlayOneShot(overrideAudio, overrideAudioVolume);

            InitializeCenter(); // 중심 재계산(Spawn 시 transform 위치 변경 가능)
            targetPos = (dir == 1) ? rightPoint : leftPoint;
            EnableMovement(true);
            applied = true;
        }
        else if (!on && applied)
        {
            // 해제: 이동 중지, VFX 끄기, Targets는 ResetTarget에서 초기화
            EnableMovement(false);
            // (선택) targets.ClearOverrides(); // 보통 ResetTarget에서 처리
            applied = false;
        }
    }

    public void EnableMovement(bool on)
    {
        movingEnabled = on;
        if (on)
        {
            InitializeCenter();
            targetPos = (dir == 1) ? rightPoint : leftPoint;
        }
    }

    public void ResetSPMovement()
    {
        EnableMovement(false);
        StopAllCoroutines();
        isWaiting = false;
        applied = false;
        // 위치 복원은 SpawnManager 책임(원래 spawn 위치로 옮김)
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 dbgCenter = (centerTransform != null) ? centerTransform.position : centerPosition;
        Vector3 rightDir = useLocalRight ? transform.right : Vector3.right;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(dbgCenter, 0.05f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(dbgCenter - rightDir * patrolRange, dbgCenter + rightDir * patrolRange);
        Gizmos.DrawSphere(dbgCenter - rightDir * patrolRange, 0.03f);
        Gizmos.DrawSphere(dbgCenter + rightDir * patrolRange, 0.03f);
    }
#endif
}