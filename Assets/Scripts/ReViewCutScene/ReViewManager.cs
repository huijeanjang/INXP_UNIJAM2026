using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReViewManager : MonoBehaviour
{
    [SerializeField] private GameObject settigPanel; // 설정 패널 참조

    [Header("Scene Names")]
    [SerializeField] private string LobbySceneName = "LobbyScene";
    [SerializeField] private string CutSceneName = "CutScene";
    [SerializeField] private string ReViewSceneName = "ReViewScene";

    [Header("Cutscene Buttons")]
    [SerializeField] private Button[] cutSceneButtons;   // 버튼 5개 (인트로 + 스테이지 4개)

    [Header("Description Popup")]
    [SerializeField] private GameObject descriptionPanel; // 설명 팝업 패널
    [SerializeField] private TMP_Text summaryText;        // 스토리 요약 텍스트
    [Tooltip("cutSceneButtons 와 같은 순서 (0: 인트로, 1~4: 스테이지 1~4)")]
    [SerializeField, TextArea(3, 8)] private string[] summaries = new string[1 + GameData.StageCount];

    private int _selectedStageIndex; // 팝업에서 "보기" 누르면 재생할 스테이지 번호

    void Start()
    {
        GameManager.Instance.soundManager.PlayBGM(SoundManager.BGM.Title);
        settigPanel.SetActive(false); // 설정 패널 초기화 - 숨김
        descriptionPanel.SetActive(false); // 설명 팝업 초기화 - 숨김

        InitializeCutSceneButtons();
    }
    private void InitializeCutSceneButtons()
    {
        for (int i = 0; i < cutSceneButtons.Length; i++)
        {
            bool isUnlocked = IsButtonUnlocked(i);

            // 스프라이트는 항상 동일하게 유지하고, 잠금 여부는 Button의
            // interactable 상태(Color Tint의 Disabled Color)로만 표현한다.
            cutSceneButtons[i].interactable = isUnlocked;
        }
    }

    private bool IsButtonUnlocked(int buttonIndex)
    {
        if (buttonIndex == 0) return true; // 인트로

        // 해당 스테이지를 클리어(1별 이상)했으면 언락
        return GameManager.Instance.IsStageCleared(buttonIndex - 1);
    }

    public void onGoToCutScene(int stageIndex)
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);

        int buttonIndex = stageIndex + 1;
        if (!IsButtonUnlocked(buttonIndex))
        {
            Debug.Log("해금되지 않은 스테이지입니다.");
            return;
        }

        // 바로 이동하지 않고 설명 팝업을 띄움
        _selectedStageIndex = stageIndex;
        summaryText.text = buttonIndex < summaries.Length ? summaries[buttonIndex] : string.Empty;
        descriptionPanel.SetActive(true);
    }

    // 설명 팝업의 "보기" 버튼 → 선택한 컷씬 재생
    public void onPlaySelectedCutScene()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        GameManager.Instance.currentStageNum = _selectedStageIndex;
        GameManager.Instance.nextSceneAfterCutscene = ReViewSceneName;
        SceneLoader.Load(CutSceneName);
    }

    // 설명 팝업의 "닫기" 버튼
    public void onCloseDescription()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        descriptionPanel.SetActive(false);
    }

    public void onGoToLobby()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        SceneLoader.Load(LobbySceneName);
    }

    public void onSettingButton()
    {
        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        settigPanel.SetActive(!settigPanel.activeSelf);
    }
}
