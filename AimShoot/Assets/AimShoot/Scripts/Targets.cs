using UnityEngine;

public class Targets : MonoBehaviour
{

    [Header("Stats")]
    public int maxHP = 25;               // 최대 체력 (인스펙터에서 조정)
    public int currentHP;                // 런타임용 현재 체력(초기화는 Awake에서 수행)
    public int baseScore = 10;           // 기본 점수(파괴 시 지급)


    [Header("Effext & Audio")]
    public GameObject hitEffectPrefab;   // 피격 이펙트 프리팹(옵션)
    public AudioClip hitSound;           // 피격 사운드(옵션)
    public AudioClip deathSound;         // 사망 사운드(옵션)


    [Header("Destroy")]
    public float destroyDelay = 0.05f;   // 오브젝트 삭제 지연(이펙트 재생을 위해)
    public bool spawnScorePopup = true;  // 점수 팝업 생성 여부(추후 UI 연동용)

    // 내부 필드(외부에서 변경하지 않음)
    private AudioSource audioSrc;        // 내부에서 사용할 AudioSource
    private bool isDead = false;         // 이미 죽었는지 플래그(중복 처리 방지)

    private void Awake()
    {
        currentHP = maxHP;

        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false; // 자동 재생 방지
        }
    }

    public void OnHit(bool isHead, int damage)
    {
        if (!isDead)
        {
            return; 
        }
               
            int finalDmg = damage;
            if (isHead)
            {
                finalDmg = Mathf.CeilToInt(damage * 1.5f);
            }
            currentHP -= finalDmg;
            // 피격 이펙트 재생(있을 경우) - 표적 중앙에 간단히 생성
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // 피격 사운드 재생(있을 경우)
            if (hitSound != null)
            {
                audioSrc.PlayOneShot(hitSound);
            }

            // 체력이 0 이하이면 사망 처리 호출
            if (currentHP <= 0)
            {
                Die(isHead);                 // 헤드샷 여부를 전달하여 점수 보정에 사용
            
        }

    }

    void Die(bool wasHead)
    {
        if (isDead) return;
        isDead = true;

        int awarded = baseScore;
        if (wasHead)
        {
            awarded = Mathf.CeilToInt(baseScore * 1.5f);
        }
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.AddScore(awarded);
        }
        else
        {
            Debug.Log($"Score +{awarded} (GameManager 없음)");
        }
        if (deathSound != null)
        {
            audioSrc.PlayOneShot(deathSound);
        }
        if (spawnScorePopup)
        {
            Debug.Log($"Score Popup: +{awarded}");
            // 추후 UI 팝업을 위해 GameManager에 위임하는 방식으로 변경 권장
        }

        // 오브젝트 제거(약간 지연) - 이펙트/사운드 재생을 위해 destroyDelay 사용
        Destroy(gameObject, destroyDelay);
    }
    // ResetTarget: 재사용(오브젝트 풀링 등) 시 호출하여 상태 리셋
    public void ResetTarget()
    {
        isDead = false;
        currentHP = maxHP;
        // 시각적 리셋(머티리얼, 애니메이션 등)이 필요하면 여기에 추가
    }
}

