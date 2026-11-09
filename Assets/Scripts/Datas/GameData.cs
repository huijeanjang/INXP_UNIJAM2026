using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class HighScoreEntry
{
    public string key; // 사용자명
    public int value;  // 점수

    public HighScoreEntry(string key, int value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public class GameData
{
    public const int SkinCount = 10; // 전체 스킨 개수(필요시 조정)
    public const int StageCount = 4; // 전체 스테이지 개수(필요시 조정)
    public int currentSkin = 0; // 현재 선택된 스킨 번호
    public string playerName = "추억수집가"; // 플레이어 네임?

    // 볼륨
    public float backGroundMusicVolume = 0.5f;
    public float effectSoundVolume = 0.5f;

    // Item Queue 저장용
    public ItemQueue itemQueue = new ItemQueue();

    // Board Fill Cursor 저장용
    public BoardFillCursor boardFillCursor = new BoardFillCursor();

    /// <summary>
    /// (게임잼용) High Scores 저장용
    /// </summary>
    public List<HighScoreEntry> highScores = new List<HighScoreEntry>();

    /// <summary>
    /// Character Skins 획득 여부 저장용
    /// </summary>
    public List<bool> characterSkins;

    /// <summary>
    /// 무한모드 하이스코어 저장용
    /// </summary>
    public int infiniteModeHighScore = 0;

    /// <summary>
    /// 랭크모드 하이스코어 저장용 (스테이지별 배열과 완전히 분리된 별도 로컬 저장값)
    /// </summary>
    public int rankModeHighScore = -1;

    /// <summary>
    /// 스테이지 해금 여부 저장용
    /// </summary>
    public List<bool> stageUnlocked;
    public bool rankModeUnlocked = false; // 랭크모드 해금 여부

    /// <summary>
    /// 스테이지별 하이스코어 저장용
    /// </summary>
    public List<int> stageHighScore;

    /// <summary>
    /// 스테이지별 클리어 기준 저장용
    /// </summary>
    public List<int> stageClearCriteria;

    public GameData()
    {
        characterSkins = new List<bool>(new bool[SkinCount]);

        stageUnlocked = new List<bool>(new bool[StageCount]);
        stageUnlocked[0] = true;

        stageHighScore = new List<int>(new int[StageCount]);


        for (int i = 0; i < StageCount; i++)
        {
            stageHighScore[i] = -1;
        }

        stageClearCriteria = new List<int>(new int[StageCount]);

        stageClearCriteria[0] = 5000; // 스테이지 1 클리어 기준 5000점
        stageClearCriteria[1] = 5000; // 스테이지 2 클리어 기준 5000점
        stageClearCriteria[2] = 5000; // 스테이지 3 클리어 기준 5000점
        stageClearCriteria[3] = 5000; // 스테이지 4 클리어 기준 5000점
    }
}

