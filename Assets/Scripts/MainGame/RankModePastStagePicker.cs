using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 랭크모드(무한모드)에서 Past 스테이지 후보 중 하나를 가중치 기반으로 뽑는 순수 C# 클래스.
/// - 직전에 나온 게임은 가중치에 decayFactor 배율이 적용되어 다시 나올 확률이 크게 줄어든다.
/// - 같은 게임이 maxConsecutive번 연속으로 나왔다면 다음 픽에서는 해당 게임을 제외한다(후보가 2개 이상일 때).
/// 가중치는 매 Pick()마다 균등값에서 다시 계산된다.
/// </summary>
public class RankModePastStagePicker
{
    private const int RecommendedCandidateCount = 4;

    private readonly float _decayFactor;
    private readonly int _maxConsecutive;

    private readonly List<StageRuntimeConfiguration> _candidates;
    private readonly float[] _weights; // GC 할당 방지를 위해 미리 할당해 매 호출마다 재사용
    private int _lastPickedIndex = -1;
    private int _consecutiveCount = 0; // _lastPickedIndex가 연속으로 뽑힌 횟수

    /// <param name="candidates">후보 스테이지 목록</param>
    /// <param name="decayFactor">직전 게임의 가중치 배율 (0~1, 작을수록 반복 확률 감소). 후보 4개 기준 0.2면 연속 2회 확률 약 6%.</param>
    /// <param name="maxConsecutive">같은 게임이 연속으로 나올 수 있는 최대 횟수 (2면 3연속 불가)</param>
    public RankModePastStagePicker(List<StageRuntimeConfiguration> candidates, float decayFactor = 0.2f, int maxConsecutive = 2)
    {
        _candidates = candidates ?? new List<StageRuntimeConfiguration>();
        _decayFactor = Mathf.Clamp01(decayFactor);
        _maxConsecutive = Mathf.Max(1, maxConsecutive);

        if (_candidates.Count < RecommendedCandidateCount)
        {
            Debug.LogWarning($"[RankModePastStagePicker] 후보 스테이지가 {RecommendedCandidateCount}개 미만입니다. (현재 {_candidates.Count}개) 있는 만큼으로만 동작합니다.");
        }

        _weights = new float[_candidates.Count];
    }

    public StageRuntimeConfiguration Pick()
    {
        int count = _candidates.Count;
        if (count == 0)
        {
            Debug.LogWarning("[RankModePastStagePicker] 후보가 없어 Pick할 수 없습니다.");
            return null;
        }

        for (int i = 0; i < count; i++)
        {
            _weights[i] = 1f;
        }

        // 후보가 1개뿐이면 반복을 막을 방법이 없으므로 그대로 반환
        if (count > 1 && _lastPickedIndex >= 0 && _lastPickedIndex < count)
        {
            _weights[_lastPickedIndex] = _consecutiveCount >= _maxConsecutive
                ? 0f                // 최대 연속 횟수 도달 → 이번 픽에서 제외
                : _decayFactor;     // 직전 게임 확률 감소
        }

        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
        {
            totalWeight += _weights[i];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        int pickedIndex = -1;

        for (int i = 0; i < count; i++)
        {
            if (_weights[i] <= 0f) continue;

            cumulative += _weights[i];
            pickedIndex = i; // 부동소수점 오차로 루프를 빠져나가도 가중치 > 0인 마지막 후보가 선택됨
            if (roll < cumulative)
            {
                break;
            }
        }

        if (pickedIndex < 0)
        {
            // decayFactor가 0이고 후보가 1개인 경우 등 모든 가중치가 0일 때의 안전장치
            pickedIndex = 0;
        }

        _consecutiveCount = pickedIndex == _lastPickedIndex ? _consecutiveCount + 1 : 1;
        _lastPickedIndex = pickedIndex;
        return _candidates[pickedIndex];
    }
}
