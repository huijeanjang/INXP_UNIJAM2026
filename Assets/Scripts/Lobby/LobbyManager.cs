using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class LobbyStageButtonVisual
{
    [Tooltip("스테이지 버튼 이미지")]
    public Image buttonImage;

    [Tooltip("클리어 시 표시할 스프라이트")]
    public Sprite clearedSprite;
}

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private GameObject rankModeBtn; // 랭크모드 버튼
    [SerializeField] private GameObject rankModeUnlockPopup; // 랭크모드 해금 알림 팝업
    [SerializeField] private LobbyController LobbyController; // 로비 패널 참조
    [SerializeField] private Image curtainImage; // 커튼 이미지 참조
    [SerializeField] private Vector2 curtainImagePosition = new Vector2(0, 0f); // 커튼 이미지 위치

    [Header("Stage Button Visuals")]
    [SerializeField] private List<LobbyStageButtonVisual> stageButtonVisuals;

    [Header("Scene Names")]
    [SerializeField] private string GameSceneName = "MainScene";
    [SerializeField] private string ReViewSceneName = "ReViewScene";

    private Sequence _seq;
    private readonly List<Sprite> _defaultStageButtonSprites = new List<Sprite>();

    private void Awake()
    {
        CacheDefaultStageButtonSprites();
    }

    public void Start()
    {
        RefreshRankModeButton();
        RefreshRankModeUnlockPopup();

        GameManager.Instance.IsRankMode = false; // 로비 진입 시 항상 랭크모드 플래그 초기화

        Time.timeScale = 1f;
        _seq?.Kill();

        GameManager.Instance.soundManager.PlayBGM(SoundManager.BGM.Title);

        RefreshStageButtonSprites(); // 스테이지 버튼 이미지 갱신
        AdjustCurtainImagePosition(); // 커튼 이미지 위치 조정
    }

    // 랭크모드 버튼 표시를 현재 해금 상태에 맞춤
    public void RefreshRankModeButton()
    {
        if (rankModeBtn == null || GameManager.Instance == null) return;
        rankModeBtn.SetActive(GameManager.Instance.GameData.rankModeUnlocked);
    }

    // 랭크모드 해금 후 첫 로비 진입 시(확인 전까지) 해금 알림 팝업 표시
    private void RefreshRankModeUnlockPopup()
    {
        if (rankModeUnlockPopup == null || GameManager.Instance == null) return;
        rankModeUnlockPopup.SetActive(GameManager.Instance.ShouldShowRankModeUnlockPopup());
    }

    // 랭크모드 해금 팝업 확인 버튼 클릭
    public void OnRankModeUnlockPopupConfirm()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        GameManager.Instance.MarkRankModeUnlockPopupShown();

        if (rankModeUnlockPopup != null)
        {
            rankModeUnlockPopup.SetActive(false);
        }
    }

    /// <summary>
    /// 로비 진입 시 커튼 이미지 위치 조정
    /// </summary>
    public void AdjustCurtainImagePosition()
    {
        curtainImage.rectTransform.anchoredPosition = new Vector2(0, 0); // 커튼 이미지 초기 위치 설정
        curtainImagePosition = new Vector2(0, 0); // 커튼 이미지 적용할 포지션 초기화

        for (int i = 0; i < GameManager.Instance.GameData.stageUnlocked.Count; i++)
        {
            if (i != 0 && GameManager.Instance.GameData.stageUnlocked[i])
            {
                curtainImagePosition += new Vector2(400f, 0); // 커튼 이미지 적용할 포지션
            }
            else if (i != 0)
            {
                break;
            }
        }
        _seq.Join(curtainImage.rectTransform.DOAnchorPos(curtainImagePosition, 2f).SetEase(Ease.OutExpo));

    }

    /// <summary>
    /// 로비 진입 시 스테이지 버튼 이미지를 클리어 여부에 맞게 갱신한다.
    /// </summary>
    public void RefreshStageButtonSprites()
    {
        if (GameManager.Instance == null) return;

        for (int i = 0; i < stageButtonVisuals.Count; i++)
        {
            LobbyStageButtonVisual visual = stageButtonVisuals[i];
            if (visual == null || visual.buttonImage == null) continue;

            if (GameManager.Instance.IsStageCleared(i) && visual.clearedSprite != null)
            {
                visual.buttonImage.sprite = visual.clearedSprite;
            }
            else if (i < _defaultStageButtonSprites.Count && _defaultStageButtonSprites[i] != null)
            {
                visual.buttonImage.sprite = _defaultStageButtonSprites[i];
            }
        }
    }

    private void CacheDefaultStageButtonSprites()
    {
        _defaultStageButtonSprites.Clear();

        foreach (LobbyStageButtonVisual visual in stageButtonVisuals)
        {
            if (visual != null && visual.buttonImage != null)
            {
                _defaultStageButtonSprites.Add(visual.buttonImage.sprite);
            }
            else
            {
                _defaultStageButtonSprites.Add(null);
            }
        }
    }

    public void onStageButton(int stageIndex)
    {
        if (GameManager.Instance.GameData.stageUnlocked[stageIndex])
        {
            GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
            LobbyController.openStageSelectPanel(stageIndex);
        }
        else
        {
            Debug.Log("해금되지 않은 스테이지입니다.");
        }

    }

    public void onReViewButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        SceneLoader.Load(ReViewSceneName);
    }
}
