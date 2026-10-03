using Core.Input;
using FlappyBird.Interfaces.Player;
using UnityEngine;

namespace FlappyBird.Player.Input
{
    /// <summary>
    /// 통합 입력 값을 플래피버드 입력 형식으로 전달합니다.
    /// </summary>
    public class FlappyInputAdapter : MonoBehaviour, IBirdInputSource
    {
        // 통합 입력 소스 참조
        private IUnifiedInput _globalInput;

        private void Start()
        {
            // 통합 입력 인스턴스 조회
            _globalInput = UnifiedInputManager.Instance;

            if (_globalInput == null)
            {
                Debug.LogError("FlappyInputAdapter: UnifiedInputManager를 찾을 수 없습니다.");
            }
        }

        // 공통 인터페이스용 홀드 상태입니다. 탭 비행 모터에서는 사용하지 않습니다.
        public bool IsHolding => _globalInput != null && _globalInput.IsPressing;
        public bool WasPressedThisFrame => _globalInput != null && _globalInput.WasTappedThisFrame;
        public bool WasReleasedThisFrame => _globalInput != null && _globalInput.WasReleasedThisFrame;
    }
}
