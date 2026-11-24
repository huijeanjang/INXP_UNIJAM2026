using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.VisualScripting;

public class LobbyController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject itemSkinSelectPanel; // 아이템 스킨 선택 패널
    [SerializeField] private GameObject stageSelectPanel; // 스테이지 선택 패널
    [SerializeField] private GameObject settigPanel; // 설정 패널

    [SerializeField] private TMPro.TextMeshProUGUI stageNameText; // 스테이지 이름 텍스트
    [SerializeField] private TMPro.TextMeshProUGUI stageHighScoreText; // 스테이지 최고 점수 텍스트
    [SerializeField] private TMPro.TextMeshProUGUI stageDescriptionText; // 스테이지 설명 텍스트
    [SerializeField] private StarRatingView stageStars; // 최고 점수 기준 별 표시
    // [SerializeField] private Button stageSelectButton; // 스테이지 선택 버튼

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "MainScene";

    private int nowStageIndex = 0;

    private void Start()
    {
        stageSelectPanel.SetActive(false);
        itemSkinSelectPanel.SetActive(false);
    }
    public void openStageSelectPanel(int stageIndex)
    {
        nowStageIndex = stageIndex;
        stageSelectPanel.SetActive(true); // 스테이지 선택 패널 활성화

        LobbyStageData stageData = GameManager.Instance.GetStageData(stageIndex);
        stageNameText.text = stageData.StageName; // 스테이지 이름 표시

        int highScore = Mathf.Max(0, GameManager.Instance.GameData.stageHighScore[stageIndex]); // -1(첫플레이) → 0
        stageHighScoreText.text = $"최고 점수  {highScore}점"; // 스테이지 최고 점수 표시

        // 최고 점수 기준 별 + 기준 점수 표시 (로비는 연출 없이 즉시)
        if (stageStars != null)
            stageStars.Show(stageData, highScore, animate: false);

        stageDescriptionText.text = stageData.stageDescription; // 스테이지 설명 표시
    }
    public void onStartStageButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        if (GameManager.Instance.GameData.stageUnlocked[nowStageIndex])
        {
            GameManager.Instance.currentStageNum = nowStageIndex;
            GameManager.Instance.IsRankMode = false;
            SceneLoader.Load(gameSceneName);
        }
        else
        {
            Debug.Log("해금되지 않은 스테이지입니다.");
        }
    }

    public void onRankModeButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        itemSkinSelectPanel.SetActive(true); // 아이템 스킨 선택 패널 활성화
    }

    public void selectItemSkinButton(int itemSkinIndex)
    {
        if (GameManager.Instance.RankModeItemSkinIndex == itemSkinIndex) return;
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        GameManager.Instance.RankModeItemSkinIndex = itemSkinIndex;
    }

    public void onStartRankModeButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        GameManager.Instance.IsRankMode = true;

        SceneLoader.Load(gameSceneName);
    }

    public void onClosePanel()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        stageSelectPanel.SetActive(false); // 스테이지 선택 패널 비활성화
        itemSkinSelectPanel.SetActive(false); // 아이템 스킨 선택 패널 비활성화
    }
    public void onGoToTitle()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        SceneLoader.Load(titleSceneName);
    }
    public void onSettingButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        settigPanel.SetActive(!settigPanel.activeSelf);
    }
}
