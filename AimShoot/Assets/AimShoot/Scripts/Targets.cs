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

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        // 초기화
        ResetTarget();
    }

    // 풀에서 꺼냈을 때 반드시 호출
    public void ResetTarget()
    {
        isDead = false;
        ClearOverrides();
        currentHP = baseMaxHP;
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

        int awarded = GetFinalScore(wasHead);

        if (OnKilled != null) OnKilled.Invoke(awarded);

        if (deathSound != null && audioSrc != null) audioSrc.PlayOneShot(deathSound);

        // 비활성화(풀 반환)
        gameObject.SetActive(false);

        if (OnReturned != null) OnReturned.Invoke(this.gameObject);
    }

    internal int? GetEffectiveMaxHP()
    {
        throw new NotImplementedException();
    }//'Targets'에는 'GetEffectiveMaxHP'에 대한 정의가 포함되어 있지 않고, 'Targets' 형식의 첫 번째 인수를 허용하는 액세스 가능한 확장 메서드 'GetEffectiveMaxHP'이(가) 없습니다. using 지시문 또는 어셈블리 참조가 있는지 확인하세요.
    //삭제할것
}