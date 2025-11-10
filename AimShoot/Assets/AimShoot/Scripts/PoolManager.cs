using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// PoolManager: prefab별 풀을 관리합니다.
// SpawnManager에서 Get(prefab)으로 꺼내고 Release(instance)로 반환하세요.
public class PoolManager : MonoBehaviour
{
    private Dictionary<GameObject, List<GameObject>> pools = new Dictionary<GameObject, List<GameObject>>();

    [Header("Options")]
    public Transform poolRoot;                // 풀 내부 오브젝트들을 정리할 부모(없으면 자동 생성)
    public int maxPoolSizePerPrefab = 100;    // 0이면 제한 없음 (선택사항)
    public bool resetOnRelease = true;        // Release 시 Targets.ResetTarget() 등을 호출할지 여부

    void Awake()
    {
        if (poolRoot == null)
        {
            // 런타임에 전용 부모 생성
            GameObject go = new GameObject("PoolManager_Root");
            go.transform.SetParent(this.transform, false);
            poolRoot = go.transform;
        }
    }

    // 풀 웜업(선택). 인스펙터에서 별도 호출하거나 Start에서 호출 가능.
    public void WarmUp(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        if (!pools.ContainsKey(prefab)) pools[prefab] = new List<GameObject>();

        var list = pools[prefab];
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab);
            go.SetActive(false);
            // 부모 지정으로 씬 정리
            go.transform.SetParent(poolRoot, true);
            list.Add(go);
        }
    }

    // prefab에 해당하는 비활성 인스턴스를 반환(없으면 새로 생성)
    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;
        if (!pools.ContainsKey(prefab)) pools[prefab] = new List<GameObject>();

        var list = pools[prefab];

        // 파괴된 참조 정리
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null) list.RemoveAt(i);
        }

        // 사용 가능한 비활성 인스턴스 반환
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null) continue;
            if (!item.activeInHierarchy)
            {
                // 안전성 보장: ResetTarget 같은 초기화가 보장되어 있는지 확인(방어 코드)
                EnsureResetOnGet(item);
                return item;
            }
        }

        // 없으면 새로 생성하여 풀에 추가
        GameObject created = Instantiate(prefab);
        created.SetActive(false);
        created.transform.SetParent(poolRoot, true);
        list.Add(created);

        // 생성 직후 초기화(안전)
        EnsureResetOnGet(created);

        return created;
    }

    // 인스턴스를 풀로 반환 (안전하게 비활성화)
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        // Reset/초기화 - 안전장치
        if (resetOnRelease)
        {
            // Targets, Movement, VFX 등 주요 컴포넌트를 찾아서 Reset 호출
            var t = instance.GetComponent<Targets>();
            if (t != null) t.ResetTarget();

            var pm = instance.GetComponent<PatrolMovement>();
            if (pm != null) pm.ResetMovement();

            var em = instance.GetComponent<EliteModifier>();
            if (em != null) em.ResetElite();

            var special = instance.GetComponent<SPMovement>();
            if (special != null) special.ResetSPMovement();

            // 기타 VFX/Audio 객체가 별도로 활성화 되어있으면 끄기
            // (프리팹 구조에 따라 추가 처리)
        }

        // 부모를 poolRoot로 정리하여 씬 뷰가 깔끔해지도록 함
        if (poolRoot != null) instance.transform.SetParent(poolRoot, true);

        // 비활성화
        if (instance.activeInHierarchy) instance.SetActive(false);

        // (선택) 풀 크기 제한: 초과 시 즉시 파괴
        if (maxPoolSizePerPrefab > 0)
        {
            // 해당 인스턴스가 속한 prefab을 찾아 리스트 크기 비교
            // (이 구현은 단순히 모든 풀을 순회 — 성능 우려가 있으면 prefab을 캐싱해야 함)
            foreach (var kv in pools)
            {
                var list = kv.Value;
                if (list.Contains(instance))
                {
                    if (list.Count > maxPoolSizePerPrefab)
                    {
                        list.Remove(instance);
                        Destroy(instance);
                    }
                    break;
                }
            }
        }
    }

    // 디버그용: 풀 상태 확인(옵션)
    public int GetPoolCount(GameObject prefab)
    {
        if (prefab == null) return 0;
        if (!pools.ContainsKey(prefab)) return 0;
        return pools[prefab].Count;
    }

    // PoolManager.cs — 예시 구현 (클래스 내부에 추가)
    public GameObject GetAndPlay(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var inst = Get(prefab); // 기존 Get(prefab) 메서드 사용 (null 체크 포함)
        if (inst == null) return null;

        inst.transform.position = pos;
        inst.transform.rotation = rot;

        // 활성화: 풀에서 꺼낼 때는 SetActive(true) 또는 풀에서 활성화를 담당할 수 있음
        inst.SetActive(true);

        // IPoolable 인터페이스에 정의된 메서드 호출 (New = 꺼낼 때 초기화)
        var poolable = inst.GetComponent<Unity.VisualScripting.IPoolable>();
        if (poolable != null)
        {
            try
            {
                poolable.New();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Poolable.New() 호출 중 예외: {inst.name} — {ex.Message}");
            }
        }

        return inst;
    }

    // 반환 헬퍼: 풀에 다시 넣을 때 사용
    public void ReleaseAndFree(GameObject instance)
    {
        if (instance == null) return;

        // IPoolable이 있다면 Free() 호출
        var poolable = instance.GetComponent<Unity.VisualScripting.IPoolable>();
        if (poolable != null)
        {
            try
            {
                poolable.Free();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Poolable.Free() 호출 중 예외: {instance.name} — {ex.Message}");
            }
        }

        // 비활성화 및 풀 내부 관리(기존 Release 또는 풀에 반환하는 로직 사용)
        instance.SetActive(false);
        Release(instance); // 기존 PoolManager.Release(instance) 사용(필요 시 구현에 맞게 변경)
    }

    // 풀에서 꺼낼 때(혹은 생성 직후) 객체가 올바르게 초기화되어 있는지 방어적으로 확인
    void EnsureResetOnGet(GameObject instance)
    {
        if (instance == null) return;
        // Targets.ResetTarget을 호출하면 중복 호출 방지 로직이 Targets 쪽에 있으면 안전
        var t = instance.GetComponent<Targets>();
        if (t != null) t.ResetTarget();

        // Movement/Elite 컴포넌트들도 초기화 필요하면 호출
        var pm = instance.GetComponent<PatrolMovement>();
        if (pm != null) pm.ResetMovement();

        var em = instance.GetComponent<EliteModifier>();
        if (em != null) em.ResetElite();

        var special = instance.GetComponent<SPMovement>();
        if (special != null) special.ResetSPMovement();
    }
}