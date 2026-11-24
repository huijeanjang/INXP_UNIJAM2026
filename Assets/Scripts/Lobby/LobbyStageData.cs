using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/LobbyStageData")]
public class LobbyStageData : ScriptableObject
{
    public const int MaxStars = 3;

    public int StageID;
    public string StageName; // 스테이지 이름
    [TextArea] public string stageDescription; // 아래 설명

    [Header("별 기준 점수 (1별 = 클리어, 오름차순)")]
    public int[] starScores = { 5000, 7500, 10000 }; // [0]: 1별, [1]: 2별, [2]: 3별

    // 클리어 기준 점수 (= 1별 점수)
    public int ClearScore => starScores[0];

    // 점수에 해당하는 별 개수 (0 = 미클리어)
    public int GetStarCount(int score)
    {
        int stars = 0;
        for (int i = 0; i < starScores.Length; i++)
        {
            if (score >= starScores[i]) stars = i + 1;
            else break;
        }
        return stars;
    }

    public bool IsCleared(int score) => GetStarCount(score) >= 1;

    private void OnValidate()
    {
        // 별 개수는 항상 3개로 유지
        if (starScores == null || starScores.Length != MaxStars)
            System.Array.Resize(ref starScores, MaxStars);
    }
}
