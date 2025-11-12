using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SpawnManager: PoolManager와 SpawnPoint를 사용하여 스폰을 관리합니다.
public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    public SpawnPoint[] spawnPoints;
    public GameObject[] targetPrefabs;
    public int maxActiveT = 6;
    public float startDelay = 1f;
    public float spawnDelay = 1.5f;

    [Header("Elite / Moving Settings")]
    public int eliteGuaranteedEvery = 10;
    public float eliteChance = 0.1f;
    public int movingGuaranteedEvery = 8;
    public float movingChance = 0.2f;
    public bool allowBothEnM = true;

    [Header("References")]
    public PoolManager poolManager;
    public LayerMask targetLayerMask;

    [Header("Debug")]
    public bool debugMode = false;

    // 내부 상태
    private List<GameObject> activeList = new List<GameObject>();
    private GameManager gm;
    private int spawnCounterGlobal = 0;
    private int spawnCounterElite = 0;
    private int spawnCounterMoving = 0;

    void Awake()
    {
        gm = FindFirstObjectByType<GameManager>();
        if (poolManager == null) poolManager = FindFirstObjectByType<PoolManager>();
    }

    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // GameManager가 있고 게임이 멈춰있으면 스폰 대기
            if (gm != null && !gm.isRunning)
            {
                yield return null;
                continue;
            }

            CleanupActiveList();

            if (activeList.Count < maxActiveT)
            {
                SpawnPoint pt = PickAvailableSpawnPoint();
                if (pt != null)
                {
                    pt.Occupy();         // 즉시 예약/점유 표시
                    SpawnAtPoint(pt);
                }
            }

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    void CleanupActiveList()
    {
        for (int i = activeList.Count - 1; i >= 0; i--)
        {
            GameObject obj = activeList[i];
            if (obj == null || !obj.activeInHierarchy)
            {
                activeList.RemoveAt(i);
            }
        }
    }

    SpawnPoint PickAvailableSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;

        int start = Random.Range(0, spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int idx = (start + i) % spawnPoints.Length;
            SpawnPoint pt = spawnPoints[idx];
            if (pt == null) continue;
            if (!pt.IsAvailable(targetLayerMask)) continue;
            return pt;
        }
        return null;
    }

    void SpawnAtPoint(SpawnPoint pt)
    {
        if (pt == null) return;

        // 1) prefab 선택
        GameObject prefab = PickPrefabRandom();
        if (prefab == null)
        {
            if (debugMode) Debug.LogWarning("[SpawnManager] No prefab available.");
            return;
        }

        // 2) 풀에서 오브젝트 가져오기 (비활성 상태 보장)
        GameObject obj = (poolManager != null) ? poolManager.Get(prefab) : Instantiate(prefab);
        if (obj == null)
        {
            if (debugMode) Debug.LogWarning("[SpawnManager] Failed to get object from pool / instantiate.");
            return;
        }
        obj.SetActive(false);

        // 3) Targets 준비
        Targets t = obj.GetComponent<Targets>() ?? obj.GetComponentInParent<Targets>();
        if (t == null)
        {
            Debug.LogError($"[SpawnManager] Prefab '{prefab.name}' missing Targets component.");
            // 안전하게 반환
            if (poolManager != null) poolManager.Release(obj);
            else Destroy(obj);
            return;
        }

        // 4) originSpawnPoint 저장(반환 시 안전한 Free를 위해)
        t.originSpawnPoint = pt;

        // 5) 초기화
        t.ResetTarget();

        // 6) 정예 / 이동 판정(보장 로직)
        // spawnCounter는 실제 스폰 성공 시 갱신(아래에서 갱신)
        bool forceElite = (eliteGuaranteedEvery > 0) && (spawnCounterElite + 1 >= eliteGuaranteedEvery);
        bool forceMoving = (movingGuaranteedEvery > 0) && (spawnCounterMoving + 1 >= movingGuaranteedEvery);

        bool isElite = forceElite || (Random.value < eliteChance);
        bool isMoving = forceMoving || (Random.value < movingChance);

        if (!allowBothEnM && isElite && isMoving)
        {
            // 정예 우선
            isMoving = false;
        }

        // 7) 컴포넌트 적용 (Reset 후 Finalize 전에 적용)
        var eliteComp = obj.GetComponent<EliteModifier>();
        if (eliteComp != null) eliteComp.Apply(isElite);

        var moveComp = obj.GetComponent<PatrolMovement>();
        if (moveComp != null)
        {
            // movement가 포인트 중심을 필요로 하면 center 값 전달
            moveComp.centerPosition = pt.GetSpawnPosition(); // public Vector3 centerPosition 필요
            moveComp.Apply(isMoving);
        }

        var specialComp = obj.GetComponent<SPMovement>(); // 특수 컴포넌트가 있으면
        if (specialComp != null)
        {
            bool isSpecial = isElite && isMoving;
            specialComp.Apply(isSpecial);
        }

        // 8) Targets 최종화
        t.FinalizeStatsAfterModifiers();

        // listener 등록 부분(Replace 기존 AddListener 부분)
        t.OnKilled.RemoveListener(OnTargetKilled);
        t.OnKilled.AddListener(OnTargetKilled);

        t.OnReturned.RemoveListener(OnTargetReturned);
        t.OnReturned.AddListener(OnTargetReturned);

        // 초기화
        t.ResetTarget();
        var tac = obj.GetComponent<TargetAnimationController>();
        if (tac != null) tac.ResetAnimationState();

        // 활성화/점유
        obj.transform.position = pt.GetSpawnPosition();
        obj.transform.rotation = pt.transform.rotation;
        obj.SetActive(true);
        pt.Occupy();
        activeList.Add(obj);

        // 11) 리스트/카운터 업데이트
        if (!activeList.Contains(obj)) activeList.Add(obj);
        spawnCounterGlobal++;
        if (isElite) spawnCounterElite = 0; else spawnCounterElite++;
        if (isMoving) spawnCounterMoving = 0; else spawnCounterMoving++;

        if (debugMode)
        {
            Debug.Log($"Spawn#{spawnCounterGlobal} prefab={prefab.name} elite={isElite} moving={isMoving} (forceE={forceElite}, forceM={forceMoving})");
        }
    }

    public void SpawnMany(int count)
    {
        for (int i = 0; i < count; i++)
        {
            SpawnOneNow(); // 또는 기존 SpawnAtRandomPoint() 등 프로젝트에 맞는 호출
        }
    }


    GameObject PickPrefabRandom()
    {
        if (targetPrefabs == null || targetPrefabs.Length == 0) return null;
        int idx = Random.Range(0, targetPrefabs.Length);
        return targetPrefabs[idx];
    }

    // 스폰된 타겟의 OnReturned 이벤트(또는 외부에서 호출하도록 연결) 처리
    void OnTargetReturned(GameObject obj)
    {
        if (obj == null) return;
        Targets t = obj.GetComponent<Targets>();
        if (t != null)
        {

            t.OnKilled.RemoveListener(OnTargetKilled);
            t.OnReturned.RemoveListener(OnTargetReturned);
            if (t.originSpawnPoint != null) { t.originSpawnPoint.Free(); t.originSpawnPoint = null; }
            // reset components
            var pm = obj.GetComponent<PatrolMovement>(); pm?.ResetMovement();
            var em = obj.GetComponent<EliteModifier>(); em?.ResetElite();
            t.ResetTarget();
        }

        if (poolManager != null) poolManager.Release(obj);
        else obj.SetActive(false);

        activeList.Remove(obj);
    }

    void OnTargetKilled(int points)
    {
        if (gm != null) gm.AddScore(points);
        // 추가 처리(이펙트, 콤보 등)
    }

    // 외부에서 수동으로 타겟 제거 시 사용가능(기존 NotifyTargetDestroyed 유지 가능)
    public void NotifyTargetDestroyed(GameObject obj)
    {
        OnTargetReturned(obj);
    }

    public void ResetState()
    {
        spawnCounterGlobal = 0;
        spawnCounterElite = 0;
        spawnCounterMoving = 0;

        for (int i = activeList.Count - 1; i >= 0; i--)
        {
            var obj = activeList[i];
            if (obj != null)
            {
                // 풀에 반환하기 전에 ResetTarget 등 초기화 보장
                Targets t = obj.GetComponent<Targets>();
                if (t != null) t.ResetTarget();

                if (poolManager != null) poolManager.Release(obj);
                else obj.SetActive(false);
            }
            activeList.RemoveAt(i);
        }

        if (spawnPoints != null)
        {
            foreach (var pt in spawnPoints) if (pt != null) pt.Free();
        }
    }

    [ContextMenu("SpawnOneNow")]
    public void SpawnOneNow()
    {
        if (spawnPoints != null && spawnPoints.Length > 0) SpawnAtPoint(spawnPoints[0]);
    }
}