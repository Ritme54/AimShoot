using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TargetAnimationController : MonoBehaviour
{
    public Animator animator;
    public string layerDamages = "Damages";
    public string layerDeaths = "Deaths";
    public string triggerDamage = "Damage01";
    public string triggerDeath = "Death01";

    public float layerFadeTime = 0.08f;
    public float damageHold = 0.6f;
    public float deathHold = 1.0f;

    int idxDamages = -1;
    int idxDeaths = -1;
    Coroutine running = null;
    bool hasDied = false;


    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null)
        {
            idxDamages = animator.GetLayerIndex(layerDamages);
            idxDeaths = animator.GetLayerIndex(layerDeaths);
        }
    }

    public void PlayHit()
    {
        if (animator == null || hasDied) return;
        if (idxDamages >= 0)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeLayerAndTrigger(idxDamages, 1f, layerFadeTime, triggerDamage, damageHold));
        }
        else if (HasParameter(triggerDamage))
        {
            animator.SetTrigger(triggerDamage);
        }
    }

    public void PlayDie()
    {
        if (animator == null || hasDied) return;
        hasDied = true;
        if (running != null) { StopCoroutine(running); running = null; }

        if (idxDeaths >= 0) animator.SetLayerWeight(idxDeaths, 1f);
        if (HasParameter(triggerDeath)) animator.SetTrigger(triggerDeath);

        StartCoroutine(DisableAfterDelay(deathHold));
    }

    IEnumerator FadeLayerAndTrigger(int layerIndex, float target, float fadeTime, string trigger, float hold)
    {
        float start = animator.GetLayerWeight(layerIndex);
        float t = 0f;
        while (t < fadeTime) { t += Time.deltaTime; animator.SetLayerWeight(layerIndex, Mathf.Lerp(start, target, t / fadeTime)); yield return null; }
        animator.SetLayerWeight(layerIndex, target);

        if (HasParameter(trigger)) { animator.ResetTrigger(trigger); animator.SetTrigger(trigger); }
        yield return new WaitForSeconds(Mathf.Max(0.01f, hold));

        t = 0f; start = animator.GetLayerWeight(layerIndex);
        while (t < fadeTime) { t += Time.deltaTime; animator.SetLayerWeight(layerIndex, Mathf.Lerp(start, 0f, t / fadeTime)); yield return null; }
        animator.SetLayerWeight(layerIndex, 0f);
        running = null;
    }

    IEnumerator DisableAfterDelay(float secs)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, secs));
        var target = GetComponent<Targets>();
        // 안전 플래그(선택적으로 Targets 쪽에 들여놓아도 됨)
        if (target != null)
        {
            Debug.Log($"[TAC] invoking OnReturned for {gameObject.name}");
            target.OnReturned?.Invoke(gameObject); // 여기서 SpawnManager가 받아서 Release 처리
        }
        else
        {
            Debug.LogWarning($"[TAC] No Targets component to notify return for {gameObject.name}");
        }
        // TAC는 직접 비활성화/Release를 하지 않음 (SpawnManager에서 처리)
    }

    bool HasParameter(string name)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in animator.parameters) if (p.name == name) return true;
        return false;
    }

    public void ResetAnimationState()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        hasDied = false;
        if (animator != null)
        {
            if (idxDamages >= 0) animator.SetLayerWeight(idxDamages, 0f);
            if (idxDeaths >= 0) animator.SetLayerWeight(idxDeaths, 0f);
            animator.Rebind();
            animator.Update(0f);
        }
    }
}