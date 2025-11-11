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
    public AudioClip eliteAudio;
    [Range(0f, 1f)] public float audioVolume = 1f;

    // 내부
    bool applied = false;
    AudioSource audioSrc;
    Targets targets;

    void Awake()
    {
        targets = GetComponent<Targets>();
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
        }

        // 초기화: 할당된 VFX가 있으면 비활성화
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
            else
            {
                Debug.LogWarning($"[EliteModifier] Targets 컴포넌트가 없습니다: {gameObject.name}");
            }


            if (eliteAudio != null && audioSrc != null)
            {
                audioSrc.PlayOneShot(eliteAudio, audioVolume);
            }

            applied = true;
        }
        else if (!on && applied)
        {
            if (targets != null)
            {
                targets.SetHPOverride(null);
                targets.SetScoreOverride(null);
            }

            applied = false;
        }
    }

    // 풀 반환 시 호출 가능
    public void ResetElite()
    {
        Apply(false);
    }

    // 안전장치: 비활성화 시에도 상태 해제
    void OnDisable()
    {
        if (applied) Apply(false);
    }

    // (선택) 에디터에서 잘못된 할당을 빨리 확인하게 하는 검증용 코드
#if UNITY_EDITOR
    void OnValidate()
    {
        if (overrideHP < 0) overrideHP = 0;
        if (overrideScore < 0) overrideScore = 0;
        if (audioVolume < 0f) audioVolume = 0f;
        if (audioVolume > 1f) audioVolume = 1f;
    }
#endif
}