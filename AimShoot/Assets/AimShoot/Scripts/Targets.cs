using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable] public class KilledEvent : UnityEvent<int> { }
[Serializable] public class ReturnedEvent : UnityEvent<GameObject> { }

public class Targets : MonoBehaviour
{
    [Header("Base Stats")]
    public int baseMaxHP = 25;                // 프리팹 기본 최대 체력
    [HideInInspector] public int currentHP;   // 런타임 체력
    public int baseScore = 10;                // 프리팹 기본 점수

    [Header("Headshot")]
    [Tooltip("헤드샷일 때 데미지 및 점수에 곱할 배수")]
    public float headshotMultiplier = 1.5f;

    [Header("Impact FX (basic)")]
    [Tooltip("기본 명중 이펙트(대부분의 타겟에 공통으로 사용)")]
    public GameObject defaultImpactFx;    // 대부분의 타겟에서 사용할 기본 이펙트
    [Tooltip("헤드샷일 때만 재생할 이펙트(있으면 사용, 없으면 기본 이펙트를 사용)")]
    public GameObject headImpactFx;       // 헤드샷일 때만 사용 (선택)
    public GameObject headImpactFx2;       // 헤드샷일 때만 사용 (선택)
    public float impactFxScale = 1f;
    public float impactFxLifetime = 1f;

    [Header("Audio (legacy)")] 
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Events")]
    public KilledEvent OnKilled;              // GameManager.AddScore 연결 권장
    public ReturnedEvent OnReturned;          // SpawnManager.NotifyTargetDestroyed 연결 권장

    // 내부 상태
    bool isDead = false;

    // override / temp 값 (컴포넌트가 설정)
    int? hpOverride = null;                   // null => 사용 안 함
    int? scoreOverride = null;                // null => 사용 안 함

    // AudioSource
    AudioSource audioSrc;
    [HideInInspector] public SpawnPoint originSpawnPoint;


    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
        }
        ResetTarget();
    }

    // 풀에서 꺼냈을 때 반드시 호출
    public void ResetTarget()
    {
        isDead = false;
        ClearOverrides();           // hpOverride/scoreOverride 초기화
        currentHP = baseMaxHP;
    }

    public void SetHPOverride(int? hp)
    {
        hpOverride = hp;
    }

    public void SetScoreOverride(int? score)
    {
        scoreOverride = score;
    }

    public void ClearOverrides()
    {
        hpOverride = null;
        scoreOverride = null;
    }

    public void FinalizeStatsAfterModifiers()
    {
        int appliedHP = hpOverride.HasValue ? hpOverride.Value : baseMaxHP;
        currentHP = appliedHP;
    }

    public void ApplyMaterialToRenderers(Material mat)
    {
        if (mat == null) return;
        var rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.material = mat;
        }
    }

    // 기존 OnHit(bool,is) 호환성 유지: hitPoint/normal 정보가 없을 때 transform 기준으로 처리
    public void OnHit(bool isHead, int damage)
    {
        // transform.position 및 transform.up을 기본 히트 위치/노멀로 사용해서 새 메서드 호출
        Vector3 fallbackPoint = transform.position;
        Vector3 fallbackNormal = transform.up;
        OnHit(isHead, damage, fallbackPoint, fallbackNormal);
    }

    // 변경된 OnHit: 발사 측에서 전달한 hitPoint와 hitNormal을 받아 처리
    public void OnHit(bool isHead, int damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;

        int applied = isHead ? Mathf.CeilToInt(damage * headshotMultiplier) : damage;
        currentHP -= applied;

        // 사운드(히트)
        if (hitSound != null && audioSrc != null) audioSrc.PlayOneShot(hitSound);

        // 표적 자체에서 보여줄 임팩트(기본 또는 헤드샷 전용)
        GameObject fxPrefab = (isHead && headImpactFx != null) ? headImpactFx : defaultImpactFx;

          

        if (fxPrefab != null)
        {
            // 임팩트 생성: world 위치 hitPoint, 회전은 hitNormal 기준
            Quaternion rot = Quaternion.LookRotation(hitNormal != Vector3.zero ? hitNormal : Vector3.up);
            var inst = Instantiate(fxPrefab, hitPoint, rot);
            inst.transform.localScale = Vector3.one * impactFxScale;
            Destroy(inst, Mathf.Max(0.1f, impactFxLifetime));
        }

        if (isHead && headImpactFx2 != null)
        {

            Quaternion rot2 = Quaternion.LookRotation(hitNormal != Vector3.zero ? hitNormal : Vector3.up);
            var inst2 = Instantiate(headImpactFx2, hitPoint, rot2);
            inst2.transform.localScale = Vector3.one * impactFxScale;
            Destroy(inst2, Mathf.Max(0.1f, impactFxLifetime));
        }


            if (currentHP <= 0)
        {
            Die(isHead);
        }
    }

    int GetFinalScore(bool wasHead)
    {
        int s = scoreOverride.HasValue ? scoreOverride.Value : baseScore;
        if (wasHead) s = Mathf.CeilToInt(s * headshotMultiplier);
        return s;
    }

    void Die(bool wasHead)
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Targets.Die called: " + name);

        int awarded = GetFinalScore(wasHead);

        if (OnKilled != null)
        {
            OnKilled.Invoke(awarded);
        }
        else
        {
            Debug.LogWarning($"[Targets] OnKilled 이벤트에 리스너가 없습니다. 지급 점수: {awarded} (Prefab: {gameObject.name})");
        }

        if (deathSound != null && audioSrc != null) audioSrc.PlayOneShot(deathSound);

        // 풀 사용 시에는 비활성화하여 풀로 반환하도록 처리(SpawnManager/PoolManager가 Release 담당)
        gameObject.SetActive(false);

        Debug.Log($"Invoking OnReturned for {gameObject.name} (instanceID={gameObject.GetInstanceID()})");
        if (OnReturned != null)
        {
            OnReturned.Invoke(this.gameObject);
        }
    }

    public int GetEffectiveMaxHP()
    {
        return hpOverride.HasValue ? hpOverride.Value : baseMaxHP;
    }
}