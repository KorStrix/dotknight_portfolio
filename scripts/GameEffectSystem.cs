using Events;
using UnityEngine;
using UNKO.EventBus;
using Util;
using static LevelSystem;

namespace Events
{
    /// <summary>
    /// 몬스터가 데미지를 받았을 때 발생하는 이벤트
    /// 데미지 이펙트와 UI 표시를 위해 사용
    /// </summary>
    public class OnDamageMonsterEvent
    {
        /// <summary>데미지를 받은 몬스터 게임오브젝트</summary>
        public GameObject Target { get; }

        /// <summary>데미지 결과 정보 (데미지량, 크리티컬 여부 등)</summary>
        public DamageResult Damage { get; }

        public OnDamageMonsterEvent(GameObject target, DamageResult damage)
        {
            Target = target;
            Damage = damage;
        }
    }

    /// <summary>
    /// 몬스터가 사망했을 때 발생하는 이벤트
    /// 사망 이펙트, 골드 획득, 원샷 보너스 처리를 위해 사용
    /// </summary>
    public class OnDeadMonsterEvent
    {
        /// <summary>사망한 몬스터 게임오브젝트</summary>
        public GameObject Target { get; }

        /// <summary>몬스터를 처치한 공격자</summary>
        public IAttacker Attacker { get; }

        /// <summary>몬스터의 기본 데이터</summary>
        public MonsterData MonsterData { get; set; }

        /// <summary>몬스터의 초기화 데이터</summary>
        public MonsterInitData InitData { get; set; }

        /// <summary>원샷 킬 여부 (한 번에 처치했는지)</summary>
        public bool IsOneShot { get; set; } = false;

        /// <summary>스페셜 몬스터 여부</summary>
        public bool IsSpecialMonster => InitData != null && InitData.IsSpecialMonster;

        public OnDeadMonsterEvent(GameObject target, IAttacker attacker, MonsterData monsterData, MonsterInitData initData)
        {
            Target = target;
            Attacker = attacker;
            MonsterData = monsterData;
            InitData = initData;
        }

        /// <summary>원샷 킬로 설정</summary>
        public void SetOneShot()
        {
            IsOneShot = true;
        }
    }

    /// <summary>
    /// 플레이어 HP 회복 이벤트
    /// HP 회복 시 UI 표시를 위해 사용
    /// </summary>
    public class OnPlayerRecoveryHPEvent
    {
        /// <summary>HP를 회복한 플레이어 캐릭터</summary>
        public CharacterMain Target { get; }

        /// <summary>회복한 HP 양</summary>
        public BigNumber Amount { get; }

        public OnPlayerRecoveryHPEvent(CharacterMain target, BigNumber amount)
        {
            Target = target;
            Amount = amount;
        }
    }
}

/// <summary>
/// 게임 내 시각적 이펙트와 UI 피드백을 관리하는 시스템
/// 몬스터 데미지, 사망, 플레이어 HP 회복 등의 이벤트에 대한 시각적 반응을 처리
/// 파티클 이펙트, 데미지 폰트, 원샷 이펙트 등을 관리
/// </summary>
public class GameEffectSystem : MonoBehaviour
{
    /// <summary>몬스터 데미지 시 표시할 히트 이펙트 프리팹</summary>
    [SerializeField]
    GameObject _damageEffectPrefab;
    
    /// <summary>몬스터 사망 시 표시할 죽음 이펙트 프리팹</summary>
    [SerializeField]
    GameObject _deadEffectPrefab;
    
    /// <summary>원샷 킬 시 표시할 특수 이펙트 프리팹</summary>
    [SerializeField]
    GameObject _oneShotEffectPrefab;
    
    /// <summary>월드 공간 UI를 표시할 캔버스 Transform</summary>
    [SerializeField]
    Transform _worldCanvas;
    
    /// <summary>일반 데미지 표시용 텍스트 프리팹</summary>
    [SerializeField]
    TMPro.TextMeshProUGUI _damageFont;
    
    /// <summary>물리 데미지 표시용 텍스트 프리팹</summary>
    [SerializeField]
    TMPro.TextMeshProUGUI _physicalDamageFont;
    
    /// <summary>크리티컬 데미지 표시용 텍스트 프리팹</summary>
    [SerializeField]
    TMPro.TextMeshProUGUI _criticalDamageFont;
    
    /// <summary>HP 회복량 표시용 텍스트 프리팹</summary>
    [SerializeField]
    TMPro.TextMeshProUGUI _recoveryFont;
    
    /// <summary>폰트가 상승하는 속도 범위 (최소, 최대)</summary>
    [SerializeField]
    Vector2 _fontRiseSpeed = new Vector2(2f, 5f);

    /// <summary>
    /// Unity의 Awake 라이프사이클 메서드
    /// 게임 이펙트 관련 이벤트들을 구독하여 시각적 피드백 시스템을 초기화
    /// </summary>
    void Awake()
    {
        EventBus.Global.Subscribe<OnDamageMonsterEvent>(OnMonsterDamage);
        EventBus.Global.Subscribe<OnDeadMonsterEvent>(OnMonsterDead);
        EventBus.Global.Subscribe<OnPlayerRecoveryHPEvent>(OnPlayerRecoveryHP);
    }

    /// <summary>
    /// 플레이어 HP 회복 시 호출되는 메서드
    /// 회복량을 표시하는 텍스트를 생성하고 물리 효과를 적용하여 상승시킴
    /// </summary>
    /// <param name="evt">HP 회복 이벤트 데이터</param>
    private void OnPlayerRecoveryHP(OnPlayerRecoveryHPEvent evt)
    {
        var recoveryFont = Instantiate(_recoveryFont, evt.Target.transform.position, Quaternion.identity, _worldCanvas);
        recoveryFont.text = evt.Amount.ToString("0.#");

        float fontSpeed = Random.Range(_fontRiseSpeed.x, _fontRiseSpeed.y);
        recoveryFont.GetComponent<Rigidbody2D>().velocity = new Vector2(-1f, 1f).normalized * fontSpeed;

        Destroy(recoveryFont.gameObject, 1f);
    }

    /// <summary>
    /// 몬스터 데미지 시 호출되는 메서드
    /// 히트 이펙트를 생성하고 데미지 타입에 따라 적절한 폰트를 표시
    /// 물리 데미지와 일반/크리티컬 데미지를 구분하여 다른 시각적 효과 적용
    /// </summary>
    /// <param name="evt">몬스터 데미지 이벤트 데이터</param>
    private void OnMonsterDamage(OnDamageMonsterEvent evt)
    {
        var hitEffect = Instantiate(_damageEffectPrefab, evt.Target.transform.position, Quaternion.identity);
        Destroy(hitEffect, 2f);

        if (evt.Damage.IsPhysicalDamage)
        {
            var damageFont = Instantiate(_physicalDamageFont, evt.Target.transform.position, Quaternion.identity, _worldCanvas);
            damageFont.text = evt.Damage.Damage.ToString("0.#");

            Destroy(damageFont.gameObject, 1f);
        }
        else
        {
            Vector3 positionOffset = new Vector2(1f, 1f);
            var damageFont = Instantiate(evt.Damage.IsCritical ? _criticalDamageFont : _damageFont, evt.Target.transform.position + positionOffset, Quaternion.identity, _worldCanvas);
            damageFont.text = evt.Damage.Damage.ToString("0.#");

            float fontSpeed = Random.Range(_fontRiseSpeed.x, _fontRiseSpeed.y);
            damageFont.GetComponent<Rigidbody2D>().velocity = new Vector2(1f, 1f).normalized * fontSpeed;

            Destroy(damageFont.gameObject, 1f);
        }
    }

    /// <summary>
    /// 몬스터 사망 시 호출되는 메서드
    /// 죽음 이펙트를 생성하고, 원샷 킬인 경우 추가 특수 이펙트를 표시
    /// </summary>
    /// <param name="evt">몬스터 사망 이벤트 데이터</param>
    private void OnMonsterDead(OnDeadMonsterEvent evt)
    {
        var deadEffect = Instantiate(_deadEffectPrefab, evt.Target.transform.position, Quaternion.identity);
        Destroy(deadEffect, 2f);

        if (evt.IsOneShot)
        {
            Vector2 offset = new Vector2(Random.Range(0f, 0.5f), Random.Range(0f, 0.5f));
            var oneShotFont = Instantiate(_oneShotEffectPrefab, evt.Target.transform.position + (Vector3)offset, Quaternion.identity, _worldCanvas);
            float fontSpeed = Random.Range(_fontRiseSpeed.x, _fontRiseSpeed.y);
            oneShotFont.GetComponent<Rigidbody2D>().velocity = new Vector2(1f, 1f).normalized * fontSpeed;

            Destroy(oneShotFont.gameObject, 1f);
        }
    }
}