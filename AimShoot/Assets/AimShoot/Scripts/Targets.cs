using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[Serializable] public class KilledEvent : UnityEvent<int> { }
[Serializable] public class ReturnedEvent : UnityEvent<GameObject> { }

public class Targets : MonoBehaviour
{
    [Header("Base Stats")]
    public int baseMaxHP = 25;
    [HideInInspector] public int currentHP;
    public int baseScore = 10;

    [Header("Headshot")]
    [Tooltip("헤드샷일 때 데미지 및 점수에 곱할 배수")]
    public float headshotMultiplier = 1.5f;

    [Header("Impact FX (basic)")]
    public GameObject defaultImpactFx;
    public GameObject headImpactFx;
    public GameObject headImpactFx2;
    public float impactFxScale = 1f;
    public float impactFxLifetime = 1f;

    [Header("Audio (legacy)")]
    public AudioClip hitSound;
    public AudioClip deathSound;

    [Header("Events")]
    public KilledEvent OnKilled;
    public ReturnedEvent OnReturned;

    // 내부 상태
    bool isDead = false;

    // overrides
    int? hpOverride = null;
    int? scoreOverride = null;

    // 컴포넌트 캐시
    AudioSource audioSrc;
    Animator anim;
    bool returnInProgress;

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
        
    public void ResetTarget()
    {
        isDead = false;
        ClearOverrides();
        currentHP = GetEffectiveMaxHP();
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
        currentHP = GetEffectiveMaxHP();
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

    // 기존 호환성: transform 기준으로 처리
    public void OnHit(bool isHead, int damage)
    {
        Vector3 fallbackPoint = transform.position;
        Vector3 fallbackNormal = transform.up;
        OnHits(isHead, damage, fallbackPoint, fallbackNormal);
    }

    // 주된 히트 처리
    public void OnHits(bool isHead, int damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isDead) return;
        // 데미지 애니메이션 재생
        var tac = GetComponent<TargetAnimationController>();
        if (tac != null) tac.PlayHit();
        else GetComponent<Animator>()?.SetTrigger("Damage01");

        // 데미지 적용
        int applied = isHead ? Mathf.CeilToInt(damage * headshotMultiplier) : damage;
        currentHP -= applied;

        // 히트 사운드 재생
        if (hitSound != null && audioSrc != null)
        {
            audioSrc.PlayOneShot(hitSound);
        }

        // FX 생성
        GameObject fxPrefab = (isHead && headImpactFx != null) ? headImpactFx : defaultImpactFx;
        if (fxPrefab != null)
        {
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

        // 사망 검사
        if (currentHP <= 0)
        {
            Die(isHead);
        }
    }

   void Die(bool wasHead)
{
    if (isDead) return;
    isDead = true;

    int awarded = GetFinalScore(wasHead);

    if (OnKilled != null)
    {
        OnKilled.Invoke(awarded);
    }
    else
    {
        Debug.LogWarning($"[Targets] OnKilled 이벤트에 리스너가 없습니다. 지급 점수: {awarded} (Prefab: {gameObject.name})");
    }

    // 사망 사운드
    if (deathSound != null && audioSrc != null)
    {
        audioSrc.PlayOneShot(deathSound);
    }

    // 우선 OnReturned 콜백을 미리 호출하는 대신, 반환은 애니/콜백 후에 하길 권장합니다.
    // 만약 PoolManager가 즉시 제거를 기대한다면 아래 코루틴에서 호출합니다.

    var tac = GetComponent<TargetAnimationController>();
    if (tac != null)
    {
        // TargetAnimationController가 애니 재생과 비활성화(또는 반환)를 책임지도록 한다.
        tac.PlayDie();
            StartCoroutine(ForceReturnIfStillActive(2.0f));
        }
    else
    {
        // fallback: Animator 트리거를 직접 걸고, 애니 길이 만큼 기다렸다가 반환/비활성화
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.ResetTrigger("Death01");
            animator.SetTrigger("Death01");
        }

        // 안전한 코루틴 호출 — 클래스 내부에 아래 IEnumerator DisableAfterDelay가 있어야 함
        StartCoroutine(DisableAfterDelayFallback(animator, 1.0f)); // 1.0f는 폴백 시간(필요시 조정)
    }
}
    IEnumerator ForceReturnIfStillActive(float wait)
    {
        yield return new WaitForSeconds(wait);
        if (!gameObject.activeInHierarchy) yield break; // 이미 반환됨
        Debug.LogWarning("[Targets] Force returning " + gameObject.name);
        OnReturned?.Invoke(gameObject);
        gameObject.SetActive(false);
    }
    // Targets 클래스 내부에 추가할 코루틴
    IEnumerator DisableAfterDelayFallback(Animator animator, float fallbackSeconds)
{
    // 애니 클립 길이를 우선 찾아 사용
    float wait = fallbackSeconds;
    if (animator != null && animator.runtimeAnimatorController != null)
    {
        // Death 애니 클립들 중 하나의 길이를 찾거나 평균/최대값을 사용
        var clips = animator.runtimeAnimatorController.animationClips;
        float found = 0f;
        foreach (var c in clips)
        {
            if (c == null) continue;
            // 클립명 규칙에 따라 검사 (예: "Death" 포함)
            if (c.name.Contains("Death") || c.name.Contains("death"))
            {
                found = Mathf.Max(found, c.length);
            }
        }
        if (found > 0f) wait = found;
    }

    // 대기
    yield return new WaitForSeconds(Mathf.Max(0.05f, wait + 0.05f));

    // 풀 반환 또는 OnReturned 이벤트 발생
    if (OnReturned != null)
        OnReturned.Invoke(this.gameObject);
    else
        Debug.LogWarning($"[Targets] OnReturned 이벤트에 리스너가 없습니다. (Prefab: {gameObject.name})");

    // 오브젝트 비활성화(혹은 PoolManager.Release로 교체)
    gameObject.SetActive(false);
}

    int GetFinalScore(bool wasHead)
    {
        int s = scoreOverride.HasValue ? scoreOverride.Value : baseScore;
        if (wasHead) s = Mathf.CeilToInt(s * headshotMultiplier);
        return s;
    }

    public int GetEffectiveMaxHP()
    {
        return hpOverride.HasValue ? hpOverride.Value : baseMaxHP;
    }
}