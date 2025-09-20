using Events;
using UnityEngine;
using UNKO.EventBus;
using UNKO.ServiceLocator;

namespace Events
{
    /// <summary>
    /// 이동 속도 설정 이벤트를 위한 인터페이스
    /// 게임 시스템에서 캐릭터의 이동 속도를 동적으로 변경할 때 사용
    /// </summary>
    public interface ISetMoveSpeedEvent
    {
        /// <summary>설정할 이동 속도 값</summary>
        float MoveSpeed { get; }
    }
}

/// <summary>
/// 2D 환경에서 게임 오브젝트의 이동을 관리하는 컴포넌트
/// Rigidbody2D를 사용하여 물리 기반 이동을 처리하며, 이벤트 시스템을 통해 속도 조절과 정지 기능을 제공
/// 주로 플레이어 캐릭터의 자동 이동에 사용됨
/// </summary>
public class MoveComponent : MonoBehaviour
{
    /// <summary>오브젝트의 이동 속도 (초당 유닛)</summary>
    [SerializeField]
    float _speed;

    /// <summary>물리 기반 이동을 위한 Rigidbody2D 컴포넌트</summary>
    [SerializeField]
    Rigidbody2D _rigidbody2D;

    /// <summary>이동 정지 상태를 나타내는 플래그</summary>
    [SerializeField]
    bool _isStop;

    /// <summary>
    /// Rigidbody2D 컴포넌트를 자동으로 찾아 할당하고, 이동 속도 변경 이벤트를 구독
    /// </summary>
    void Awake()
    {
        if (_rigidbody2D == null)
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
        }

        EventBus.Global.SubscribeSticky<ISetMoveSpeedEvent>(e =>
        {
            _speed = e.MoveSpeed;
        }).AddToDestroy(this);
    }

    /// <summary>
    /// 물리 업데이트 주기에 맞춰 오브젝트를 오른쪽으로 일정한 속도로 이동시킴
    /// 정지 상태가 아닐 때만 이동을 수행
    /// </summary>
    void FixedUpdate()
    {
        if (_isStop)
        {
            return;
        }

        _rigidbody2D.velocity = Vector2.right * _speed;
    }

    /// <summary>
    /// 오브젝트의 이동을 정지하거나 재개하는 메서드
    /// 정지 시에는 velocity를 0으로 설정하여 즉시 멈춤
    /// </summary>
    /// <param name="isStop">true면 이동 정지, false면 이동 재개</param>
    public void SetStop(bool isStop)
    {
        _isStop = isStop;

        if (isStop)
        {
            _rigidbody2D.velocity = Vector2.zero;
        }
    }
}
