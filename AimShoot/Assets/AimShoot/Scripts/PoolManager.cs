using System.Collections.Generic;
using UnityEngine;

// PoolManager: prefab별 풀을 관리합니다.
// SpawnManager에서 Get(prefab)으로 꺼내고 Release(instance)로 반환하세요.
public class PoolManager : MonoBehaviour
{
    private Dictionary<GameObject, List<GameObject>> pools = new Dictionary<GameObject, List<GameObject>>();

    // 풀 웜업(선택). 인스펙터에서 별도 호출하거나 Start에서 호출 가능.
    public void WarmUp(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (!pools.ContainsKey(prefab)) pools[prefab] = new List<GameObject>();

        var list = pools[prefab];
        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(prefab);
            go.SetActive(false);
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
            if (!list[i].activeInHierarchy)
            {
                return list[i];
            }
        }

        // 없으면 새로 생성하여 풀에 추가
        GameObject created = Instantiate(prefab);
        created.SetActive(false);
        list.Add(created);
        return created;
    }

    // 인스턴스를 풀로 반환 (안전하게 비활성화)
    public void Release(GameObject instance)
    {
        if (instance == null) return;
        instance.SetActive(false);
    }

    // 디버그용: 풀 상태 확인(옵션)
    public int GetPoolCount(GameObject prefab)
    {
        if (prefab == null) return 0;
        if (!pools.ContainsKey(prefab)) return 0;
        return pools[prefab].Count;
    }
}