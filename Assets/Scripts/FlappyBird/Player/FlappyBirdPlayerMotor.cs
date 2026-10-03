using FlappyBird.Configs;
using FlappyBird.Interfaces.Player;
using UnityEngine;

namespace FlappyBird.Player
{
    /// <summary>
    /// 탭마다 일정한 상승 속도를 적용하고 중력으로 포물선 비행을 만듭니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class FlappyBirdPlayerMotor : MonoBehaviour, IFlappyBirdPlayerMotor
    {
        [SerializeField] private FlappyBirdConfig flappyBirdConfig;

        private Rigidbody2D _rigidBody2D;
        private Vector2 _startPosition;
        private bool _initialized;

        private void Awake()
        {
            _rigidBody2D = GetComponent<Rigidbody2D>();
            _startPosition = transform.position;
            _initialized = true;
        }

        public void MotorFixedTick(bool isHolding, bool wasPressed, bool wasReleased)
        {
            if (!_initialized || flappyBirdConfig is null) return;

            _rigidBody2D.gravityScale = flappyBirdConfig.FlapGravityScale;

            // 기존 상승/낙하 속도에 힘을 누적하지 않아 연타와 낙하 중에도 같은 반응을 냅니다.
            // 홀드와 릴리스는 비행에 영향을 주지 않습니다.
            if (wasPressed)
            {
                Vector2 velocity = _rigidBody2D.linearVelocity;
                velocity.y = flappyBirdConfig.FlapVelocity;
                _rigidBody2D.linearVelocity = velocity;
            }

            Vector2 clampedVelocity = _rigidBody2D.linearVelocity;
            float clampedY = Mathf.Clamp(clampedVelocity.y, -flappyBirdConfig.MaxDownVelocity, flappyBirdConfig.MaxUpVelocity);
            _rigidBody2D.linearVelocity = new Vector2(clampedVelocity.x, clampedY);
        }

        public void ResetState()
        {
            if (!_initialized) return;

            transform.position = _startPosition;
            _rigidBody2D.linearVelocity = Vector2.zero;
            _rigidBody2D.angularVelocity = 0f;
        }
    }
}
