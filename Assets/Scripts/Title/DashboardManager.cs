using UnityEngine;
using TMPro;

/// <summary>
/// 대시보드(랭킹) 패널. 랭크모드 랭킹만 표시합니다.
/// </summary>
public class DashboardManager : MonoBehaviour
{
    [Header("랭킹 목록 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI rankContentText;

    const string LoadingMessage = "불러오는 중...";
    const string ErrorMessage = "랭킹을 불러올 수 없습니다.";
    const string EmptyMessage = "아직 기록이 없습니다.";

    void OnEnable()
    {
        RefreshRankModeRank();
    }

    void OnDisable()
    {
        if (GameManager.Instance == null || GameManager.Instance.soundManager == null) return;
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
    }

    /// <summary>
    /// 랭크모드 랭킹을 백엔드에서 가져와 rankContentText에 표시합니다.
    /// </summary>
    public void RefreshRankModeRank()
    {
        if (rankContentText == null) return;

        rankContentText.text = LoadingMessage;

        BackendRank.Instance.GetRankModeRankListForUI(
            onSuccess: list =>
            {
                if (rankContentText == null) return;
                if (list == null || list.Count == 0)
                {
                    rankContentText.text = EmptyMessage;
                    return;
                }

                string content = "";
                foreach (var item in list)
                {
                    string nickname = item.nickname ?? "";
                    string scoreStr = item.score.ToString();
                    int dashLength = 20 - nickname.Length - scoreStr.Length;
                    if (dashLength < 0) dashLength = 0;
                    content += $"{item.rank}. [ {nickname} ] {new string('-', dashLength)} {item.score} 점\n";
                }
                rankContentText.text = content.TrimEnd();
            },
            onFailure: () =>
            {
                if (rankContentText != null)
                    rankContentText.text = ErrorMessage;
            }
        );
    }
}
