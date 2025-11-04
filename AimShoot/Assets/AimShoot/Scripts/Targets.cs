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

    private bool isElite = false;
    private bool isMoving = false;
    //  private GameManager gm; // 캐싱용(선택)

    [SerializeField] private GameObject eliteVFX; // 정예 타겟용 VFX 오브젝트
    [SerializeField] private MonoBehaviour movementScript; // 이동 로직 스크립트
    [SerializeField] private Renderer[] targetRenderers; // 머터리얼 변경용 렌더러들
    private MaterialPropertyBlock mpb;



    private void Awake()
    {
        currentHP = maxHP;

        audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false; // 자동 재생 방지
        }
//        gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();

    }

    public void ResetTarget()
    {
        isDead = false;
        currentHP = maxHP;

        // 정예/이동 상태 초기화
        isElite = false;
        isMoving = false;

        // VFX 초기화
        if (eliteVFX != null) eliteVFX.SetActive(false);

        // 이동 스크립트 비활성화
        if (movementScript != null) movementScript.enabled = false;

        // 렌더러 Emission 초기화 (materialPropertyBlock 사용 가정)
        if (targetRenderers != null)
        {
            foreach (var r in targetRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", Color.black);
                r.SetPropertyBlock(mpb);
            }
        }

        // 애니메이터가 있다면 리셋(있을 경우에만)
        var animator = GetComponent<Animator>();
        if (animator != null) animator.Rebind();
    }

    // 정예/이동 플래그 설정 메서드(SpawnManager에서 호출)
    public void SetElite(bool elite)
    {
        isElite = elite;
        // 시각적 표시: 예를 들어 머터리얼 변경이나 이펙트 활성화
        // (현재 점수/체력은 동일하게 유지하므로 값 변경은 하지 않음)
    }
    public void SetMoving(bool moving)
    {
        isMoving = moving;
        // 이동 로직을 별도 스크립트로 구성하였다면 활성화/비활성화 처리
        var mover = GetComponent<MonoBehaviour>(); // 필요 시 구체 스크립트로 교체
                                                   // 예: if (mover != null) mover.enabled = moving;
    }


    public void OnHit(bool isHead, int damage)
    {
        // 이미 죽어있으면 아무 처리하지 않음
        if (isDead)
        { return; }

      
        // 기존 OnHit 로직: 데미지 계산 및 체력 감소
        int finalDmg = damage;
        if (isHead)
        {
            finalDmg = Mathf.CeilToInt(damage * 1.5f);
        }

        currentHP -= finalDmg;

        // 피격 이펙트 재생(있을 경우)
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
            Die(isHead);
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
        gameObject.SetActive(false);
        // Die() 내부에서 마지막에 추가 (Targets.cs)
        SpawnManager sm = UnityEngine.Object.FindFirstObjectByType<SpawnManager>();
        if (sm != null)
        {
            sm.NotifyTargetDestroyed(this.gameObject);
            Debug.Log("삭제");
        }



        // 오브젝트 제거(약간 지연) - 이펙트/사운드 재생을 위해 destroyDelay 사용
        Destroy(gameObject, destroyDelay);
    }
  
  
}

