using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    private Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    private Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();

    [Header("Options")]
    public Transform poolRoot;
    public int maxPoolSizePerPrefab = 100; // 0 = 제한 없음
    public bool resetOnRelease = true;

    void Awake()
    {
        if (poolRoot == null)
        {
            var go = new GameObject("PoolManager_Root");
            go.transform.SetParent(this.transform, false);
            poolRoot = go.transform;
        }
    }

    // 풀 웜업
    public void WarmUp(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab);
            go.SetActive(false);
            go.transform.SetParent(poolRoot, true);
            stack.Push(go);
            instanceToPrefab[go] = prefab;
        }
    }

    // 풀에서 꺼냄(활성화된 상태로 반환)
    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }

        while (stack.Count > 0)
        {
            var inst = stack.Pop();
            if (inst == null) continue;
            EnsureResetOnGet(inst);
            inst.SetActive(true);
            instanceToPrefab[inst] = prefab;
            return inst;
        }

        var created = Instantiate(prefab);
        created.SetActive(true);
        created.transform.SetParent(poolRoot, true);
        instanceToPrefab[created] = prefab;
        EnsureResetOnGet(created);
        return created;
    }

    // Get 후 위치/회전 지정 후 즉시 재생(편의 메서드)
    public GameObject GetAndPlay(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var inst = Get(prefab);
        if (inst == null) return null;

        inst.transform.position = pos;
        inst.transform.rotation = rot;
        inst.SetActive(true);

        var poolable = inst.GetComponent<Unity.VisualScripting.IPoolable>();
        if (poolable != null) poolable.New();

        return inst;
    }

    // 풀에 반환(공통 Release). ReleaseAndFree 역할 통합.
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        // reset 처리 (간결)
        if (resetOnRelease)
        {
            var poolable = instance.GetComponent<Unity.VisualScripting.IPoolable>();
            if (poolable != null)
            {
                poolable.Free();
            }
            else
            {
                var t = instance.GetComponent<Targets>();
                if (t != null) t.ResetTarget();

                var pm = instance.GetComponent<PatrolMovement>();
                if (pm != null) pm.ResetMovement();

                var em = instance.GetComponent<EliteModifier>();
                if (em != null) em.ResetElite();

                var sp = instance.GetComponent<SPMovement>();
                if (sp != null) sp.ResetSPMovement();
            }
        }

        // 부모 정리 및 비활성화
        instance.transform.SetParent(poolRoot, true);
        if (instance.activeInHierarchy) instance.SetActive(false);

        // 어떤 prefab 풀에 속하는지 역참조로 찾기
        if (!instanceToPrefab.TryGetValue(instance, out var prefab))
        {
            // 역참조 정보가 없으면 안전하게 파괴
            Destroy(instance);
            return;
        }

        if (!pools.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }

        // 풀 크기 제한
        if (maxPoolSizePerPrefab > 0 && stack.Count >= maxPoolSizePerPrefab)
        {
            instanceToPrefab.Remove(instance);
            Destroy(instance);
            return;
        }
        stack.Push(instance);
    }

    // 풀 상태 조회
    public int GetPoolCount(GameObject prefab)
    {
        if (prefab == null) return 0;
        if (!pools.TryGetValue(prefab, out var stack)) return 0;
        return stack.Count;
    }

    // EnsureResetOnGet: 풀에서 꺼낼 때 초기화 처리(간결)
    void EnsureResetOnGet(GameObject inst)
    {
        if (inst == null) return;

        var t = inst.GetComponent<Targets>();
        if (t != null) t.ResetTarget();

        var pm = inst.GetComponent<PatrolMovement>();
        if (pm != null) pm.ResetMovement();

        var em = inst.GetComponent<EliteModifier>();
        if (em != null) em.ResetElite();

        var sp = inst.GetComponent<SPMovement>();
        if (sp != null) sp.ResetSPMovement();
    }
}