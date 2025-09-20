using Cysharp.Threading.Tasks;
using Events;
using UnityEngine;
using UNKO.EventBus;
using UNKO.ServiceLocator;
using static LevelSystem;

/// <summary>
/// 몬스터의 핵심 로직을 관리하는 메인 컴포넌트
/// 몬스터 데이터 초기화, 시각적 표현 설정, HP 관리, 데미지/사망 이벤트 처리를 담당
/// 서비스 로케이터와 이벤트 버스를 통해 다른 시스템들과 상호작용
/// </summary>
public class MonsterMain : MonoBehaviour
{
    /// <summary>몬스터의 기본 정보 (HP, 공격력, 모델 등)를 담는 데이터</summary>
    [SerializeField]
    MonsterData _monsterData;

    /// <summary>몬스터 생성 시 초기화 데이터 (스페셜 몬스터 여부 등)</summary>
    [SerializeField]
    MonsterInitData _monsterInitData;

    /// <summary>스페셜 몬스터일 때 표시할 이펙트 프리팹</summary>
    [SerializeField]
    GameObject _specialMonsterEffect;

    /// <summary>몬스터의 시각적 모델을 설정하는 컴포넌트</summary>
    ModelSetter _modelSetter;

    /// <summary>
    /// 몬스터 컴포넌트들을 초기화하고 서비스 등록, 이벤트 구독, HP 시스템 설정을 수행
    /// - 몬스터 데이터 수신 시 모델 설정
    /// - 스페셜 몬스터 처리 (색상 변경, 이펙트 생성)
    /// - HP 컴포넌트 이벤트 연결 (데미지, 사망 처리)
    /// </summary>
    void Awake()
    {
        _modelSetter = GetComponentInChildren<ModelSetter>();

        SL.GameObjectOf(this).RegisterServiceAndInterfaces(this);
        SL.GameObjectOf(this).RegisterService(GetComponent<Rigidbody2D>());

        // 몬스터 데이터 수신 시 모델 설정
        EventBus.GameObjectOf(this).SubscribeSticky<MonsterData>(data =>
        {
            _monsterData = data;
            _modelSetter.SetModel(Resources.Load<Sprites>("Monster/" + _monsterData.modelPath));
        }).AddToDestroy(this);

        // 몬스터 초기화 데이터 수신 시 스페셜 몬스터 처리
        EventBus.GameObjectOf(this).SubscribeSticky<MonsterInitData>(data =>
        {
            _monsterInitData = data;

            if (data.IsSpecialMonster)
            {
                _modelSetter.Renderer.color = Color.yellow;
                Instantiate(_specialMonsterEffect, transform);
            }
        }).AddToDestroy(this);

        // HP 컴포넌트 설정 및 이벤트 연결
        var hpComponent = GetComponent<HPComponent>();
        SL.GameObjectOf(this).RegisterService(hpComponent);
        hpComponent.OnTakeDamage += (damage) =>
        {
            EventBus.Global.Publish(new OnDamageMonsterEvent(gameObject, damage));
        };

        hpComponent.OnDead += async (damage) =>
        {
            OnDeadMonsterEvent evt = new(gameObject, damage.arg.Attacker, _monsterData, _monsterInitData);
            if (damage.isOneShot)
            {
                evt.SetOneShot();
            }
            EventBus.Global.Publish(evt);
            GetComponentInChildren<BoxCollider2D>().enabled = false;

            Destroy(gameObject, 1f);
        };
    }
}