using DG.Tweening;
using FlappyBird.Interfaces.Player;
using UnityEngine;

namespace FlappyBird.Player
{
    /// <summary>
    /// 플레이어 활성화, 리셋, 연출 상태를 관리합니다.
    /// </summary>
    [RequireComponent(typeof(IFlappyBirdPlayerMotor))]
    [RequireComponent(typeof(FlappyBirdPlayerDeathAnimator))]
    [RequireComponent(typeof(FlappyBirdPlayerCollisionHandler))]
    public class FlappyBirdPlayer : MonoBehaviour
    {
        private IFlappyBirdPlayerMotor _motor;
        private IBirdInputSource _input;
        private Rigidbody2D _rb;
        private Collider2D _collider;
        private FlappyBirdPlayerDeathAnimator _deathAnimator;

        private bool _isPlayerActive;
        private bool _isHoldingInput;
        private bool _wasPressedInput;
        private bool _wasReleasedInput;

        public bool IsPlayerActive => _isPlayerActive;
        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<IFlappyBirdPlayerMotor>();
            _input = GetComponent<IBirdInputSource>();
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _deathAnimator = GetComponent<FlappyBirdPlayerDeathAnimator>();
        }

        private void Update()
        {
            if (!_isPlayerActive || _input is null)
            {
                ClearCachedInput();
                return;
            }

            _isHoldingInput = _input.IsHolding;
            _wasPressedInput |= _input.WasPressedThisFrame;
            _wasReleasedInput |= _input.WasReleasedThisFrame;
        }

        private void FixedUpdate()
        {
            if (!_isPlayerActive || _input is null)
            {
                ClearCachedInput();
                return;
            }

            _motor.MotorFixedTick(_isHoldingInput, _wasPressedInput, _wasReleasedInput);
            _wasPressedInput = false;
            _wasReleasedInput = false;
        }

        public void ActivatePlayer()
        {
            _isPlayerActive = true;
            ClearCachedInput();
            // 시작 탭도 첫 날갯짓으로 예약해 Update 실행 순서에 따라 입력이 빠지지 않게 합니다.
            _wasPressedInput = true;

            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            if (_rb is not null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
            }

            _deathAnimator?.Cancel();
            transform.DOKill();
            IsAnimating = false;

            if (GameSceneManager.Instance is not null)
            {
                GameSceneManager.Instance.ResumeGame();
            }
        }

        public void DeactivatePlayer()
        {
            _isPlayerActive = false;
            ClearCachedInput();
        }

        public void ResetPlayer()
        {
            if (_motor == null)
            {
                _motor = GetComponent<IFlappyBirdPlayerMotor>();
            }

            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            if (_rb is not null)
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.linearVelocity = Vector2.zero;
            }

            if (_collider is not null)
            {
                _collider.enabled = true;
            }

            transform.rotation = Quaternion.identity;
            transform.DOKill();

            _motor?.ResetState();
            DeactivatePlayer();

            Vector3 startPos = new Vector3(transform.position.x, 0f, transform.position.z);
            transform.position = startPos + Vector3.down * 6f;

            IsAnimating = true;
            transform
                .DOMove(startPos, 0.4f)
                .SetDelay(1.0f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => IsAnimating = false);
        }

        private void ClearCachedInput()
        {
            _isHoldingInput = false;
            _wasPressedInput = false;
            _wasReleasedInput = false;
        }

        public void PlayDeathAnimation(TweenCallback onComplete)
        {
            _deathAnimator?.Play(onComplete);
        }

        public void CancelDeathAnimation()
        {
            _deathAnimator?.Cancel();
        }
    }
}
