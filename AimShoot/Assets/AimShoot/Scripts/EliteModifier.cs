using UnityEngine;

// 정예 컴포넌트: override 방식으로 Targets에 값을 설정
[DisallowMultipleComponent]
public class EliteModifier : MonoBehaviour
{
    [Header("Elite Stats (Override)")]
    public int overrideHP = 0;                // 0이면 사용 안 함
    public int overrideScore = 0;             // 0이면 사용 안 함

    [Header("Visuals & Audio")]
    public Material eliteMaterial;
    public GameObject eliteVFX;
    public AudioClip eliteAudio;
    [Range(0f, 1f)] public float audioVolume = 1f;

    // 내부
    bool applied = false;
    AudioSource audioSrc;
    Targets targets;

    void Awake()
    {
        targets = GetComponent<Targets>();
        audioSrc = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        if (eliteVFX != null) eliteVFX.SetActive(false);
    }

    // Apply true = enable elite; false = disable
    public void Apply(bool on)
    {
        if (on && !applied)
        {
            if (targets != null)
            {
                targets.SetHPOverride(overrideHP > 0 ? (int?)overrideHP : null);
                targets.SetScoreOverride(overrideScore > 0 ? (int?)overrideScore : null);
                if (eliteMaterial != null) targets.ApplyMaterialToRenderers(eliteMaterial);
            }
            if (eliteVFX != null) eliteVFX.SetActive(true);
            if (eliteAudio != null && audioSrc != null) audioSrc.PlayOneShot(eliteAudio, audioVolume);
            applied = true;
        }
        else if (!on && applied)
        {
            // 해제: 대상은
            //
            //
            //
            // 또는 명시 해제로 관리
            if (targets != null)
            {
                targets.SetHPOverride(null);
                targets.SetScoreOverride(null);
            }
            if (eliteVFX != null) eliteVFX.SetActive(false);
            applied = false;
        }
    }

    // 풀 반환 시 호출 가능
    public void ResetElite()
    {
        Apply(false);
    }
}