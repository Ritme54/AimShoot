using UnityEngine;
using TMPro; // TextMeshPro 사용 시 필요

// UIController: GameManager와 (선택) GunController와 연동해 HUD를 갱신함
public class UIController : MonoBehaviour
{
    [Header("Refs (assign in Inspector)")]
    public GameManager gm;        // GameManager 참조 (기존 GameManager 사용)
    public GunController gun;     // GunController 참조 (있으면 탄창 표시, 없으면 null 허용)

    [Header("UI (TMP)")]
    public TextMeshProUGUI tText; // TimerText (상단 중앙)
    public TextMeshProUGUI sText; // ScoreText (우측 상단)
    public TextMeshProUGUI aText; // AmmoText (우측 중간) - optional

    void Start()
    {
        // 안전성: gm이 할당되어 있지 않다면 자동으로 씬에서 찾아봄 (선택사항)
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }
        // gun은 선택적(없어도 동작)
    }

    void Update()
    {
        // 타이머 UI 갱신 (GameManager의 timeLeft 사용)
        if (gm != null && tText != null)
        {
            // 남은 시간 초 단위로 표시 (MM:SS)
            int sec = Mathf.CeilToInt(gm.timeLeft);      // 기존 GameManager 변수명 유지
            int min = sec / 60;
            int s = sec % 60;
            tText.text = string.Format("{0:00}:{1:00}", min, s);
        }

        // 점수 UI 갱신 (GameManager의 totalScore 사용)
        if (gm != null && sText != null)
        {
            sText.text = gm.totalScore.ToString();      // 기존 변수명 유지
        }

        // 탄창 UI 갱신 (GunController에 Ammo 정보가 있을 때만)
        if (aText != null)
        {
            if (gun != null)
            {
                // GunController에 public int currentAmmo, public int maxAmmo가 있다면 표시
                // 변수명이 없다면 이 부분은 주석처리하거나 빈칸 표기
                // 안전하게 reflection 대신 null-check로 접근
                // 아래 코드는 gun이 해당 필드를 갖고 있다고 가정(없으면 컴파일 에러 - 이 경우 주석 처리 필요)
              aText.text = $"{gun.currentAmmo} / {gun.maxAmmo}";
            }
            else
            {
                // gun이 할당되지 않았을 때 기본 표시
                aText.text = "- / -";
            }
        }
    }
}