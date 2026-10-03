using System.Collections.Generic;
using FlappyBird.Components;
using FlappyBird.Configs;
using FlappyBird.Interfaces.Pipes;
using UnityEngine;
using Utils;

namespace FlappyBird
{
    /// <summary>
    /// 파이프/아이템을 생성하고 스크롤 이동을 제어합니다.
    /// </summary>
    public class PipeSpawner : MonoBehaviour, IScrollSpeedProvider
    {
        [Header("설정")]
        [SerializeField] private FlappyBirdConfig config;

        public float CurrentScrollSpeed { get; private set; }
        public bool IsScrolling { get; private set; }

        private readonly DistanceSpawnScheduler _spawnScheduler = new DistanceSpawnScheduler();
        private readonly SpawnedMoverRegistry _moverRegistry = new SpawnedMoverRegistry();
        private readonly IPipePatternGenerator _patternGenerator = new DefaultPipePatternGenerator();

        private bool _isSpawning;
        private float _spawnIntervalDistance;
        private float _spawnOffset;

        private float? _prevItemY;
        private Item _lastSpawnedItem;
        private int _consecutiveItemCount;

        private const string TAG_PIPE = "Pipe";
        private const string TAG_ITEM = "Item";

        private void Awake()
        {
            IsScrolling = false; // 초기에는 이동이 멈춘 상태로 시작
            if (config != null)
            {
                CurrentScrollSpeed = config.PipeMoveSpeed; // 초기 이동 속도 설정
                // 스폰 간격을 이동 속도에 기반하여 계산
                _spawnIntervalDistance = config.PipeMoveSpeed * config.PipeSpawnInterval;
            }
        }

        private void Start()
        {
            if (config == null)
            {
                CustomLog.Error("PipeSpawner: 설정 파일이 누락되었습니다.", this);
                enabled = false;
                return;
            }

            if (ObjectPool.Instance != null)
            {
                // 파이프와 아이템 프리팹에 대한 오브젝트 풀 생성
                ObjectPool.Instance.CreatePool(config.TopPipePrefab, 5);
                ObjectPool.Instance.CreatePool(config.BottomPipePrefab, 5);
                ObjectPool.Instance.CreatePool(config.BranchPipePrefab, 5);
                if (config.ItemPrefab != null)
                {
                    ObjectPool.Instance.CreatePool(config.ItemPrefab, 10);
                }
            }

            if (config.TopPipePrefab == null || config.BottomPipePrefab == null || config.BranchPipePrefab == null)
            {
                CustomLog.Error("PipeSpawner: 파이프 프리팹 설정이 누락되었습니다 (Top/Bottom/Branch).", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_isSpawning)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            // 이동 속도가 최대값보다 낮을 때 가속 적용
            if (CurrentScrollSpeed < config.MaxMoveSpeed)
            {
                CurrentScrollSpeed += config.Acceleration * deltaTime;
                // 최대 이동 속도 제한
                CurrentScrollSpeed = Mathf.Min(CurrentScrollSpeed, config.MaxMoveSpeed);
            }

            // 이번 프레임에 이동한 거리 계산
            float movedDistanceThisFrame = CurrentScrollSpeed * deltaTime;

            // 누적 이동 거리를 업데이트하고, 스폰 간격을 초과했는지 여부를 확인
            if (_spawnScheduler.TryConsume(movedDistanceThisFrame, _spawnIntervalDistance))
            {
                SpawnObstaclePattern(config.PipeSpawnX + _spawnOffset, true);
            }

            _moverRegistry.CleanupInactiveMovers(); // 비활성화된 이동 오브젝트 정리
        }

        /// <summary>
        /// 파이프와 아이템을 초기화하고, 스폰 패턴과 스폰 타이밍을 리셋합니다. preserveSpeed가 true로 설정되면, 현재 이동 속도를 유지한 채로 초기화합니다. false로 설정하면, 이동 속도가 config에 정의된 초기값으로 리셋됩니다.
        /// </summary>
        /// <param name="preserveSpeed"></param>
        /// <param name="isFirstPlay">첫 플레이에서는 초기 배치와 이후 스폰 위치를 함께 오른쪽으로 미룹니다.</param>
        public void PreparePipes(bool preserveSpeed = false, bool isFirstPlay = false)
        {
            if (config == null)
            {
                CustomLog.Error("PipeSpawner: 설정 파일이 누락되었습니다.", this);
                return;
            }

            // 스폰 관련 상태 초기화
            _spawnOffset = isFirstPlay ? Mathf.Max(0f, config.InitialPipeSpawnOffset) : 0f;
            _prevItemY = null;
            _lastSpawnedItem = null;
            _consecutiveItemCount = 0;

            _patternGenerator.Reset(config);
            _spawnScheduler.Reset();

            if (!preserveSpeed) // 이동 속도를 초기값으로 리셋
            {
                CurrentScrollSpeed = config.PipeMoveSpeed;
            }

            // 기존 파이프와 아이템 제거 및 초기 파이프/아이템 프리웜
            ClearPipes();
            PreWarmPipes(false);
        }

        public void StartSpawning()
        {
            _isSpawning = true;
            IsScrolling = true;
            _moverRegistry.SetMovementState(true);
        }

        public void StopSpawning()
        {
            _isSpawning = false;
        }

        public void StopPipeMovement()
        {
            IsScrolling = false;
            _moverRegistry.SetMovementState(false);
        }

        public void ClearPipes()
        {
            _moverRegistry.Clear();
        }

        /// <summary>
        /// 초기 파이프와 아이템을 스폰합니다. moveImmediately가 true로 설정되면, 초기 파이프와 아이템이 생성되자마자 이동을 시작합니다. 이 메서드는 config에 정의된 PipeSpawnX에서 시작하여, 화면 왼쪽 끝까지 일정 간격으로 파이프와 아이템을 스폰합니다. 무한 루프 방지를 위해, spawnIntervalDistance가 0이거나 너무 작은 경우에는 PreWarm을 중단합니다.
        /// </summary>
        /// <param name="moveImmediately"></param>
        private void PreWarmPipes(bool moveImmediately)
        {
            if (config == null)
            {
                return;
            }

            // spawnIntervalDistance가 0이거나 음수인 경우, config에서 이동 속도와 스폰 간격 설정을 기반으로 spawnIntervalDistance를 계산합니다.
            if (_spawnIntervalDistance <= 0f)
            {
                _spawnIntervalDistance = config.PipeMoveSpeed * config.PipeSpawnInterval;
            }

            float pipeSpacing = _spawnIntervalDistance;
            if (pipeSpacing <= 0.01f)
            {
                CustomLog.Warn("PipeSpawner: PipeSpacing이 0이거나 너무 작습니다. 무한 루프를 방지하기 위해 PreWarm을 중단합니다.", this);
                return;
            }

            float currentX = config.PipeSpawnX;
            List<float> spawnPositions = new List<float>(); // 스폰 위치 리스트 생성

            while (currentX >= -0.8f)
            {
                spawnPositions.Add(currentX);
                currentX -= pipeSpacing;
            }

            // 화면 왼쪽 끝에서부터 시작하여 오른쪽으로 스폰하기 위해 리스트를 역순으로 정렬
            spawnPositions.Reverse();
            foreach (float x in spawnPositions)
            {
                // 개수와 간격을 유지한 채 전체 배치를 이동해야 첫 장애물도 늦게 도착합니다.
                SpawnObstaclePattern(x + _spawnOffset, moveImmediately);
            }
        }

        /// <summary>
        /// 파이프와 아이템을 스폰하는 메서드입니다. spawnX 위치에 파이프 패턴을 생성하고, 패턴의 중앙에 아이템을 배치합니다. 패턴이 분기형인 경우, 중앙과 양쪽에 아이템을 추가로 배치합니다. moveImmediately가 true로 설정되면, 생성된 파이프와 아이템이 즉시 이동을 시작합니다.
        /// </summary>
        /// <param name="spawnX"></param>
        /// <param name="moveImmediately"></param>
        private void SpawnObstaclePattern(float spawnX, bool moveImmediately)
        {
            PipePatternResult pattern = _patternGenerator.Next(config); // 다음 스폰 패턴 생성
            float centerY = pattern.CenterY;
            bool isBranching = pattern.IsBranching;

            // 이전 아이템 위치가 존재하는 경우, 이전 아이템과 현재 패턴 중앙 사이의 중간 지점에 아이템을 배치하여, 플레이어가 다양한 높이에서 아이템을 수집할 수 있도록 합니다.
            if (_prevItemY.HasValue)
            {
                float halfDistance = (config.PipeMoveSpeed * config.PipeSpawnInterval) / 2f;
                float midX = spawnX - halfDistance;
                float midY = (_prevItemY.Value + centerY) / 2f;
                CreateItemObject(new Vector3(midX, midY, 0f), moveImmediately);
            }

            // 패턴 유형에 따라 파이프와 아이템을 생성합니다. 분기형 패턴인 경우, 중앙과 양쪽에 아이템을 배치하여 플레이어가 다양한 높이에서 아이템을 수집할 수 있도록 합니다.
            if (isBranching)
            {
                CreateBranchingPipes(centerY, spawnX, moveImmediately);

                float innerEdge = config.InnerPipeSize / 2f;
                float outerEdge = config.DoublePipeVerticalSpacing - (config.PipeSize / 2f);
                float gapCenterOffset = (innerEdge + outerEdge) / 2f + 0.5f;
                float offset = config.ItemPathSpacing / 2f;

                CreateItemObject(new Vector3(spawnX + offset, centerY + gapCenterOffset, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX, centerY + gapCenterOffset, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX - offset, centerY + gapCenterOffset, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX + offset, centerY - gapCenterOffset, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX, centerY - gapCenterOffset, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX - offset, centerY - gapCenterOffset, 0), moveImmediately);
            }
            else
            {
                CreateStandardPipePair(centerY, spawnX, moveImmediately);
                float offset = config.ItemPathSpacing / 2f;
                CreateItemObject(new Vector3(spawnX - offset, centerY, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX, centerY, 0), moveImmediately);
                CreateItemObject(new Vector3(spawnX + offset, centerY, 0), moveImmediately);
            }

            _prevItemY = centerY;
        }

        /// <summary>
        /// 표준 파이프 쌍을 생성하는 메서드입니다. centerY를 기준으로 상하 파이프를 배치하며, gapHeight는 config에 정의된 GapHeight를 사용하여 계산됩니다. moveImmediately가 true로 설정되면, 생성된 파이프가 즉시 이동을 시작합니다.
        /// </summary>
        /// <param name="centerY"></param>
        /// <param name="spawnX"></param>
        /// <param name="moveImmediately"></param>
        private void CreateStandardPipePair(float centerY, float spawnX, bool moveImmediately)
        {
            float halfGap = config.GapHeight / 2f;
            CreatePipeInstance(config.BottomPipePrefab, new Vector3(spawnX, centerY - halfGap, 0), moveImmediately);
            CreatePipeInstance(config.TopPipePrefab, new Vector3(spawnX, centerY + halfGap, 0), moveImmediately);
        }

        /// <summary>
        /// 분기형 파이프를 생성하는 메서드입니다. centerY를 기준으로 중앙에 분기형 파이프를 배치하고, 그 위와 아래에 상하 파이프를 배치합니다. gapHeight는 config에 정의된 DoublePipeVerticalSpacing을 사용하여 계산됩니다. moveImmediately가 true로 설정되면, 생성된 파이프가 즉시 이동을 시작합니다.
        /// </summary>
        /// <param name="centerY"></param>
        /// <param name="spawnX"></param>
        /// <param name="moveImmediately"></param>
        private void CreateBranchingPipes(float centerY, float spawnX, bool moveImmediately)
        {
            CreatePipeInstance(config.BranchPipePrefab, new Vector3(spawnX, centerY, 0), moveImmediately);
            float spacing = config.DoublePipeVerticalSpacing;
            CreatePipeInstance(config.BottomPipePrefab, new Vector3(spawnX, centerY - spacing, 0), moveImmediately);
            CreatePipeInstance(config.TopPipePrefab, new Vector3(spawnX, centerY + spacing, 0), moveImmediately);
        }

        /// <summary>
        /// 파이프 인스턴스를 생성하는 메서드입니다. prefab을 spawnX 위치에 생성하고, moveImmediately가 true로 설정되면 즉시 이동을 시작합니다. 생성된 오브젝트는 PipeSpawner의 자식으로 설정되고, 태그가 "Pipe"로 지정됩니다. 또한, LinearMover와 BoundaryRecycler 컴포넌트를 추가하여 이동과 화면 밖에서의 재활용을 처리합니다. 생성된 오브젝트는 SpawnedMoverRegistry에 등록되어, 이동 상태를 일괄적으로 제어할 수 있도록 합니다.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="moveImmediately"></param>
        private void CreatePipeInstance(GameObject prefab, Vector3 position, bool moveImmediately)
        {
            if (prefab == null || ObjectPool.Instance == null)
            {
                return;
            }

            GameObject pipeInstance = ObjectPool.Instance.Spawn(prefab, position, Quaternion.identity);
            pipeInstance.transform.SetParent(transform);
            pipeInstance.tag = TAG_PIPE;

            AttachComponents(pipeInstance, prefab, moveImmediately);
        }

        /// <summary>
        /// 아이템 인스턴스를 생성하는 메서드입니다. config.ItemPrefab을 spawnX 위치에 생성하고, moveImmediately가 true로 설정되면 즉시 이동을 시작합니다. 생성된 오브젝트는 PipeSpawner의 자식으로 설정되고, 태그가 "Item"으로 지정됩니다. 또한, WorldItem 컴포넌트를 추가하여 아이템 데이터를 초기화하고, LinearMover와 BoundaryRecycler 컴포넌트를 추가하여 이동과 화면 밖에서의 재활용을 처리합니다. 생성된 오브젝트는 SpawnedMoverRegistry에 등록되어, 이동 상태를 일괄적으로 제어할 수 있도록 합니다.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="moveImmediately"></param>
        private void CreateItemObject(Vector3 position, bool moveImmediately)
        {
            if (config.ItemPrefab is null || ObjectPool.Instance == null)
            {
                return;
            }

            if (ItemDataBase.Items == null || ItemDataBase.Items.Length == 0)
            {
                return;
            }

            GameObject itemInstance = ObjectPool.Instance.Spawn(config.ItemPrefab, position, Quaternion.identity);
            itemInstance.transform.SetParent(transform);
            itemInstance.tag = TAG_ITEM;

            Item selectedItem = SelectNextItem(); // 아이템 선택 로직을 통해 다음에 스폰할 아이템 결정
            if (!itemInstance.TryGetComponent(out WorldItem worldItem))
            {
                worldItem = itemInstance.AddComponent<WorldItem>();
            }
            worldItem.Initialize(selectedItem);

            AttachComponents(itemInstance, config.ItemPrefab, moveImmediately);
        }

        /// <summary>
        /// 아이템 선택 로직입니다. ItemDataBase에서 랜덤하게 아이템을 선택하되, 동일한 아이템이 3회 이상 연속으로 선택되는 것을 방지합니다. 이를 위해, 마지막으로 선택된 아이템과 연속 선택 횟수를 추적하여, 동일한 아이템이 2회 연속으로 선택된 경우 다음 선택에서는 다른 아이템이 선택되도록 합니다.
        /// </summary>
        /// <returns></returns>
        private Item SelectNextItem()
        {
            int randomIndex = Random.Range(0, ItemDataBase.Items.Length);
            Item selectedItem = ItemDataBase.Items[randomIndex];

            if (ItemDataBase.Items.Length > 1)
            {
                if (selectedItem == _lastSpawnedItem)
                {
                    if (_consecutiveItemCount >= 2)
                    {
                        randomIndex = (randomIndex + 1) % ItemDataBase.Items.Length;
                        selectedItem = ItemDataBase.Items[randomIndex];
                        _lastSpawnedItem = selectedItem;
                        _consecutiveItemCount = 1;
                    }
                    else
                    {
                        _consecutiveItemCount++;
                    }
                }
                else
                {
                    _lastSpawnedItem = selectedItem;
                    _consecutiveItemCount = 1;
                }
            }

            return selectedItem;
        }

        /// <summary>
        /// 생성된 오브젝트에 LinearMover와 BoundaryRecycler 컴포넌트를 추가하여 이동과 화면 밖에서의 재활용을 처리하는 메서드입니다. mover는 config에 정의된 이동 속도로 초기화되고, recycler는 config에 정의된 파이프 스폰 X 위치를 기준으로 화면 밖으로 나갔을 때 재활용되도록 설정됩니다. 또한, SpawnedMoverRegistry에 등록하여 이동 상태를 일괄적으로 제어할 수 있도록 합니다.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="originalPrefab"></param>
        /// <param name="moveImmediately"></param>
        private void AttachComponents(GameObject obj, GameObject originalPrefab, bool moveImmediately)
        {
            if (!obj.TryGetComponent(out LinearMover mover))
            {
                mover = obj.AddComponent<LinearMover>();
            }

            float initialSpeed = moveImmediately ? config.PipeMoveSpeed : 0f;
            mover.Initialize(Vector3.left, initialSpeed, this);

            if (!obj.TryGetComponent(out BoundaryRecycler recycler))
            {
                recycler = obj.AddComponent<BoundaryRecycler>();
            }

            float thresholdX = -config.PipeSpawnX - 5.0f;
            recycler.Initialize(thresholdX, originalPrefab);

            _moverRegistry.Register(mover, obj, originalPrefab);
        }
    }
}
