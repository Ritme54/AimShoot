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
    public int maxActiveTargets = 6;          // 동시 활성 표적 수
    public float initialSpawnDelay = 1f;      // 첫 스폰 딜레이
    public float spawnInterval = 1.5f;        // 기본 스폰 간격
    public float eliteChance = 0.1f;          // 정예 등장 확률
    public float movingChance = 0.2f;         // 이동형 등장 확률

    [Header("References")]
    public PoolManager poolManager;           // 인스펙터에서 할당
    public LayerMask targetLayerMask;         // SpawnPoint의 충돌 검사에 사용할 레이어 마스크

    // 내부 상태
    private List<GameObject> active = new List<GameObject>();
    private GameManager gm;

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
        yield return new WaitForSeconds(initialSpawnDelay);

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

            if (active.Count < maxActiveTargets)
            {
                SpawnPoint pt = PickAvailableSpawnPoint();
                if (pt != null)
                {
                    SpawnAtPoint(pt);
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void CleanupActiveList()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            GameObject obj = active[i];
            if (obj == null)
            {
                active.RemoveAt(i);
                continue;
            }
            if (!obj.activeInHierarchy)
            {
                active.RemoveAt(i);
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

        // prefab 선택(동일 비율)
        GameObject prefab = PickPrefabRandom();
        if (prefab == null) return;

        bool isElite = Random.value < eliteChance;
        bool isMoving = Random.value < movingChance;

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
            t.SetElite(isElite);
            t.SetMoving(isMoving);
        }

        obj.SetActive(true);

        if (!active.Contains(obj)) active.Add(obj);
        pt.Occupy();
    }

    GameObject PickPrefabRandom()
    {
        if (targetPrefabs == null || targetPrefabs.Length == 0) return null;
        int idx = Random.Range(0, targetPrefabs.Length);
        return targetPrefabs[idx];
    }

    // Targets가 파괴(또는 반환)되면 호출되는 함수
    public void NotifyTargetDestroyed(GameObject obj)
    {
        if (obj == null) return;

        if (active.Contains(obj)) active.Remove(obj);

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