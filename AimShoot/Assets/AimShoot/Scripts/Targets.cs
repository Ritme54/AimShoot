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

    [Header("VFX / Audio")]
    public GameObject hitEffectPrefab;
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
        // 안전한 AudioSource 확보: 프리팹에 없으면 런타임에 추가 (테스트용)
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
        originSpawnPoint = null;    // origin 참조 초기화(여유 안전)
        currentHP = baseMaxHP;      // 또는 baseMaxHP 대신 originalMaxHP를 사용
    }

    // 컴포넌트들이 호출하는 API: override 지정(대체)
    public void SetHPOverride(int? hp)
    {
        hpOverride = hp;
    }

    public void SetScoreOverride(int? score)
    {
        scoreOverride = score;
    }

    // 확실한 초기화: 호출 시 오버라이드 해제
    public void ClearOverrides()
    {
        hpOverride = null;
        scoreOverride = null;
    }

    // 스폰 시 최종 HP 계산: 반드시 Apply(컴포넌트) 후 호출
    public void FinalizeStatsAfterModifiers()
    {
        int appliedHP = hpOverride.HasValue ? hpOverride.Value : baseMaxHP;
        currentHP = appliedHP;
    }

    // Apply incoming visual material via renderer.material assignment (instance)
    public void ApplyMaterialToRenderers(Material mat)
    {
        if (mat == null) return;
        var rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.material = mat; // 인스턴스화된 material 할당(다른 인스턴스 영향 없음)
        }
    }

    // 데미지 처리 (isHead: 헤드샷 여부)
    public void OnHit(bool isHead, int damage)
    {
        if (isDead) return;
        int applied = isHead ? Mathf.CeilToInt(damage * headshotMultiplier) : damage;
        currentHP -= applied;

        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        if (hitSound != null && audioSrc != null) audioSrc.PlayOneShot(hitSound);

        if (currentHP <= 0) Die(isHead);
    }

    // 최종 점수 계산 및 사망 처리
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

        // 이벤트로 점수 전달
        if (OnKilled != null)
        {
            OnKilled.Invoke(awarded);
        }
        else
        {
            Debug.LogWarning($"[Targets] OnKilled 이벤트에 리스너가 없습니다. 지급 점수: {awarded} (Prefab: {gameObject.name})");
        }

        // 사운드 재생
        if (deathSound != null && audioSrc != null) audioSrc.PlayOneShot(deathSound);

        // 비활성화(풀 반환)
        gameObject.SetActive(false);

        // 반환 이벤트 호출 (listener가 없어도 안전하게 호출하지 않음)
        Debug.Log($"Invoking OnReturned for {gameObject.name} (instanceID={gameObject.GetInstanceID()})");
        if (OnReturned != null)
        {
            OnReturned.Invoke(this.gameObject); 
        }
    }

    // 추가 권장 메서드: 현재 적용될 최대 HP 반환 (다른 컴포넌트가 필요하면 호출)
    public int GetEffectiveMaxHP()
    {
        return hpOverride.HasValue ? hpOverride.Value : baseMaxHP;
    }
}