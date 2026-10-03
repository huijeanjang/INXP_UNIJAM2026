using System.Collections.Generic;
using FlappyBird.Configs;
using FlappyBird.Interfaces.Game;
using FlappyBird.Player;
using UnityEngine;
using Utils;

namespace FlappyBird.Game
{
    /// <summary>
    /// 플래피버드 game flow와 state 전환을 관리합니다.
    /// </summary>
    public class FlappyBirdGameManager : MonoBehaviour, IFlappyBirdGameFlow
    {
        public static FlappyBirdGameManager Instance { get; private set; }

        [Header("설정 및 참조")]
        [SerializeField] private FlappyBirdConfig flappyBirdConfig;
        [SerializeField] private FlappyBirdPlayer player;
        [SerializeField] private PipeSpawner pipeSpawner;
        [SerializeField] private MonoBehaviour startInputSource;

        private readonly FlappyBirdStateMachine _stateMachine = new FlappyBirdStateMachine();
        private readonly List<Item> _collectedItems = new List<Item>();

        private IGameStartInput _startInput;
        private bool _hasStartedGame;

        private int Score { get; set; }
        public bool IsPlaying => _stateMachine.Is(FlappyBirdState.Playing);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _startInput = ResolveStartInput();
        }

        private void OnEnable()
        {
            ResetGameState();

            if (player is not null)
            {
                _stateMachine.Set(FlappyBirdState.Ready);
                player.ResetPlayer();
            }

            if (pipeSpawner != null)
            {
                pipeSpawner.ClearPipes();

                bool preserveSpeed = !(GameSceneManager.Instance != null && GameSceneManager.Instance.IsResetting);
                bool isFirstPlay = !_hasStartedGame || !preserveSpeed;
                pipeSpawner.PreparePipes(preserveSpeed, isFirstPlay);
            }
        }

        private void Start()
        {
            if (player == null || pipeSpawner == null)
            {
                if (GameSceneManager.Instance != null && !GameSceneManager.Instance.IsGameOver)
                {
                    Debug.LogWarning("[FlappyBirdGameManager] 필수 오브젝트 참조가 누락되었습니다. (씬 전환 중일 수 있음)");
                }
                return;
            }

            if (!_stateMachine.Is(FlappyBirdState.Ready))
            {
                _stateMachine.Set(FlappyBirdState.Ready);
                player.ResetPlayer();
            }
        }

        private void Update()
        {
            if (player is null || pipeSpawner is null || _startInput is null)
            {
                return;
            }

            if (player.IsAnimating)
            {
                return;
            }

            if (_stateMachine.Is(FlappyBirdState.Ready) && _startInput.IsStartPressedThisFrame)
            {
                StartGame();
            }
        }

        public void StartGame()
        {
            if (!_stateMachine.TryStart())
            {
                return;
            }

            _hasStartedGame = true;
            player.ActivatePlayer();
            pipeSpawner.StartSpawning();

            Score = 0;
            _collectedItems.Clear();
#if UNITY_EDITOR
            Debug.Log("게임 시작!");
#endif
        }

        public void EndGame()
        {
            if (!_stateMachine.TryEnd())
            {
                return;
            }

            pipeSpawner.StopSpawning();
            pipeSpawner.StopPipeMovement();

            FlappyBirdPlayer resolved = ResolvePlayerForEndGame();
            if (resolved != null)
            {
                resolved.DeactivatePlayer();
            }

#if UNITY_EDITOR
            Debug.Log($"게임 종료! 점수: {Score}, 아이템: {_collectedItems.Count}");
#endif
        }

        private FlappyBirdPlayer ResolvePlayerForEndGame()
        {
            if (player != null)
            {
                return player;
            }

            return FindFirstObjectByType<FlappyBirdPlayer>();
        }

        public void TransitionToNextGame()
        {
            if (!_stateMachine.Is(FlappyBirdState.GameOver))
            {
                return;
            }

            if (GameSceneManager.Instance == null)
            {
                return;
            }

            pipeSpawner.ClearPipes();
            GameSceneManager.Instance.OnChangeGame();
        }

        public void ResetGameState()
        {
            _stateMachine.Set(FlappyBirdState.Ready);

            if (player != null)
            {
                player.CancelDeathAnimation();
            }

            Score = 0;
            _collectedItems.Clear();

            pipeSpawner?.StopSpawning();
            pipeSpawner?.StopPipeMovement();
            pipeSpawner?.ClearPipes();
        }

        public void OnItemCollected(Item item)
        {
            if (!_stateMachine.Is(FlappyBirdState.Playing) || item == null)
            {
                return;
            }

            _collectedItems.Add(item);
            FlappyItemCollector.CollectItem(item);
#if UNITY_EDITOR
            Debug.Log($"아이템 획득: {item.name}");
#endif
        }

        public List<Item> GetCollectedItems()
        {
            List<Item> items = new List<Item>(_collectedItems);
            _collectedItems.Clear();
            return items;
        }

        public void IncrementScore()
        {
            if (!_stateMachine.Is(FlappyBirdState.Playing))
            {
                return;
            }

            Score++;
        }

        private IGameStartInput ResolveStartInput()
        {
            if (startInputSource is IGameStartInput typed)
            {
                return typed;
            }

            PointerGameStartInput fallback = GetComponent<PointerGameStartInput>();
            if (fallback == null)
            {
                fallback = gameObject.AddComponent<PointerGameStartInput>();
            }

            return fallback;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            player = null;
            pipeSpawner = null;
            _startInput = null;
        }
    }
}
