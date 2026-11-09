using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "savedata.json");

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 게임 시작 시 자동 로드
        LoadGame();
    }

    // 게임 로드
    public void LoadGame()
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                GameData loadedData = JsonUtility.FromJson<GameData>(json);

                // GameManager에 데이터 적용
                ApplyDataToGameManager(loadedData);

                Debug.Log("게임 데이터 로드 완료!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"게임 로드 실패: {e.Message}");
            }
        }
        else
        {
            Debug.Log("저장된 데이터가 없습니다. 기본값을 사용합니다.");
        }
    }

    // 게임 저장
    public void SaveGame()
    {
        try
        {
            // GameManager에서 현재 데이터 가져오기
            GameData dataToSave = GetDataFromGameManager();

            string json = JsonUtility.ToJson(dataToSave, true);
            File.WriteAllText(SavePath, json);

            Debug.Log($"게임 저장 완료! 경로: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"게임 저장 실패: {e.Message}");
        }
    }

    // GameManager에서 데이터 수집 (세이브)
    private GameData GetDataFromGameManager()
    {
        GameData data = new GameData();

        if (GameManager.Instance != null)
        {
            // 각종 데이터
            data.currentSkin = GameManager.Instance.GameData.currentSkin;
            data.playerName = GameManager.Instance.GameData.playerName;

            // 볼륨 데이터
            data.backGroundMusicVolume = GameManager.Instance.GameData.backGroundMusicVolume;
            data.effectSoundVolume = GameManager.Instance.GameData.effectSoundVolume;

            // ItemQueue, BoardFillCursor
            data.itemQueue = GameManager.Instance.GameData.itemQueue;
            data.boardFillCursor = GameManager.Instance.GameData.boardFillCursor;

            // Dictionary -> List 변환
            data.highScores.Clear();
            foreach (var kvp in GameManager.Instance.highScores)
            {
                data.highScores.Add(new HighScoreEntry(kvp.Key, kvp.Value));
            }

            // Character Skins 정보 저장
            data.characterSkins = new List<bool>(GameManager.Instance.GameData.characterSkins);
            // stageUnlocked Skins 정보 저장
            data.stageUnlocked = new List<bool>(GameManager.Instance.GameData.stageUnlocked);
            // stageHighScore 정보 저장
            data.stageHighScore = new List<int>(GameManager.Instance.GameData.stageHighScore);

            // 랭크모드 데이터 저장
            data.rankModeHighScore = GameManager.Instance.GameData.rankModeHighScore;
            data.rankModeUnlocked = GameManager.Instance.GameData.rankModeUnlocked;
        }

        return data;
    }

    // GameManager에 데이터 적용 (로드)
    private void ApplyDataToGameManager(GameData data)
    {
        if (GameManager.Instance != null)
        {
            // 각종 데이터
            GameManager.Instance.GameData.currentSkin = data.currentSkin;
            GameManager.Instance.GameData.playerName = data.playerName;

            // 볼륨 데이터
            GameManager.Instance.GameData.backGroundMusicVolume = data.backGroundMusicVolume;
            GameManager.Instance.GameData.effectSoundVolume = data.effectSoundVolume;

            // ItemQueue, BoardFillCursor
            GameManager.Instance.GameData.itemQueue = data.itemQueue;
            GameManager.Instance.GameData.boardFillCursor = data.boardFillCursor;

            // Character Skins 정보 불러오기 및 길이 보정
            // 만약 저장된 데이터가 부족하면 false로 채움
            GameManager.Instance.GameData.characterSkins = new List<bool>(data.characterSkins);
            int diff = GameData.SkinCount - GameManager.Instance.GameData.characterSkins.Count;
            for (int i = 0; i < diff; i++)
            {
                GameManager.Instance.GameData.characterSkins.Add(false);
            }

            // stageUnlocked 정보 불러오기 및 길이 보정
            // 만약 저장된 데이터가 부족하면 false로 채움
            GameManager.Instance.GameData.stageUnlocked = new List<bool>(data.stageUnlocked);
            diff = GameData.StageCount - GameManager.Instance.GameData.stageUnlocked.Count;
            for (int i = 0; i < diff; i++)
            {
                GameManager.Instance.GameData.stageUnlocked.Add(false);
            }

            // stageHighScore 정보 불러오기 및 길이 보정
            // 만약 저장된 데이터가 부족하면 0으로 채움
            GameManager.Instance.GameData.stageHighScore = new List<int>(data.stageHighScore);
            diff = GameData.StageCount - GameManager.Instance.GameData.stageHighScore.Count;
            for (int i = 0; i < diff; i++)
            {
                GameManager.Instance.GameData.stageHighScore.Add(0);
            }

            // List -> Dictionary 변환
            GameManager.Instance.highScores.Clear();
            foreach (var entry in data.highScores)
            {
                GameManager.Instance.highScores[entry.key] = entry.value;
            }

            // 랭크모드 데이터 불러오기
            GameManager.Instance.GameData.rankModeHighScore = data.rankModeHighScore;
            GameManager.Instance.GameData.rankModeUnlocked = data.rankModeUnlocked;
        }
    }

    // 애플리케이션 종료 시 자동 저장
    void OnApplicationQuit()
    {
        SaveGame();
    }

    // 앱이 백그라운드로 갈 때 저장 (모바일용)
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
        }
    }

    // 저장 파일 삭제
    public void DeleteSaveData()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("저장 데이터 삭제 완료!");
        }
    }

    // 저장 파일 존재 여부 확인
    public bool SaveFileExists()
    {
        return File.Exists(SavePath);
    }
}