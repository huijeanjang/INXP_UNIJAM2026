using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// 별 개수 표시용 컴포넌트 (게임오버 UI, 로비 공용)
/// 빈 별 이미지는 씬에 항상 배치해 두고, 그 위에 겹친 "채운 별" 이미지를 개수만큼 차례로 등장시킨다.
/// </summary>
public class StarRatingView : MonoBehaviour
{
    [Tooltip("빈 별 위에 겹쳐 둔 채운 별 이미지. 왼쪽부터 차례로 (1별, 2별, 3별)")]
    [SerializeField] private Image[] filledStars = new Image[LobbyStageData.MaxStars];

    [Tooltip("각 별 아래 기준 점수 텍스트. 왼쪽부터 차례로 (비워두면 표시 안 함)")]
    [SerializeField] private TMP_Text[] scoreTexts = new TMP_Text[LobbyStageData.MaxStars];

    [Header("등장 연출")]
    [SerializeField] private float startDelay = 0.3f;   // 첫 별 등장 전 대기 (창 열림 연출 고려)
    [SerializeField] private float interval = 0.3f;     // 별 사이 간격
    [SerializeField] private float popDuration = 0.35f; // 별 하나 등장 시간
    [SerializeField] private Ease popEase = Ease.OutBack;

    private Sequence _seq;

    /// <summary>
    /// 스테이지 기준 점수 텍스트 + 점수에 따른 별 표시를 한 번에
    /// </summary>
    public void Show(LobbyStageData stageData, int score, bool animate = true)
    {
        SetScoreLabels(stageData);
        SetStars(stageData != null ? stageData.GetStarCount(score) : 0, animate);
    }

    /// <summary>
    /// 각 별 아래에 스테이지의 별 기준 점수 표시 (숫자만)
    /// </summary>
    public void SetScoreLabels(LobbyStageData stageData)
    {
        for (int i = 0; i < scoreTexts.Length; i++)
        {
            if (scoreTexts[i] == null) continue;

            bool hasScore = stageData != null && stageData.starScores != null && i < stageData.starScores.Length;
            scoreTexts[i].text = hasScore ? stageData.starScores[i].ToString() : string.Empty;
        }
    }

    /// <summary>
    /// 별 개수 표시. animate = false 면 연출 없이 즉시 표시 (로비 등)
    /// </summary>
    public void SetStars(int starCount, bool animate = true)
    {
        _seq?.Kill();
        _seq = null;

        // 채운 별: 개수만큼만 켜고 나머지는 끔 (빈 별은 항상 보임)
        for (int i = 0; i < filledStars.Length; i++)
        {
            Image star = filledStars[i];
            if (star == null) continue;

            star.gameObject.SetActive(i < starCount);
            star.rectTransform.localScale = Vector3.one;
            SetAlpha(star, 1f);
        }

        if (!animate || starCount <= 0) return;

        // 차례로 커지면서(OutBack) 페이드인
        _seq = DOTween.Sequence()
            .SetUpdate(true)      // 일시정지(timeScale 0) 중에도 재생
            .SetLink(gameObject); // 오브젝트 파괴 시 자동 Kill

        int order = 0;
        for (int i = 0; i < filledStars.Length && i < starCount; i++)
        {
            Image star = filledStars[i];
            if (star == null) continue;

            star.rectTransform.localScale = Vector3.zero;
            SetAlpha(star, 0f);

            float t = startDelay + order * interval;
            _seq.Insert(t, star.rectTransform.DOScale(1f, popDuration).SetEase(popEase));
            _seq.Insert(t, star.DOFade(1f, popDuration * 0.5f));
            order++;
        }
    }

    private void OnDisable()
    {
        // 연출 도중 꺼지면 최종 상태로 맞춰 둠
        _seq?.Complete();
        _seq = null;
    }

    private static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
