using UnityEngine;
using System.Collections.Generic;
// using UnityEngine.SceneManagement;
// using UnityEngine.UI;
using System;
using Utils;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameData gamedata = new GameData();    // GameData (세이브 필요한 데이터)
    [SerializeField] private List<StageRuntimeConfiguration> stageConfigurations = new List<StageRuntimeConfiguration>();
    public GameData GameData => gamedata;

    public SoundManager soundManager { get; private set; }

    public Dictionary<string, int> highScores = new Dictionary<string, int>();
    public int currentStageNum = -1; // 플레이 할 스테이지 넘버 (스테이지 밖: -1)
    public string nextSceneAfterCutscene = "MainScene"; // 컷씬 이후 이동할 씬 이름

    public bool IsRankMode { get; set; } = false; // 랭크모드 진입 여부
    public int RankModeItemSkinIndex { get; set; } = 0; // 랭크모드 진입 시 선택한 아이템 스킨 인덱스

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            soundManager = GetComponent<SoundManager>();
        }
        else
        {
            Destroy(gameObject);
        }

        Application.targetFrameRate = 60;
    }

    public void SetCurrentStage(int stageIndex)
    {
        currentStageNum = stageIndex;
    }

    /// <summary>
    /// (에디터/테스트용) 모든 스테이지와 랭크모드 해금
    /// </summary>
    public void UnlockAllStagesForTest()
    {
        EnsureStageUnlockListSize();

        for (int i = 0; i < GameData.StageCount; i++)
        {
            gamedata.stageUnlocked[i] = true;
        }

        gamedata.rankModeUnlocked = true;
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log("[GameManager] 모든 스테이지/랭크모드 해금");
    }

    /// <summary>
    /// (에디터/테스트용) 스테이지 해금을 초기 상태(1스테이지만 해금)로 초기화
    /// </summary>
    public void ResetStageUnlockToDefault()
    {
        EnsureStageUnlockListSize();

        for (int i = 0; i < GameData.StageCount; i++)
        {
            gamedata.stageUnlocked[i] = i == 0;
        }

        gamedata.rankModeUnlocked = false;
        SaveLoadManager.Instance?.SaveGame();
        Debug.Log("[GameManager] 스테이지 해금 초기화 (1스테이지만 해금)");
    }

    private void EnsureStageUnlockListSize()
    {
        if (gamedata.stageUnlocked == null)
        {
            gamedata.stageUnlocked = new List<bool>();
        }

        while (gamedata.stageUnlocked.Count < GameData.StageCount)
        {
            gamedata.stageUnlocked.Add(false);
        }
    }

    public StageRuntimeConfiguration GetCurrentStageConfiguration()
    {
        return GetStageConfiguration(currentStageNum);
    }

    public void EnsureValidCurrentStage()
    {
        if (currentStageNum >= 0)
        {
            return;
        }

        if (stageConfigurations.Count > 0)
        {
            currentStageNum = stageConfigurations[0].stageIndex;
            return;
        }

        currentStageNum = 0;
    }

    public StageRuntimeConfiguration GetStageConfiguration(int stageIndex)
    {
        for (int i = 0; i < stageConfigurations.Count; i++)
        {
            StageRuntimeConfiguration configuration = stageConfigurations[i];
            if (configuration != null && configuration.stageIndex == stageIndex)
            {
                return configuration;
            }
        }

        return null;
    }

    /// <summary>
    /// 랭크모드용 Past 스테이지 후보 풀 반환 (stageConfigurations 리스트 포지션 0~3, stageIndex 필드와 무관)
    /// </summary>
    public List<StageRuntimeConfiguration> GetRankModeStagePool()
    {
        const int RankModePoolSize = 4;
        int count = Mathf.Min(RankModePoolSize, stageConfigurations.Count);

        if (stageConfigurations.Count < RankModePoolSize)
        {
            Debug.LogWarning($"[GameManager] 랭크모드 스테이지 풀이 {RankModePoolSize}개 미만입니다. (현재 {stageConfigurations.Count}개)");
        }

        List<StageRuntimeConfiguration> pool = new List<StageRuntimeConfiguration>(count);
        for (int i = 0; i < count; i++)
        {
            pool.Add(stageConfigurations[i]);
        }

        return pool;
    }

    /// <summary>
    /// 랭크모드 하이스코어 업데이트 (스테이지별 하이스코어와 완전히 분리된 별도 로컬 저장값)
    /// </summary>
    public void UpdateRankModeScore(int score)
    {
        if (gamedata.rankModeHighScore < 0 || score > gamedata.rankModeHighScore)
        {
            gamedata.rankModeHighScore = score;
            SaveLoadManager.Instance?.SaveGame();
        }
    }

    /// <summary>
    /// 모든 GameData 초기화 (테스트용)
    /// </summary>
    public void ResetAllData()
    {
        gamedata = new GameData(); // GameData 기본값으로 완전 초기화
        SaveLoadManager.Instance?.SaveGame(); // 초기화된 상태를 파일에도 덮어씀
        Debug.Log("[GameManager] 모든 데이터가 초기화되었습니다.");
    }

    /// <summary>
    /// 스테이지별 하이스코어 업데이트
    /// </summary>
    /// <param name="levelName"></param>
    /// <param name="score"></param>
    public void UpdateHighScore(string levelName, int score)
    {

        if (!highScores.ContainsKey(levelName) || highScores[levelName] < score)
        {
            highScores[levelName] = score;
            SaveLoadManager.Instance?.SaveGame(); // 즉시 저장
        }
    }

    /// <summary>
    /// 스테이지 튜토리얼 팝업을 봤다는 것을 표시(-1을 0으로 바꿔서 다음부터 재노출 방지).
    /// stageHighScore == -1을 "튜토리얼 노출 조건"으로도 겸용하므로, 이미 실제 점수가 있으면 건드리지 않는다.
    /// </summary>
    public void MarkStageTutorialSeen(int stageIndex)
    {
        if (gamedata.stageHighScore == null || stageIndex < 0 || stageIndex >= gamedata.stageHighScore.Count)
            return;

        if (gamedata.stageHighScore[stageIndex] != -1) return;

        gamedata.stageHighScore[stageIndex] = 0;
        SaveLoadManager.Instance?.SaveGame();
    }

    /// <summary>
    /// 스테이지별 하이스코어 업데이트
    /// </summary>
    public void UpdateStageHighScore(int highScore)
    {
        // 스테이지 리스트가 비어있으면 갱신 불가
        if (gamedata.stageHighScore == null || gamedata.stageHighScore.Count == 0)
            return;

        int stageIndex = currentStageNum;
        // 인덱스가 범위를 벗어나면 0번 스테이지로 강제 매핑
        if (stageIndex < 0 || stageIndex >= gamedata.stageHighScore.Count)
            stageIndex = 0;

        if (gamedata.stageHighScore[stageIndex] >= highScore) return;

        gamedata.stageHighScore[stageIndex] = highScore;
        SaveLoadManager.Instance?.SaveGame();

    }

    /// <summary>
    /// 스테이지 해금(private)
    /// </summary>
    private void UnlockStage(int stageNum)
    {
        // 유효 범위 체크
        if (stageNum < 0 || stageNum >= GameData.StageCount) return;

        // 리스트 길이 보정 (대비용)
        while (gamedata.stageUnlocked.Count < GameData.StageCount)
        {
            gamedata.stageUnlocked.Add(false);
        }

        gamedata.stageUnlocked[stageNum] = true;
        SaveLoadManager.Instance?.SaveGame();
    }

    /// <summary>
    /// 다음 스테이지 해금
    /// </summary>
    public void UnlockNextStage()
    {
        int nextStage = currentStageNum + 1;
        if (nextStage >= GameData.StageCount) return; // 마지막 스테이지면 해금 불필요
        if (gamedata.stageUnlocked[nextStage]) return; // 이미 해금됨

        UnlockStage(nextStage);
    }

    public void UnlockRankMode()
    {
        if (gamedata.rankModeUnlocked) return; // 이미 해금됨

        gamedata.rankModeUnlocked = true;
        SaveLoadManager.Instance?.SaveGame();
    }

    /// <summary>
    /// 스테이지 클리어 여부 
    /// </summary>
    public bool IsStageCleared(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= GameData.StageCount) return false;
        if (gamedata.stageHighScore == null || gamedata.stageClearCriteria == null) return false;
        if (stageIndex >= gamedata.stageHighScore.Count || stageIndex >= gamedata.stageClearCriteria.Count) return false;

        int highScore = gamedata.stageHighScore[stageIndex];
        if (highScore < 0) return false;

        return highScore >= gamedata.stageClearCriteria[stageIndex];
    }
}