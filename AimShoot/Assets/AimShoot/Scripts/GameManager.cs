using UnityEngine;
using UnityEngine.UI; // UI.Text 사용 시 필요 (만약 TMPro를 쓰면 using TMPro 추가)

// GameManager: 점수 관리, 타이머(시간제) 제어, 로컬 최고기록 저장을 담당합니다.
// 기존 변수명(totalScore, AddScore, ResetScore)을 그대로 유지했습니다.
public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public float timeLimit = 60f;           // 게임 시간(초), 인스펙터에서 조정 가능
    public string bestScoreKey = "BestScore"; // PlayerPrefs에 저장할 키명(짧게)

    [Header("Runtime State")]
    public int totalScore = 0;              // 기존 변수: 누적 점수
    public float timeLeft = 0f;             // 남은 시간(초), 런타임에 업데이트 됨
    public bool isRunning = false;          // 게임 진행중 여부 플래그

    [Header("UI References (optional)")]
    public Text timerText;                  // 타이머 표시용 UI 텍스트(연결 안 해도 동작함)
    public Text scoreText;                  // 점수 표시용 UI 텍스트(연결 안 해도 동작함)
    public Text bestText;                   // 최고기록 표시용 UI 텍스트(연결 안 해도 동작함)

    // Awake: 게임이 시작되기 전 초기화
    void Awake()
    {
        // 시작 시 시간과 점수 초기화
        timeLeft = timeLimit;              // timeLeft를 timeLimit으로 초기화
        totalScore = 0;                    // 누적 점수 초기화
        isRunning = false;                 // 기본적으로는 정지 상태
    }

    // Update: 프레임마다 타이머 처리(게임 진행중일 때만)
    void Update()
    {
        // 게임이 진행 중일 때만 시간 감소 로직 실행
        if (isRunning)
        {
            // 시간 감소(프레임 시간 보정)
            timeLeft -= Time.deltaTime;

            // 타이머 UI 갱신(연결된 텍스트가 있으면)
            UpdateTimerUI();

            // 시간이 0 이하가 되면 게임 종료 처리
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;             // 음수 방지
                EndGame();                 // 게임 종료 호출
            }
        }
    }

    // StartGame: 외부에서 게임 시작을 호출할 때 사용
    public void StartGame()
    {
        totalScore = 0;                    // 점수 초기화
        timeLeft = timeLimit;              // 시간 초기화
        isRunning = true;                  // 게임 진행 상태로 전환
        UpdateScoreUI();                   // UI 갱신
        UpdateTimerUI();                   // UI 갱신
        Debug.Log("Game Started");         // 디버그 로그
    }

    // EndGame: 시간 초과나 외부 이벤트로 게임을 종료할 때 사용
    public void EndGame()
    {
        isRunning = false;                 // 진행 중지
        UpdateScoreUI();                   // 최종 점수 UI 갱신
        SaveBestScoreIfNeeded();           // 최고기록 저장 검사 및 저장
        Debug.Log($"Game Ended. Score: {totalScore}"); // 디버그 로그
        // 필요 시 여기서 결과 화면 호출(씬 전환 또는 UI 활성화) 로직 추가
    }

    // AddScore: 외부(예: Targets.OnDie)에서 호출하여 점수 누적
    public void AddScore(int amount)
    {
    
        totalScore += amount;              // 점수 누적(기존 변수명 유지)
        UpdateScoreUI();                   // UI 즉시 갱신
        Debug.Log($"Score +{amount}, Total: {totalScore}"); // 디버그 로그
    }

    // ResetScore: 점수 초기화(기존 함수명 유지)
    public void ResetScore()
    {
        totalScore = 0;                    // 점수 0으로 리셋
        UpdateScoreUI();                   // UI 갱신
        Debug.Log("Score reset.");         // 디버그 로그
    }

    // UpdateTimerUI: 타이머 UI를 표시 형식으로 갱신(연결된 텍스트가 있을 때만)
    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // 초를 정수형으로 표시(예: 59 -> "00:59" 형식)
            int sec = Mathf.CeilToInt(timeLeft);                   // 올림 처리로 0.9초도 1초로 표시
            int min = sec / 60;                                    // 분 단위(필요 시)
            int s = sec % 60;                                      // 초 단위
            timerText.text = $"{min:00}:{s:00}";                   // "MM:SS" 형식으로 세팅
        }
    }

    // UpdateScoreUI: 점수 UI 갱신 및 최고기록 UI 갱신(연결된 텍스트가 있을 때만)
    void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = totalScore.ToString();               // 점수 숫자만 표시
        }

        if (bestText != null)
        {
            int best = PlayerPrefs.GetInt(bestScoreKey, 0);       // 저장된 최고점 불러오기
            bestText.text = $"Best: {best}";                      // "Best: 123" 형식으로 표시
        }
    }

    // SaveBestScoreIfNeeded: 현재 점수를 로컬 최고기록으로 저장할지 검사 후 저장
    void SaveBestScoreIfNeeded()
    {
        int best = PlayerPrefs.GetInt(bestScoreKey, 0);           // 기존 최고기록 조회
        if (totalScore > best)
        {
            PlayerPrefs.SetInt(bestScoreKey, totalScore);         // 갱신 시 저장
            PlayerPrefs.Save();                                   // 즉시 저장
            Debug.Log($"New Best Score: {totalScore}");          // 디버그 로그
        }
    }

    // Helper: 즉시 타임어택 모드를 멈추고(디버깅용) 시간이 남았더라도 강제 종료
    public void ForceEnd()
    {
        if (isRunning)
        {
            EndGame();                   // EndGame 호출로 정리
        }
    }
}