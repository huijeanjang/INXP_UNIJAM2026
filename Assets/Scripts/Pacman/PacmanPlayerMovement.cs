using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Pacman
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PacmanPlayerInput))]
    public class PacmanPlayerMovement : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PacmanPlayerInput input;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Movement")]
        [SerializeField] private float speed = 4f;
        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private Vector2 initialDirection = Vector2.zero;
        [SerializeField] private LayerMask obstacleLayer;

        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider;
        private Tilemap _wallTilemap;
        private readonly RaycastHit2D[] _wallHits = new RaycastHit2D[16];
        // CompositeCollider2D의 접촉 여유까지 남겨 물리 보정으로 벽 안쪽에 밀리지 않게 함.
        private const float CollisionSkin = 0.02f;
        private Vector2 _direction;
        private Vector2 _nextDirection;
        private Vector2 _startingPosition;
        private bool _hasStartingPosition;
        private PacmanConfig _config;

        public Rigidbody2D Rigidbody2D => _rigidbody2D;
        public Vector2 CurrentDirection => _direction;
        public Vector2 NextDirection => _nextDirection;

        public void Configure(PacmanConfig config)
        {
            _config = config;
        }

        public Vector2 StartingPosition
        {
            get => _startingPosition;
            set
            {
                _startingPosition = value;
                _hasStartingPosition = true;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
            CaptureStartingPosition();
        }

        private void Start()
        {
            ResetState();
        }

        private void FixedUpdate()
        {
            if (_rigidbody2D == null || IsGameStopped())
            {
                return;
            }

            ReadRequestedDirection();
            _rigidbody2D.MovePosition(GetNextPosition(_rigidbody2D.position, StepDistance));
        }

        private float StepDistance => Mathf.Max(0f,
            (_config != null ? _config.playerMoveSpeed : speed) * speedMultiplier * Time.fixedDeltaTime);

        private Vector2 GetNextPosition(Vector2 position, float distance)
        {
            if (_nextDirection != Vector2.zero && distance > 0f)
            {
                Vector2 turnPosition = GetTurnPosition(position, _nextDirection);
                Vector2 alignment = turnPosition - position;
                float alignmentDistance = alignment.magnitude;
                // 회전 전에 통로 중앙까지의 경로와 회전 후의 경로를 모두 검사함.
                if (GetClearDistance(position, alignment.normalized, alignmentDistance) >= alignmentDistance &&
                    GetClearDistance(turnPosition, _nextDirection, distance) >= distance)
                {
                    float alignmentStep = Mathf.Min(distance, alignmentDistance);
                    position = Vector2.MoveTowards(position, turnPosition, alignmentStep);
                    distance -= alignmentStep;
                    if (alignmentStep < alignmentDistance)
                    {
                        return position;
                    }

                    _direction = _nextDirection;
                    _nextDirection = Vector2.zero;
                    UpdateSpriteFlip(_direction);
                    // 한 번의 MovePosition으로 꺾인 경로를 대각선으로 가로지르지 않게 함.
                    if (alignmentDistance > 0f)
                    {
                        return position;
                    }
                }
            }

            return position + _direction * GetClearDistance(position, _direction, distance);
        }

        public void ResetState()
        {
            EnsureInitialized();
            CaptureStartingPosition();

            speedMultiplier = 1f;
            _direction = _config != null ? _config.playerInitialDirection : initialDirection;
            _nextDirection = Vector2.zero;
            UpdateSpriteFlip(_direction);
            enabled = true;

            if (_rigidbody2D == null)
            {
                return;
            }

            transform.position = new Vector3(_startingPosition.x, _startingPosition.y, transform.position.z);
            _rigidbody2D.position = _startingPosition;
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        }

        public void StopMovement()
        {
            _direction = Vector2.zero;
            _nextDirection = Vector2.zero;

            if (_rigidbody2D == null)
            {
                return;
            }

            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }

        public void SetDirection(Vector2 direction, bool forced = false)
        {
            direction = ToCardinal(direction);
            if (direction == Vector2.zero)
            {
                return;
            }

            if (forced)
            {
                _direction = direction;
                _nextDirection = Vector2.zero;
                UpdateSpriteFlip(_direction);
            }
            else
            {
                _nextDirection = direction;
            }
        }

        public bool Occupied(Vector2 direction)
        {
            EnsureInitialized();
            direction = ToCardinal(direction);
            if (direction == Vector2.zero)
            {
                return false;
            }

            return GetClearDistance(_rigidbody2D.position, direction, StepDistance) < StepDistance;
        }

        private float GetClearDistance(Vector2 position, Vector2 direction, float distance)
        {
            if (distance <= 0f || direction == Vector2.zero)
            {
                return 0f;
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleLayer);
            PhysicsScene2D physicsScene = gameObject.scene.GetPhysicsScene2D();
            int count;
            if (_collider is CircleCollider2D circle)
            {
                Vector2 offset = circle.transform.TransformVector(circle.offset);
                Vector3 scale = circle.transform.lossyScale;
                float radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                count = physicsScene.CircleCast(position + offset, radius, direction,
                    distance + CollisionSkin, filter, _wallHits);
            }
            else
            {
                Bounds bounds = _collider.bounds;
                Vector2 offset = (Vector2)bounds.center - _rigidbody2D.position;
                count = physicsScene.BoxCast(position + offset, bounds.size, 0f, direction,
                    distance + CollisionSkin, filter, _wallHits);
            }

            float clearDistance = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _wallHits[i];
                if (hit.collider == _collider || hit.rigidbody == _rigidbody2D ||
                    Vector2.Dot(hit.normal, direction) >= -0.001f)
                {
                    continue;
                }

                clearDistance = Mathf.Min(clearDistance, Mathf.Max(0f, hit.distance - CollisionSkin));
            }

            return clearDistance;
        }

        private Vector2 GetTurnPosition(Vector2 position, Vector2 direction)
        {
            // 반대 방향 전환은 현재 위치에서 즉시 허용함.
            if (_wallTilemap == null || (_direction != Vector2.zero &&
                Mathf.Abs(Vector2.Dot(_direction, direction)) > 0.5f))
            {
                return position;
            }

            // 교차로 자체는 넓으므로 진입할 통로 쪽에서도 너비를 확인함.
            float probeDistance = Mathf.Max(_collider.bounds.size.x, _collider.bounds.size.y);
            Vector2 aligned = GetCorridorCenter(position, position + direction * probeDistance, direction);
            return aligned != position ? aligned : GetCorridorCenter(position, position, direction);
        }

        private Vector2 GetCorridorCenter(Vector2 position, Vector2 probePosition, Vector2 direction)
        {
            Vector3Int cell = _wallTilemap.WorldToCell(probePosition);
            Vector3Int side = direction.x == 0f ? Vector3Int.right : Vector3Int.up;
            if (_wallTilemap.HasTile(cell))
            {
                return position;
            }

            // 이 맵의 좁은 통로는 두 셀 너비임. 한 셀 중앙이 아닌 통로 중앙을 사용함.
            bool openBefore = !_wallTilemap.HasTile(cell - side);
            bool openAfter = !_wallTilemap.HasTile(cell + side);
            if (openBefore == openAfter)
            {
                return position;
            }

            Vector3Int neighbor = cell + (openBefore ? -side : side);
            if (!_wallTilemap.HasTile(neighbor + (openBefore ? -side : side)))
            {
                return position;
            }

            Vector2 center = (_wallTilemap.GetCellCenterWorld(cell) +
                              _wallTilemap.GetCellCenterWorld(neighbor)) * 0.5f;
            Vector2 target = direction.x == 0f ? new Vector2(center.x, position.y) : new Vector2(position.x, center.y);
            // 셀 좌표 변환의 미세 오차는 정렬 완료로 취급함.
            // 작은 벡터의 normalized가 zero가 되어 출발이 거절되는 것을 방지함.
            if ((target - position).sqrMagnitude < 0.00000001f)
            {
                return position;
            }
            float halfWidth = Vector3.Distance(_wallTilemap.GetCellCenterWorld(cell),
                _wallTilemap.GetCellCenterWorld(neighbor));
            return Vector2.Distance(position, target) <= halfWidth ? target : position;
        }

        private void ReadRequestedDirection()
        {
            if (input == null || input.RequestedDirection == Vector2.zero)
            {
                return;
            }

            SetDirection(input.RequestedDirection);
            input.ClearRequestedDirection();
        }

        private void EnsureInitialized()
        {
            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (input == null)
            {
                input = GetComponent<PacmanPlayerInput>();
            }

            if (_collider == null)
            {
                _collider = GetComponent<Collider2D>();
            }

            if (_wallTilemap == null)
            {
                PacmanGrid grid = GetComponentInParent<PacmanGrid>();
                if (grid != null)
                {
                    grid.ResolveReferences();
                    _wallTilemap = grid.WallTilemap;
                }
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (obstacleLayer.value == 0)
            {
                obstacleLayer = LayerMask.GetMask("Wall");
            }

            if (_rigidbody2D == null)
            {
                // INXPLog.Warn("[PacmanPlayerMovement] Rigidbody2D is missing.", this);
            }
        }

        private void CaptureStartingPosition()
        {
            if (_hasStartingPosition)
            {
                return;
            }

            _startingPosition = transform.position;
            _hasStartingPosition = true;
        }

        private static Vector2 ToCardinal(Vector2 direction)
        {
            if (direction == Vector2.zero)
            {
                return Vector2.zero;
            }

            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }

        private void UpdateSpriteFlip(Vector2 direction)
        {
            if (spriteRenderer == null || Mathf.Approximately(direction.x, 0f))
            {
                return;
            }

            spriteRenderer.flipX = direction.x > 0f;
        }

        private bool IsGameStopped()
        {
            bool mainGameStopped = GameSceneManager.Instance != null &&
                                   (GameSceneManager.Instance.IsPaused || GameSceneManager.Instance.IsGameOver);
            bool pacmanWaitingToStart = PacmanGameManager.Instance != null &&
                                        !PacmanGameManager.Instance.IsPlaying;

            return mainGameStopped || pacmanWaitingToStart;
        }
    }
}
