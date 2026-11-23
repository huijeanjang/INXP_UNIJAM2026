using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Utils;
using Core.Input;

public class CutscenePlayer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image itemImage; // 아이템 이미지용 단일 Image 컴포넌트
    [SerializeField] private RectTransform itemBG; // 아이템 컷씬 배경 (활성화는 CutsceneManager 담당)
    [SerializeField] private List<Image> moveImagePool;
    [SerializeField] private List<TMP_Text> dialogueTexts;
    [SerializeField] private Image cursorImage;
    [SerializeField] private Vector2 cursorOffset;
    [SerializeField] private float cursorStartDelay;

    [Header("스킵 버튼 (현재 컷씬 하나를 통째로 건너뜀)")]
    [SerializeField] private Button skipButton;

    private CutsceneFrame[] _frames;
    private int _index;
    private bool _isPlaying;
    private System.Action _onComplete;
    private Sequence _seq;
    private Tween _cursorTween;
    private Tween _bounceTween;
    private Vector2? _itemBGDefaultPos; // 씬에 배치된 ItemBG 기본 위치
    private int _skipFrame = -1; // 스킵한 프레임 번호 (같은 프레임의 탭 입력 무시용)

    private void Awake()
    {
        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);
    }

    public void Play(CutsceneFrame[] frames, System.Action onComplete)
    {
        _frames = frames;
        _index = 0;
        _onComplete = onComplete;
        if (itemBG != null && _itemBGDefaultPos == null)
            _itemBGDefaultPos = itemBG.anchoredPosition;
        //_isPlaying = true;
        _isPlaying = false;
        gameObject.SetActive(true);
        ShowFrame(0);
    }

    private void Update()
    {
        if (!_isPlaying) return;
        if (Time.frameCount == _skipFrame) return; // 스킵 직후 같은 프레임 입력이 다음 컷씬으로 넘어가지 않게

        if (UnifiedInputManager.Instance.WasTappedThisFrame && !IsPointerOverSkipButton())
            NextFrame();
        /*if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            NextFrame();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            NextFrame();*/
    }

    private void NextFrame()
    {
        if (!_isPlaying) return;
        _isPlaying = false; // 입력 차단

        HideCursor();
        _seq?.Kill();
        _seq = null;

        _index++;
        if (_index >= _frames.Length) { EndCutscene(); return; }
        ShowFrame(_index);

        //_isPlaying = true; // 다음 프레임이 보여지면 입력 허용 (트윈이 있으면 트윈 완료 후 허용하도록 변경)
    }
    private void ShowFrame(int index)
    {
        var frame = _frames[index];
        HideCursor();
        _seq?.Kill();

        // 효과음 (체크된 프레임에서만, BGM은 건드리지 않음)
        if (frame.sfx != null && frame.sfx.playSFX)
        {
            GameManager.Instance.soundManager.PlaySFX(frame.sfx.sfx);
            CustomLog.Info($"효과음 재생 - index:{index} sfx:{frame.sfx.sfx}");
        }

        // 아이템 이미지
        if (frame.itemImage != null && frame.itemImage.sprite != null)
        {
            itemImage.gameObject.SetActive(true);
            itemImage.sprite = frame.itemImage.sprite;
            itemImage.SetNativeSize(); // 원본 스프라이트 크기 그대로 적용
            itemImage.color = frame.itemImage.fadeSettings.useFade ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }
        else
        {
            itemImage.gameObject.SetActive(false);
        }

        // 아이템 배경 위치 (지정 안 하면 기본 위치로 복원)
        if (itemBG != null && _itemBGDefaultPos.HasValue)
        {
            itemBG.anchoredPosition = frame.itemImage != null && frame.itemImage.useCustomBGPos
                ? frame.itemImage.bgPos
                : _itemBGDefaultPos.Value;
        }

        // 이미지 전부 비활성화
        foreach (var img in moveImagePool)
            img.gameObject.SetActive(false);

        // 위치·스프라이트 세팅
        for (int i = 0; i < frame.moveImages?.Count; i++)
        {
            if (i >= moveImagePool.Count) break;
            var data = frame.moveImages[i];
            var img = moveImagePool[i];

            img.gameObject.SetActive(true);
            img.sprite = data.sprite;
            img.rectTransform.anchoredPosition = data.startPos; // 위치
            img.rectTransform.sizeDelta = data.size; // 크기
            img.color = data.fadeSettings.useFade ? new Color(1f, 1f, 1f, 0f) : Color.white;
        }

        // 텍스트 세팅
        for (int i = 0; i < dialogueTexts.Count; i++)
        {
            if (i < frame.Texts.Count)
            {
                var textData = frame.Texts[i];
                dialogueTexts[i].text = textData.dialogueText;
                dialogueTexts[i].rectTransform.anchoredPosition = textData.textPos; // 위치 먼저
                dialogueTexts[i].alignment = textData.textAlignment;
                dialogueTexts[i].color = textData.fadeSettings.useFade ? new Color(52 / 255f, 63 / 255f, 63 / 255f, 0f) : Color.white; // 페이드 여부에 따라 초기 투명도 설정
            }
            else
            {
                dialogueTexts[i].text = string.Empty;
                dialogueTexts[i].color = Color.white;
            }
        }
        bool hasTween = false;
        _seq = DOTween.Sequence();
        // 이미지 트윈
        for (int i = 0; i < frame.moveImages?.Count; i++)
        {
            if (i >= moveImagePool.Count) break;
            var data = frame.moveImages[i];
            var img = moveImagePool[i];

            if (data.duration > 0f)
            {
                _seq.Join(img.rectTransform.DOAnchorPos(data.endPos, data.duration).SetEase(data.ease));
                hasTween = true;
                CustomLog.Info($"이동 트윈 추가 - index:{index} i:{i} duration:{data.duration}");
            }

            if (data.fadeSettings.useFade)
            {
                if (data.fadeSettings.fadeInDuration > 0f)
                {
                    img.color = new Color(1f, 1f, 1f, 0f);
                    _seq.Insert(data.fadeSettings.startDelay,
                        img.DOFade(1f, data.fadeSettings.fadeInDuration));
                    hasTween = true;
                    CustomLog.Info($"이미지 페이드인 추가 - index:{index} i:{i} fadeIn:{data.fadeSettings.fadeInDuration}");
                }
                else
                {
                    img.color = Color.white;
                }

                if (data.fadeSettings.fadeOutDuration > 0f)
                {
                    float outStart = data.fadeSettings.startDelay + data.fadeSettings.fadeInDuration;
                    _seq.Insert(outStart, img.DOFade(0f, data.fadeSettings.fadeOutDuration));
                    hasTween = true;
                    CustomLog.Info($"이미지 페이드아웃 추가 - outStart:{outStart} fadeOutDuration:{data.fadeSettings.fadeOutDuration} SeqDuration:{_seq.Duration()}");
                }
            }
        }

        // 텍스트 트윈
        for (int i = 0; i < frame.Texts?.Count; i++)
        {
            if (i >= dialogueTexts.Count) break;
            var textData = frame.Texts[i];

            if (textData.fadeSettings.useFade)
            {
                if (textData.fadeSettings.fadeInDuration > 0f)
                {
                    dialogueTexts[i].color = new Color(52 / 255f, 63 / 255f, 63 / 255f, 0f);
                    _seq.Insert(textData.fadeSettings.startDelay,
                        dialogueTexts[i].DOFade(1f, textData.fadeSettings.fadeInDuration));
                    hasTween = true;
                    CustomLog.Info($"텍스트 페이드인 추가 - index:{index} i:{i}");
                }
                else
                {
                    dialogueTexts[i].color = Color.white;
                }

                if (textData.fadeSettings.fadeOutDuration > 0f)
                {
                    float outStart = textData.fadeSettings.startDelay + textData.fadeSettings.fadeInDuration;
                    _seq.Insert(outStart, dialogueTexts[i].DOFade(0f, textData.fadeSettings.fadeOutDuration));
                    hasTween = true;
                    CustomLog.Info($"텍스트 페이드아웃 추가 - outStart:{outStart} fadeOutDuration:{textData.fadeSettings.fadeOutDuration} SeqDuration:{_seq.Duration()}");
                }
            }
        }

        _seq.OnKill(() => CustomLog.Info($"Kill - index:{index} hasTween:{hasTween}"));

        if (!hasTween)
        {
            CustomLog.Info($"빈 Sequence - index:{index}, Kill 처리");
            _seq.Kill();
            _seq = null;
            _isPlaying = true; // 트윈 없으면 즉시 입력 허용
            ShowCursor();
        }
        else
        {
            _seq.OnComplete(() => { _isPlaying = true; ShowCursor(); }); // 트윈 완료 후 입력 허용
        }
    }

    private void ShowCursor()
    {
        if (cursorImage == null) return;

        TMP_Text lastText = null;
        for (int i = dialogueTexts.Count - 1; i >= 0; i--)
        {
            if (dialogueTexts[i].text != string.Empty) { lastText = dialogueTexts[i]; break; }
        }

        if (lastText == null) return;

        lastText.ForceMeshUpdate();
        var info = lastText.textInfo;
        if (info.characterCount == 0) return;

        int last = info.characterCount - 1;
        while (last > 0 && !info.characterInfo[last].isVisible) last--;
        var ci = info.characterInfo[last];
        var li = info.lineInfo[ci.lineNumber];

        float localX = ci.xAdvance;
        float localY = (li.ascender + li.descender) * 0.5f;
        Vector3 world = lastText.rectTransform.TransformPoint(new Vector3(localX, localY, 0f));
        Vector3 p = cursorImage.rectTransform.parent.InverseTransformPoint(world);
        cursorImage.rectTransform.localPosition = new Vector3(p.x + cursorOffset.x, p.y + cursorOffset.y, 0f);

        var c = cursorImage.color;
        cursorImage.color = new Color(c.r, c.g, c.b, 0f);
        cursorImage.gameObject.SetActive(true);

        float baseY = cursorImage.rectTransform.localPosition.y;
        _cursorTween?.Kill();
        _bounceTween?.Kill();

        _cursorTween = cursorImage.DOFade(1f, 0.2f)
            .SetDelay(cursorStartDelay)
            .OnComplete(() =>
            {
                _bounceTween = cursorImage.rectTransform
                    .DOLocalMoveY(baseY - 4f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            });
    }

    private void HideCursor()
    {
        _cursorTween?.Kill();
        _bounceTween?.Kill();
        _cursorTween = null;
        _bounceTween = null;
        if (cursorImage != null) cursorImage.gameObject.SetActive(false);
    }

    // 스킵 버튼: 현재 컷씬을 즉시 종료 → onComplete 콜백으로 다음 컷씬(있으면) 재생, 없으면 컷씬 종료
    public void Skip()
    {
        if (_frames == null || !gameObject.activeInHierarchy) return; // 이미 끝났거나 재생 중이 아니면 무시 (중복 클릭 방지)

        GameManager.Instance.soundManager.PlaySFX(SoundManager.SFX.ButtonClick);
        CustomLog.Info($"컷씬 스킵 - index:{_index}");
        _skipFrame = Time.frameCount;
        EndCutscene();
    }

    // 스킵 버튼을 누르는 탭이 대사 넘기기로도 처리되지 않게 체크
    private bool IsPointerOverSkipButton()
    {
        if (skipButton == null || !skipButton.gameObject.activeInHierarchy) return false;

        var canvas = skipButton.GetComponentInParent<Canvas>();
        Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)skipButton.transform, UnifiedInputManager.Instance.PointerPosition, cam);
    }

    private void EndCutscene()
    {
        _isPlaying = false;
        HideCursor();
        _seq?.Kill();
        foreach (var img in moveImagePool)
            img.gameObject.SetActive(false);
        _frames = null; // 종료 상태 표시 (Skip 중복 호출 방지)
        gameObject.SetActive(false);

        var onComplete = _onComplete;
        _onComplete = null; // 콜백은 한 번만 호출
        onComplete?.Invoke(); // 다음 컷씬이 있으면 여기서 Play()가 다시 호출됨
    }
}