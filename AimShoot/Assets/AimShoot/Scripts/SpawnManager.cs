using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SpawnManager: PoolManager와 SpawnPoint를 사용하여 스폰을 관리합니다.
// 인스펙터에서 poolManager, spawnPoints(SpawnPoint 컴포넌트), targetPrefabs 등을 연결하세요.
public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    public SpawnPoint[] spawnPoints;          // 씬의 SpawnPoint 컴포넌트들(인스펙터 할당)
    public GameObject[] targetPrefabs;        // 스폰할 프리팹들 (외형만 다름)
    public int maxActiveT = 6;              // 동시 활성 표적 수 (원래 maxActiveTargets)
    public float startDelay = 1f;           // 기존 initialSpawnDelay
    public float spawnDelay = 1.5f;         // 기존 spawnInterval

    [Header("Elite / Moving Settings")]
    public int eliteGuaranteedEvery = 10;   // N번에 1번 정예 보장 (0=비활성)
    public float eliteChance = 0.1f;        // 정예 확률(기본)
    public int movingGuaranteedEvery = 8;   // N번에 1번 이동형 보장 (0=비활성)
    public float movingChance = 0.2f;       // 이동 확률(기본)
    public bool allowBothEnM = true;        // 한 스폰에서 정예+이동 허용 여부


    [Header("References")]
    public PoolManager poolManager;           // 인스펙터에서 할당
    public LayerMask targetLayerMask;         // SpawnPoint의 충돌 검사에 사용할 레이어 마스크

    [Header("Debug")]
    public bool debugMode = false;


    // 내부 상태
    private List<GameObject> activeList = new List<GameObject>(); // 이전 active
    private GameManager gm;
    private int spawnCounterGlobal = 0;
    private int spawnCounterElite = 0;
    private int spawnCounterMoving = 0;



    void Awake()
    {
        gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        // poolManager는 인스펙터에서 연결하거나 Find 후 사용 가능
        if (poolManager == null)
        {
            poolManager = FindFirstObjectByType<PoolManager>();
        }
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
            // GameManager가 있고 isRunning이 false이면 스폰 멈춤
            if (gm != null && !gm.isRunning)
            {
                yield return null;
                continue;
            }

            // 활성 리스트 정리
            CleanupActiveList();

            if (activeList.Count < maxActiveT)
            {
                SpawnPoint pt = PickAvailableSpawnPoint();
                if (pt != null)
                {
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

        // 카운터 증가 (글로벌/정예/이동 각각)
        spawnCounterGlobal++;
        spawnCounterElite++;
        spawnCounterMoving++;

        // prefab 선택(동일 비율)
        GameObject prefab = PickPrefabRandom();
        if (prefab == null) return;

        // 보장(강제) 판정
        bool forceElite = (eliteGuaranteedEvery > 0) && (spawnCounterElite % eliteGuaranteedEvery == 0);
        bool forceMoving = (movingGuaranteedEvery > 0) && (spawnCounterMoving % movingGuaranteedEvery == 0);

        // 혼합형 판정: 보장 OR 확률
        bool isElite = forceElite || (Random.value < eliteChance);
        bool isMoving = forceMoving || (Random.value < movingChance);

        // 동시 발생 정책: allowBothEnM 사용
        if (!allowBothEnM && isElite && isMoving)
        {
            // 정예 우선(원하면 이동 우선으로 바꿀 수 있음)
            isMoving = false;
        }

        // 풀에서 꺼내기
        GameObject obj = (poolManager != null) ? poolManager.Get(prefab) : Instantiate(prefab);
        if (obj == null) return;

        // 위치/회전 설정
        obj.transform.position = pt.transform.position;
        obj.transform.rotation = pt.transform.rotation;

        // Targets 초기화
        Targets t = obj.GetComponent<Targets>();
        if (t == null) t = obj.GetComponentInParent<Targets>();
        if (t != null)
        {
            t.ResetTarget();
           // t.SetElite(isElite);
           // t.SetMoving(isMoving);
        }

        obj.SetActive(true);

        if (!activeList.Contains(obj)) activeList.Add(obj);
        pt.Occupy();

        if (debugMode)
        {
            Debug.Log($"Spawn#{spawnCounterGlobal} prefab={prefab.name} elite={isElite} moving={isMoving} (forceE={forceElite}, forceM={forceMoving})");
        }
    }

    GameObject PickPrefabRandom()
    {
        if (targetPrefabs == null || targetPrefabs.Length == 0) return null;
        int idx = Random.Range(0, targetPrefabs.Length);
        return targetPrefabs[idx];
    }

    public void NotifyTargetDestroyed(GameObject obj)
    {
        if (obj == null) return;

        if (activeList.Contains(obj)) activeList.Remove(obj);

        // 풀로 반환
        if (poolManager != null)
        {
            poolManager.Release(obj);
        }
        else
        {
            // 풀 미사용 시 안전하게 비활성화
            if (obj.activeInHierarchy) obj.SetActive(false);
        }

        // 해당 포인트 점유 해제(위치 기준으로 탐색)
        foreach (var pt in spawnPoints)
        {
            if (pt == null) continue;
            if (Vector3.SqrMagnitude(pt.transform.position - obj.transform.position) < 0.01f)
            {
                pt.Free();
                break;
            }
        }
    }
    public void ResetState()
    {
        // 카운터 리셋
        spawnCounterGlobal = 0;
        spawnCounterElite = 0;
        spawnCounterMoving = 0;

        // active 리스트가 남아있다면 안전하게 반환 처리
        for (int i = activeList.Count - 1; i >= 0; i--)
        {
            var obj = activeList[i];
            if (obj != null)
            {
                if (poolManager != null) poolManager.Release(obj);
                else obj.SetActive(false);
            }
            activeList.RemoveAt(i);
        }

        // 포인트 점유 해제
        if (spawnPoints != null)
        {
            foreach (var pt in spawnPoints) if (pt != null) pt.Free();
        }
    }


    // 디버그용 즉시 스폰
    [ContextMenu("SpawnOneNow")]
    public void SpawnOneNow()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            SpawnAtPoint(spawnPoints[0]);
        }
    }
}