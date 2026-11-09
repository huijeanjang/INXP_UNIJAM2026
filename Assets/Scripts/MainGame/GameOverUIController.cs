using UnityEngine;
using TMPro;
using Utils;
public class GameOverUIController : MonoBehaviour
{
    public TextMeshProUGUI gameResult; // 게임 결과 출력
    public TextMeshProUGUI alarm; // 기록 저장 여부 등 출력
    public GameObject DashboardPanel; // 대시보드 패널

    [Header("Scene Names")]
    [SerializeField] private string CutSceneName = "CutScene";
    [SerializeField] private string LobbySceneName = "LobbyScene";

    [Header("Buttons")]
    [SerializeField] private GameObject GiveButton; // 선물하러 가기 버튼
    [SerializeField] private GameObject LobbyButton; // 로비로 버튼
    [SerializeField] private GameObject RestartButton; // 다시하기 버튼
    [SerializeField] private GameObject RankingButton; // 랭킹 보기 버튼 (랭크모드 전용)

    private void Start()
    {
        CustomLog.Info("GameOverUIController 초기화");

        GiveButton.SetActive(false); // 선물하러 가기 버튼 비활성화
        RestartButton.SetActive(true); // 다시하기 버튼 활성화
        LobbyButton.SetActive(true); // 로비 버튼 활성화
        RankingButton.SetActive(false); // 랭킹 버튼 기본 비활성화
        DashboardPanel.SetActive(false); // 대시보드 패널 비활성화
    }
    private void OnEnable()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnGameOver += OnGameOverTextUpdate;
        }
    }

    private void OnDisable()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnGameOver -= OnGameOverTextUpdate;
        }
    }

    private void OnDestroy()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.OnGameOver -= OnGameOverTextUpdate;
        }
    }

    /// <summary>
    /// 게임 오버 이후 뜨는 텍스트 업데이트 + 다음 스테이지 해금
    /// </summary>
    public void OnGameOverTextUpdate()
    {
        int myScore = GameSceneManager.Instance.CurrentScore;

        // 랭킹모드일 시
        if (GameManager.Instance.IsRankMode)
        {
            gameResult.text = myScore.ToString();
            alarm.text = "";

            GameManager.Instance.UpdateRankModeScore(myScore);

            GiveButton.SetActive(false);
            RestartButton.SetActive(true);
            LobbyButton.SetActive(true);
            RankingButton.SetActive(true);

            BackendRank.Instance.RankInsertCurrentRankModeHighScore();
            return;
        }

        RankingButton.SetActive(false);

        // 스테이지별 최고점수 기록 업데이트
        GameManager.Instance.UpdateStageHighScore(GameSceneManager.Instance.CurrentScore);

        int maxScore = GameManager.Instance.GameData.stageHighScore[GameManager.Instance.currentStageNum];
        int clearScore = GameManager.Instance.GameData.stageClearCriteria[GameManager.Instance.currentStageNum];

        //점수 출력
        gameResult.text = myScore.ToString();
        CustomLog.Info("점수 출력");

        int nextStage = GameManager.Instance.currentStageNum + 1;


        // 점수, 스테이지 클리어 여부 별 알람 메시지 출력

        // 클리어 점수 달성 + 다음 스테이지 있음 + 다음 스테이지 미해금 => 다음 스테이지 해금
        if (myScore >= clearScore
            && nextStage < GameData.StageCount
            && !GameManager.Instance.GameData.stageUnlocked[nextStage])
        {
            alarm.text = "다음 스테이지가 해금되었습니다!";
            GameManager.Instance.UnlockNextStage(); //다음 스테이지 해금

            CustomLog.Info($"스테이지 {nextStage}이 해금되었습니다.");

            GiveButton.SetActive(true); // 선물하러 가기 버튼 활성화
            RestartButton.SetActive(false); // 다시하기 버튼 비활성화
            LobbyButton.SetActive(false); // 로비 버튼 비활성화

            //게임 종료 직후 스테이지 하이스코어를 뒤끝 랭킹에 반영
            BackendRank.Instance.RankInsertCurrentStageHighScore();
        }
        // 클리어 점수 달성 + 다음 스테이지 없음 + 랭크모드 미해금 => 랭킹모드 해금
        else if (myScore >= clearScore && nextStage >= GameData.StageCount && !GameManager.Instance.GameData.rankModeUnlocked)
        {
            alarm.text = "모든 스테이지를 클리어했습니다!";
            CustomLog.Info("모든 스테이지 클리어, 랭크 모드 해금");

            GameManager.Instance.UnlockRankMode();

            GiveButton.SetActive(true); // 선물하러 가기 버튼 활성화
            RestartButton.SetActive(false); // 다시하기 버튼 비활성화
            LobbyButton.SetActive(false); // 로비 버튼 비활성화

            //게임 종료 직후 스테이지 하이스코어를 뒤끝 랭킹에 반영
            BackendRank.Instance.RankInsertCurrentStageHighScore();
        }
        // 클리어 점수 미달 => 단순 실패
        else if (myScore < clearScore)
        {
            CustomLog.Info("실패");
            alarm.text = "실패";
        }
        // 스테이지 클리어 + 신기록
        else if (myScore >= maxScore)
        {
            CustomLog.Info("신기록!");
            alarm.text = "신기록!";

            //게임 종료 직후 스테이지 하이스코어를 뒤끝 랭킹에 반영
            BackendRank.Instance.RankInsertCurrentStageHighScore();
        }
        // 스테이지 클리어 + 신기록 아님
        else
        {
            CustomLog.Info("스테이지 클리어 후 재도전, 신기록 아님");
            alarm.text = "";
        }

    }

    public void OnGiveButton() // 선물하러 가기 버튼 클릭
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick); // 버튼 클릭 효과음 재생

        GameManager.Instance.nextSceneAfterCutscene = LobbySceneName; // 컷씬 이후 로비로 이동해야 함 지정
        SceneLoader.Load(CutSceneName);
    }
    public void OnLobbyButton() // 로비 버튼 클릭
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick); // 버튼 클릭 효과음 재생

        SceneLoader.Load(LobbySceneName);

    }

    public void OnRankingButton() // 랭킹 버튼 클릭 (랭크모드 전용)
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        DashboardPanel.SetActive(true); // 대시보드 패널 활성화

        Debug.Log($"[GameOverUIController] 랭크모드 최고 점수(로컬): {GameManager.Instance.GameData.rankModeHighScore}");
    }
}
