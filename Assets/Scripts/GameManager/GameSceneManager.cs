using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Utils;
using DG.Tweening;
using UI;
using UnityEngine.Rendering.Universal;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [SerializeField] private TransitionVisuals transitionVisuals;

    [Header("게임 전환시 입력 차단")]
    [SerializeField] private GameObject transitionInputBlocker;
    [SerializeField] private float transitionGlitchInputBlockFallbackSeconds = 0.5f;

    private GameObject _runtimeTransitionInputBlocker;

    [Header("GamePrefabs")] // 각 게임 전체 화면 프리팹, 연결 필수!!
    public GameObject PresentGamePrefab; // 현재 (3매치) 게임 프리팹

    public GameObject PastGamePrefab; // 과거 (스테이지별 상이) 게임 프리팹

    public GameObject PresentUIPrefab; // 현재 게임 UI 프리팹

    public GameObject PastUIPrefab; // 과거 게임 UI 프리팹

    [Header("Runtime Slot Roots")]
    [SerializeField] private Transform presentGameRoot;
    [SerializeField] private Transform pastGameRoot;
    [SerializeField] private Transform presentUIRoot;
    [SerializeField] private Transform pastUIRoot;
    [SerializeField] private Camera mainGameplayCamera;
    [SerializeField] private Camera uiPostProcessingCamera;


    [Header("Game object")]
    public Image gameTimer; // 타이머 연결
    public List<Image> gameTimers = new List<Image>(); // 모든 타이머 UI 이미지 연결
    [SerializeField] private TextMeshProUGUI penaltyText; // 페널티 텍스트

    public TextMeshProUGUI gameScore; // 게임 스코어 출력
    [SerializeField] private NumberCounter scoreCounter;
    // public GameObject gameChangeButton; // gameChangeButton (단일 연결)


    [Header("Game State")]
    private List<int> collectedItems = new List<int>(); // 수집한 아이템 id를 차례로 저장

    public IReadOnlyList<int> CollectedItems => collectedItems;
    public int CurrentScore { get; private set; } // 현재 점수
    [SerializeField] private float currentTime; // 남은 시간 카운트
    public float CurrentTime => currentTime;


    private bool isGameOver = false; // 게임 오버 여부
    public bool IsGameOver => isGameOver;
    // 목표 점수 달성 후 연출이 끝날 때까지 클리어 UI를 미룸
    private bool _isClearPending;
    public bool IsClearPending => _isClearPending;
    private int _scorePresentationLocks;
    private bool isPaused = false; // 게임 일시정지 여부
    public bool IsPaused => isPaused;
    // 튜토리얼/시작 대기 팝업이 떠 있는 동안 타이머만 멈추기 위한 별도 플래그.
    // ChangeGameRoutine이 트랜지션 종료 시 isPaused를 무조건 false로 되돌리기 때문에,
    // isPaused를 그대로 재사용하면 팝업이 떠 있는 도중에도 풀려버려서 별도로 둔다.
    private bool _isInputGateActive = false;
    public bool IsInputGateActive => _isInputGateActive;
    private int currentGameId = 0; // 현재 게임 ID (0: 현재, 1: 과거)
    public int CurrentGameId => currentGameId;
    // private int currentdeathCount = 0; // 현재 죽음 횟수
    private bool _isTransitioning = false;
    public bool IsTransitioning => _isTransitioning;
    public bool IsResetting { get; private set; } = false;
    private Vector3 _penaltyTextOriginPos;
    private StageRuntimeConfiguration _currentStageConfiguration;
    private GameObject _runtimePastGame;
    private GameObject _runtimePastUI;
    private RankModePastStagePicker _rankStagePicker;

    public event System.Action OnGameChanged;

    //public event System.Action OnPausePanelOpened;
    public event System.Action OnGameOver;

    [SerializeField] private float gameTimeLimit = 60f; // 게임 제한 시간
    [SerializeField] private float rankModeGameTimeLimit = 180f; // 랭크모드 게임 제한 시간
    public float RankModeGameTimeLimit => rankModeGameTimeLimit;


    private enum GamePrefabState // 현재 게임 상태
    {
        Present,
        Past
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }
    void Start()
    {
        StartCoroutine(StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        while (GameManager.Instance == null)
        {
            yield return null;
        }

        InitializeStageObjects();
        InjectCanvasCamera(PresentGamePrefab);
        InjectCanvasCamera(PresentUIPrefab);
        if (gameTimer != null && !gameTimers.Contains(gameTimer))
        {
            gameTimers.Add(gameTimer);
        }

        if (penaltyText != null)
        {
            _penaltyTextOriginPos = penaltyText.transform.localPosition;
            penaltyText.gameObject.SetActive(false);
        }

        ResetGame();
    }
    public void ResetGame() // 새 게임 시작 시 필요
    {
        IsResetting = true;
        gameTimers.RemoveAll(timer => timer == null);
        CurrentScore = 0;
        if (gameScore != null)
        {
            gameScore.text = CurrentScore.ToString();
        }
        FlappyItemCollector.ClearItems();

        if (scoreCounter != null)
        {
            scoreCounter.SetValue(0);
        }
        else
        {
            // NumberCounter가 없을 경우를 대비한 예외 처리
            if (gameScore != null) gameScore.text = "0";
        }

        if (GameManager.Instance != null && GameManager.Instance.IsRankMode)
        {
            gameTimeLimit = rankModeGameTimeLimit;
        }

        currentTime = gameTimeLimit;
        collectedItems.Clear();
        isGameOver = false;
        _isClearPending = false;
        _scorePresentationLocks = 0;
        isPaused = false;
        _isTransitioning = false;
        SetPresentObjectsActive(false);
        SetPastObjectsActive(false);
        currentGameId = 0; // 기본 현재 게임 설정

        // 애니팡 큐 초기화
        if (ItemQueueManager.Instance != null)
        {
            ItemQueueManager.Instance.ClearQueue();
            GameManager.Instance.GameData.itemQueue = new ItemQueue();
        }

        // FlappyBirdGameManager 상태 초기화
        if (FlappyBird.Game.FlappyBirdGameManager.Instance != null)
        {
            FlappyBird.Game.FlappyBirdGameManager.Instance.ResetGameState();
        }
        bool hasPastRuntimeContent = GetPastGameObject() != null || GetPastUIObject() != null;
        if (hasPastRuntimeContent)
        {
            OnChangeGame();
        }
        else
        {
            SetPresentObjectsActive(true);
            SetPastObjectsActive(false);
            EnsurePresentChangeButtonVisible();
            EnsurePresentStartGateVisible();
            currentGameId = 0;

            if (GameManager.Instance != null && GameManager.Instance.soundManager != null)
            {
                GameManager.Instance.soundManager.PlayBGM(GetPresentBgm());
            }
        }

        currentTime += 5f; // 게임 리셋 한정 시간 패널티 무효

        float fill = (gameTimeLimit > 0f) ? (currentTime / gameTimeLimit) : 0f;
        foreach (var timer in gameTimers)
        {
            if (timer != null) timer.fillAmount = Mathf.Clamp01(fill);
        }

        // GameManager.Instance.soundManager.PlayBGM(SoundManager.BGM.FlappyBird); // 플래피버드 BGM 재생

    }
    public void SetInputGateActive(bool active)
    {
        _isInputGateActive = active;
    }

    void Update()
    {
        if (isGameOver) return;
        if (_isClearPending) return;
        if (isPaused) return;
        if (_isInputGateActive) return;
        if (IsCurrentPastGameWaitingToStart()) return;

        currentTime -= Time.deltaTime; // 시간 감소

        float fill = (gameTimeLimit > 0f) ? (currentTime / gameTimeLimit) : 0f;
        foreach (var timer in gameTimers)
        {
            if (timer != null) timer.fillAmount = Mathf.Clamp01(fill);
        }

        if (currentTime <= 0)
        {
            currentTime = 0;
            TriggerGameOver();
        }

    }

    void OnDestroy()
    {
        if (_runtimeTransitionInputBlocker != null)
        {
            Destroy(_runtimeTransitionInputBlocker);
            _runtimeTransitionInputBlocker = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AddItem(int itemId) // 아이템 id 매개변수로 받아 리스트에 추가
    {
        if (isGameOver) return;
        if (isPaused) return;
        collectedItems.Add(itemId);
    }

    public void ClearItem() // 아이템 전체 삭제
    {
        if (isGameOver) return;
        if (isPaused) return;
        collectedItems.Clear();
    }

    public void AddScore(int score, bool forceAddScore = false) // 점수 추가
    {
        if (isGameOver) return;
        if (isPaused && !forceAddScore) return;

        int previousScore = CurrentScore; // [추가] 오르기 전 점수 저장
        CurrentScore += score;            // 실제 데이터 갱신

        // gameScore.text = CurrentScore.ToString();

        if (scoreCounter)
        {
            // 이전 점수부터 현재 점수까지 0.5초 동안 올라가라
            DOVirtual.DelayedCall(0.5f, () =>
            {
                scoreCounter.PlayCountAnimation(previousScore, CurrentScore, 0.5f);
            });
        }
        else if (gameScore)
        {
            // Counter가 연결 안 되어 있으면 그냥 텍스트 갱신
            gameScore.text = CurrentScore.ToString("N0");
        }

        TryClearByTargetScore();
    }

    // 자동 pop/매치 연쇄 연출이 시작됨
    public void BeginScorePresentation()
    {
        _scorePresentationLocks++;
    }

    // 연출이 끝나면 대기 중이던 클리어를 확정
    public void EndScorePresentation()
    {
        if (_scorePresentationLocks > 0)
        {
            _scorePresentationLocks--;
        }

        TryFinishPendingClear();
    }

    // 목표 점수 달성 시 클리어를 예약하고, 연출이 없으면 즉시 종료
    private void TryClearByTargetScore()
    {
        if (isGameOver || _isClearPending) return;
        if (GameManager.Instance == null || GameManager.Instance.IsRankMode) return;

        int stageIndex = GameManager.Instance.currentStageNum;
        List<int> criteria = GameManager.Instance.GameData != null
            ? GameManager.Instance.GameData.stageClearCriteria
            : null;
        if (criteria == null || stageIndex < 0 || stageIndex >= criteria.Count) return;

        if (CurrentScore >= criteria[stageIndex])
        {
            _isClearPending = true;
            TryFinishPendingClear();
        }
    }

    private void TryFinishPendingClear()
    {
        if (!_isClearPending || isGameOver) return;
        if (_scorePresentationLocks > 0) return;

        TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        _isClearPending = false;
        OnGameOver?.Invoke();
    }

    /// <summary>
    /// 테스트용: 현재 점수를 지정한 값으로 덮어씁니다. UI도 즉시 갱신합니다.
    /// </summary>
    public void SetScore(int score)
    {
        CurrentScore = Mathf.Max(0, score);

        if (scoreCounter != null)
            scoreCounter.SetValue(CurrentScore);
        if (gameScore != null)
            gameScore.text = CurrentScore.ToString("N0");
    }

    /// <summary>
    /// 게임 전환을 요청하는 메서드입니다. 실제 로직은 코루틴에서 수행합니다.
    /// </summary>
    public void OnChangeGame()
    {
        if (isGameOver || _isClearPending) return;
        if (_isTransitioning) return; // 이미 전환 중이면 무시

        StartCoroutine(ChangeGameRoutine());
    }

    public void ResumeGame()
    {
        isPaused = false;
    }

    private GameObject GetOrCreateTransitionInputBlocker()
    {
        if (transitionInputBlocker != null)
            return transitionInputBlocker;
        if (_runtimeTransitionInputBlocker != null)
            return _runtimeTransitionInputBlocker;

        var root = new GameObject("TransitionInputBlocker", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.overrideSorting = true;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGo = new GameObject("RaycastCatcher", typeof(RectTransform));
        imgGo.transform.SetParent(root.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = imgGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        root.SetActive(false);
        _runtimeTransitionInputBlocker = root;
        return root;
    }

    private void SetTransitionInputBlocked(bool blocked)
    {
        var blocker = GetOrCreateTransitionInputBlocker();
        if (blocker != null)
            blocker.SetActive(blocked);
    }

    /// <summary>
    /// 시각적 연출(Glitch/Fade)과 함께 게임 오브젝트를 교체하는 코루틴입니다.
    /// </summary>
    private IEnumerator ChangeGameRoutine()
    {
        _isTransitioning = true;
        isPaused = true; // 게임 로직(타이머 등) 일시 정지

        // -----------------------------------------------------------
        // 0. (AniPang -> FlappyBird) 페널티 연출 (전환 전)
        // -----------------------------------------------------------
        if (currentGameId == 0 && !IsResetting) // Present -> Past (초기화 중일 땐 스킵)
        {
            if (penaltyText != null)
            {
                penaltyText.gameObject.SetActive(true);
                penaltyText.text = "-5s";
                penaltyText.transform.localPosition = _penaltyTextOriginPos;
                penaltyText.alpha = 1f;

                penaltyText.transform.DOLocalMoveY(_penaltyTextOriginPos.y + 100f, 0.8f).SetEase(Ease.OutQuad);
                penaltyText.DOFade(0f, 0.8f).SetEase(Ease.InQuad);
            }

            if (gameTimers != null && gameTimers.Count > 0)
            {
                float targetTime = Mathf.Max(0, currentTime - 5f);
                float targetFill = (gameTimeLimit > 0f) ? (targetTime / gameTimeLimit) : 0f;

                Sequence penaltySeq = DOTween.Sequence();
                foreach (var timer in gameTimers)
                {
                    if (timer != null) penaltySeq.Join(timer.DOFillAmount(targetFill, 0.8f));
                }
                yield return penaltySeq.WaitForCompletion();
            }
            else
            {
                yield return new WaitForSeconds(0.8f);
            }

            currentTime -= 5f;
        }

        OnGameChanged?.Invoke();

        // -----------------------------------------------------------
        // 1. 전환 시작 연출 (화면 암전 + 글리치 시작)
        // -----------------------------------------------------------
        if (transitionVisuals is not null)
        {
            // 시작할 때는 Volume을 켜고 -> 글리치를 실행
            // (필요하다면 여기도 Sequence로 묶을 수 있습니다)
            transitionVisuals.FadePastVolumeWeight(1f);
            transitionVisuals.FadePresentVolumeWeight(0f);

            SetTransitionInputBlocked(true);
            Tween startTween = transitionVisuals.PlayStartMixedGlitch(0.5f, 1.0f);
            if (startTween != null) yield return startTween.WaitForCompletion();
            SetTransitionInputBlocked(false);
        }
        else
        {
            float t = Mathf.Max(0f, transitionGlitchInputBlockFallbackSeconds);
            if (t > 0f)
            {
                SetTransitionInputBlocked(true);
                yield return new WaitForSecondsRealtime(t);
                SetTransitionInputBlocked(false);
            }
        }

        // -----------------------------------------------------------
        // 2. 게임 오브젝트(프리팹) 교체 로직
        // -----------------------------------------------------------
        if (currentGameId == 1) // Past -> Present으로 전환
        {
            SetPresentObjectsActive(true);
            SetPastObjectsActive(false);
            EnsurePresentChangeButtonVisible();
            EnsurePresentStartGateVisible();

            currentGameId = 0;

            GameManager.Instance.soundManager.PlayBGM(GetPresentBgm());
        }
        else if (currentGameId == 0) // Present -> Past로 전환
        {
            SetPresentObjectsActive(false);

            if (GameManager.Instance != null && GameManager.Instance.IsRankMode)
            {
                SwapRankModePastStage();
            }

            SetPastObjectsActive(true);


            currentGameId = 1;

            // currentTime -= 5f; // 페널티 이미 적용됨
            GameManager.Instance.soundManager.PlayBGM(GetPastBgm());
        }

        // 잠시 대기
        yield return null;

        // -----------------------------------------------------------
        // 3. 전환 종료 연출 (수정된 부분: 동시 실행 로직 적용)
        // -----------------------------------------------------------
        if (transitionVisuals is not null)
        {
            SetTransitionInputBlocked(true);
            // 새로운 시퀀스를 생성하여 애니메이션들을 병렬(Join)로 연결합니다.
            Sequence endSequence = DOTween.Sequence();

            // A. 글리치 효과 종료 애니메이션 추가
            endSequence.Join(transitionVisuals.PlayEndMixedGlitch());

            // B. '애니팡(0)'으로 왔을 때만 Volume 비활성화 애니메이션을 '동시에' 추가
            if (currentGameId == 0)
            {
                // 앞서 수정한 TransitionVisuals 덕분에 SetVolumeActive가 Tween을 반환하므로 Join 가능
                endSequence.Join(transitionVisuals.FadePastVolumeWeight(0f));
                endSequence.Join(transitionVisuals.FadePresentVolumeWeight(1f));

            }

            // 두 애니메이션이 동시에 진행되고 끝날 때까지 대기
            yield return endSequence.WaitForCompletion();
            SetTransitionInputBlocked(false);
        }
        else
        {
            float t = Mathf.Max(0f, transitionGlitchInputBlockFallbackSeconds);
            if (t > 0f)
            {
                SetTransitionInputBlocked(true);
                yield return new WaitForSecondsRealtime(t);
                SetTransitionInputBlocked(false);
            }
        }

        IsResetting = false;
        _isTransitioning = false;

        if (!isGameOver)
        {
            isPaused = false;
        }

        if (penaltyText != null)
        {
            penaltyText.DOKill();
            penaltyText.gameObject.SetActive(false);
            penaltyText.alpha = 0f;
            penaltyText.transform.localPosition = _penaltyTextOriginPos;
        }
    }

    private void InitializeStageObjects()
    {
        bool isRankMode = GameManager.Instance != null && GameManager.Instance.IsRankMode;

        if (isRankMode)
        {
            List<StageRuntimeConfiguration> rankPool = GameManager.Instance.GetRankModeStagePool();
            _rankStagePicker = new RankModePastStagePicker(rankPool);
            _currentStageConfiguration = _rankStagePicker.Pick();
        }
        else
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EnsureValidCurrentStage();
            }

            _currentStageConfiguration = GameManager.Instance != null
                ? GameManager.Instance.GetCurrentStageConfiguration()
                : null;
        }

        if (PresentGamePrefab != null) PresentGamePrefab.SetActive(false);
        if (PastGamePrefab != null) PastGamePrefab.SetActive(false);
        if (PresentUIPrefab != null) PresentUIPrefab.SetActive(false);
        if (PastUIPrefab != null) PastUIPrefab.SetActive(false);

        CleanupRuntimeObjects();

        EnsureCameraSetup();
        InjectCanvasCamera(PresentGamePrefab);
        InjectCanvasCamera(PresentUIPrefab);

        if (_currentStageConfiguration == null)
        {
            Debug.LogWarning("[GameSceneManager] Stage configuration was not found. Falling back to scene default past prefabs.");
            InjectCanvasCamera(PastGamePrefab);
            InjectCanvasCamera(PastUIPrefab);
            return;
        }

        _runtimePastGame = InstantiateStageObject(_currentStageConfiguration.pastGamePrefab, pastGameRoot, "PastGame");
       
        _runtimePastUI = InstantiateStageObject(_currentStageConfiguration.pastUIPrefab, pastUIRoot, "PastUI", optional: true);

        InjectCanvasCamera(_runtimePastGame);
        InjectCanvasCamera(_runtimePastUI);
    }

    /// <summary>
    /// 랭크모드에서 Present -> Past 전환 시 Past 프리팹을 다음 후보로 교체합니다 (Instantiate/Destroy 방식).
    /// </summary>
    private void SwapRankModePastStage()
    {
        if (_rankStagePicker == null)
        {
            return;
        }

        StageRuntimeConfiguration nextConfiguration = _rankStagePicker.Pick();

        DestroyRuntimeObject(_runtimePastGame);
        DestroyRuntimeObject(_runtimePastUI);

        _runtimePastGame = InstantiateStageObject(nextConfiguration?.pastGamePrefab, pastGameRoot, "PastGame");
        _runtimePastUI = InstantiateStageObject(nextConfiguration?.pastUIPrefab, pastUIRoot, "PastUI", optional: true);

        InjectCanvasCamera(_runtimePastGame);
        InjectCanvasCamera(_runtimePastUI);

        _currentStageConfiguration = nextConfiguration; // GetPastBgm()이 이 값을 참조하므로 BGM 재생 전에 갱신
    }

    private GameObject InstantiateStageObject(GameObject prefab, Transform parent, string label, bool optional = false)
    {
        if (prefab == null)
        {
            if (!optional)
            {
                Debug.LogWarning($"[GameSceneManager] {label} prefab is not assigned for stage {GameManager.Instance?.currentStageNum}.");
            }
            return null;
        }

        GameObject instance = parent != null
            ? Instantiate(prefab, parent)
            : Instantiate(prefab);

        instance.name = $"{prefab.name}_{label}";
        instance.SetActive(false);
        return instance;
    }

    private void CleanupRuntimeObjects()
    {
        DestroyRuntimeObject(_runtimePastGame);
        DestroyRuntimeObject(_runtimePastUI);

        _runtimePastGame = null;
        _runtimePastUI = null;
    }

    private void DestroyRuntimeObject(GameObject target)
    {
        if (target != null)
        {
            Destroy(target);
        }
    }

    private GameObject GetPresentGameObject()
    {
        return PresentGamePrefab;
    }

    private GameObject GetPastGameObject()
    {
        return _runtimePastGame != null ? _runtimePastGame : PastGamePrefab;
    }

    private GameObject GetPresentUIObject()
    {
        return PresentUIPrefab;
    }

    private GameObject GetPastUIObject()
    {
        return _runtimePastUI != null ? _runtimePastUI : PastUIPrefab;
    }

    private void SetPresentObjectsActive(bool isActive)
    {
        GameObject presentGame = GetPresentGameObject();
        GameObject presentUI = GetPresentUIObject();

        if (presentGame != null)
        {
            presentGame.SetActive(isActive);
        }

        if (presentUI != null)
        {
            presentUI.SetActive(isActive);
        }
    }

    private void SetPastObjectsActive(bool isActive)
    {
        GameObject pastGame = GetPastGameObject();
        GameObject pastUI = GetPastUIObject();

        if (pastGame != null)
        {
            pastGame.SetActive(isActive);
        }

        if (pastUI != null)
        {
            pastUI.SetActive(isActive);
        }
    }

    private SoundManager.BGM GetPresentBgm()
    {
        return SoundManager.BGM.Anipang;
    }

    private SoundManager.BGM GetPastBgm()
    {
        return _currentStageConfiguration != null
            ? _currentStageConfiguration.pastBgm
            : SoundManager.BGM.FlappyBird;
    }

    private void EnsureCameraSetup()
    {
        Camera baseCamera = ResolveMainGameplayCamera();
        Camera overlayCamera = ResolveUIPostProcessingCamera();

        if (baseCamera == null || overlayCamera == null)
        {
            return;
        }

        if (!overlayCamera.gameObject.activeSelf)
        {
            overlayCamera.gameObject.SetActive(true);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            overlayCamera.cullingMask = 1 << uiLayer;
        }

        AudioListener overlayListener = overlayCamera.GetComponent<AudioListener>();
        if (overlayListener != null)
        {
            overlayListener.enabled = false;
        }

        UniversalAdditionalCameraData baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
        UniversalAdditionalCameraData overlayCameraData = overlayCamera.GetComponent<UniversalAdditionalCameraData>();

        if (baseCameraData == null || overlayCameraData == null)
        {
            return;
        }

        overlayCameraData.renderType = CameraRenderType.Overlay;

        if (!baseCameraData.cameraStack.Contains(overlayCamera))
        {
            baseCameraData.cameraStack.Add(overlayCamera);
        }
    }

    private void InjectCanvasCamera(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
        if (canvases == null || canvases.Length == 0)
        {
            return;
        }

        Camera targetCamera = ResolveUIPostProcessingCamera();
        if (targetCamera == null)
        {
            return;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = 100f;
            }

            canvas.worldCamera = targetCamera;
        }
    }

    private Camera ResolveMainGameplayCamera()
    {
        if (mainGameplayCamera != null)
        {
            return mainGameplayCamera;
        }

        mainGameplayCamera = Camera.main;
        return mainGameplayCamera;
    }

    private Camera ResolveUIPostProcessingCamera()
    {
        if (uiPostProcessingCamera != null)
        {
            return uiPostProcessingCamera;
        }

        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            if (camera.name != "UIPostProcessingCamera")
            {
                continue;
            }

            if (!camera.gameObject.scene.IsValid())
            {
                continue;
            }

            uiPostProcessingCamera = camera;
            return uiPostProcessingCamera;
        }

        uiPostProcessingCamera = ResolveMainGameplayCamera();
        return uiPostProcessingCamera;
    }

    public void OnApplicationPause(bool pause)
    {
        //if (pause) OnPausePanelOpened?.Invoke();
        isPaused = pause;
    }

    private void EnsurePresentChangeButtonVisible()
    {
        GameObject presentUI = GetPresentUIObject();
        if (presentUI == null) return;

        Transform[] children = presentUI.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == "ChangeGameButton")
            {
                child.gameObject.SetActive(true);
            }
        }
    }

    private void EnsurePresentStartGateVisible()
    {
        GameObject presentUI = GetPresentUIObject();
        if (presentUI == null) return;

        PresentGameTutorialAlarm[] alarms = presentUI.GetComponentsInChildren<PresentGameTutorialAlarm>(true);
        for (int i = 0; i < alarms.Length; i++)
        {
            PresentGameTutorialAlarm alarm = alarms[i];
            if (alarm != null)
            {
                alarm.gameObject.SetActive(true);
            }
        }
    }

    private bool IsCurrentPastGameWaitingToStart()
    {
        if (currentGameId != 1)
        {
            return false;
        }

        FlappyBird.Game.FlappyBirdGameManager flappyBirdGameManager =
            FlappyBird.Game.FlappyBirdGameManager.Instance;
        return flappyBirdGameManager != null && !flappyBirdGameManager.IsPlaying;
    }


#if UNITY_EDITOR

    [ContextMenu("Debug: CurrentScore log")]
    void TestAddScore()
    {
        CustomLog.Info($"Current Score: {CurrentScore}");
    }

    [ContextMenu("Test: Trigger Game Over")]
    void TestGameOver()
    {
        OnGameOver();
    }

    [ContextMenu("Debug: Print Current State")]
    void DebugPrintState()
    {
        CustomLog.Info($"Score: {CurrentScore}, Time: {CurrentTime:F1}, GameOver: {isGameOver}, Paused: {isPaused}");
    }

    [ContextMenu("Debug: Print Current Items")]
    void DebugPrintItems()
    {
        CustomLog.Info($"Collected Items: {string.Join(", ", CollectedItems)}");
    }
#endif

}

