
// ==================================================================================== //
// 개인 프로젝트이며, 게임 데모 플레이가 가능합니다.
// 게임 플레이 링크와 더 많은 코드는 깃허브에 있습니다.
// https://github.com/KorStrix/dotknight_portfolio
// ==================================================================================== //

// ==================================================================================== //
// GameSystem.cs
// ==================================================================================== //
#region GameSystem
namespace Events
{
    /// <summary>
    /// 게임 재시작 전 레벨 오브젝트 초기화를 알리는 이벤트
    /// 모든 몬스터를 제거하고 게임 상태를 리셋하기 전에 발생
    /// </summary>
    public class OnBeforeReplayGameEvent
    {
        public OnBeforeReplayGameEvent()
        {
        }
    }

    /// <summary>
    /// 게임 재시작 완료를 알리는 이벤트
    /// 무한 청크 시스템의 리셋 이벤트 인터페이스를 구현
    /// </summary>
    public class OnReplayGameEvent : IInfiniteChunkResetEvent
    {
        public OnReplayGameEvent()
        {
        }
    }
}

/// <summary>
/// 게임 전체 시스템을 관리하는 핵심 클래스
/// 플레이어 캐릭터의 생성, 스테이지 진행, 몬스터 스폰, 스탯 관리, 환생 시스템 등을 총괄
/// UniTask와 이벤트 버스 시스템을 활용하여 비동기적으로 게임 루프를 관리
/// </summary>
public class GameSystem : MonoBehaviour
{
    /// <summary>
    /// 게임 초기화에 필요한 모든 데이터를 담는 내부 클래스
    /// 플레이어 초기 스탯, 스페셜 몬스터 확률, 스테이지 거리 설정 등을 포함
    /// IPlayerCharacterInitEvent와 ISetSpecialMonsterChanceEvent 인터페이스를 구현
    /// </summary>
    public class InitData : IPlayerCharacterInitEvent, ISetSpecialMonsterChanceEvent
    {
        /// <summary>초기화 완료 여부를 나타내는 플래그</summary>
        public bool IsInit => true;

        /// <summary>플레이어 초기 최대 HP (BigNumber 형태로 변환)</summary>
        public BigNumber MaxHP => new BigNumber(playerInitHp);

        /// <summary>플레이어 초기 HP 재생량 (BigNumber 형태로 변환)</summary>
        public BigNumber HpRegen => new BigNumber(playerInitHpRegen);

        /// <summary>HP 재생 쿨타임 (BigNumber를 float로 변환)</summary>
        public float HpRegenCooltime => new BigNumber(playerInitHPRegenCooltime);

        /// <summary>플레이어 초기 공격력 (BigNumber 형태로 변환)</summary>
        public BigNumber Damage => new BigNumber(playerInitDamage);

        /// <summary>플레이어 초기 크리티컬 확률</summary>
        public float CriticalChance => playerInitCriticalChance;

        /// <summary>플레이어 초기 넉백 수치</summary>
        public float Knockback => playerInitKnockback;

        /// <summary>플레이어 초기 이동 속도</summary>
        public float MoveSpeed => playerInitMoveSpeed;

        /// <summary>스페셜 몬스터 출현 확률</summary>
        public float SpecialMonsterChance => specialMonsterChance;

        /// <summary>플레이어 초기 HP (문자열 형태, BigNumber 연산을 위함)</summary>
        public string playerInitHp;

        /// <summary>플레이어 초기 공격력 (문자열 형태, BigNumber 연산을 위함)</summary>
        public string playerInitDamage;

        /// <summary>플레이어 초기 HP 재생량 (문자열 형태, BigNumber 연산을 위함)</summary>
        public string playerInitHpRegen;

        /// <summary>플레이어 초기 방어력 (문자열 형태, BigNumber 연산을 위함)</summary>
        public string playerInitArmor;

        /// <summary>HP 재생 쿨타임 (초 단위)</summary>
        public float playerInitHPRegenCooltime;

        /// <summary>플레이어 초기 넉백 수치</summary>
        public float playerInitKnockback;

        /// <summary>플레이어 초기 크리티컬 확률 (0.0 ~ 1.0)</summary>
        public float playerInitCriticalChance;

        /// <summary>플레이어 초기 이동 속도</summary>
        public float playerInitMoveSpeed;

        /// <summary>스페셜 몬스터 출현 확률 (0.0 ~ 1.0)</summary>
        public float specialMonsterChance;

        /// <summary>다음 스테이지까지의 거리</summary>
        public float nextStageDistance;

        /// <summary>다음 챕터까지의 거리</summary>
        public float nextChapterDistance;

        /// <summary>골드 획득량 계산을 위한 수식 (문자열 형태)</summary>
        public string getGoldExpression;

        /// <summary>환생 포인트 계산을 위한 수식 (문자열 형태)</summary>
        public string rebirthExpression;

        /// <summary>환생 조건이 되는 최소 챕터</summary>
        public int rebirthConditionChapter;

        /// <summary>원샷 킬 보너스 배율</summary>
        public float oneShotBonusMultiplier;

        /// <summary>스페셜 몬스터 처치 보너스 배율</summary>
        public float specialMonsterBonusMultiplier;
    }

    /// <summary>플레이어가 시작할 위치를 나타내는 게임오브젝트</summary>
    [SerializeField]
    GameObject _playerStartPosition;

    /// <summary>생성할 플레이어 캐릭터의 프리팹</summary>
    [SerializeField]
    GameObject _playerPrefab;

    /// <summary>게임 초기화 데이터 인스턴스</summary>
    InitData _initData;

    /// <summary>몬스터 스폰과 레벨 관리를 담당하는 시스템</summary>
    LevelSystem _levelSystem;

    /// <summary>현재 생성된 플레이어 캐릭터의 Transform 컴포넌트</summary>
    Transform _playerTransform;

    /// <summary>계정 정보(골드, 환생 포인트 등)를 관리하는 컨텍스트</summary>
    AccountContext _accountContext;

    /// <summary>스테이지별 데이터(몬스터 수, 종류 등)를 관리하는 데이터 저장소</summary>
    IDataRepository<string, StageData> _stageDataRepository;

    /// <summary>일반 스탯 업그레이드를 관리하는 시스템</summary>
    StatUpgradeSystem _statUpgradeSystem;

    /// <summary>환생 스탯 업그레이드를 관리하는 시스템</summary>
    RebirthStatUpgradeSystem _rebirthStatUpgradeSystem;

    /// <summary>비동기 작업 취소를 위한 토큰 소스</summary>
    CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    /// <summary>현재 진행 중인 챕터 번호</summary>
    int _currentChapter;

    /// <summary>현재 진행 중인 스테이지 번호</summary>
    int _currentStage;

    /// <summary>현재 챕터의 스테이지 데이터</summary>
    StageData _currentStageData;

    /// <summary>
    /// Unity의 Awake 라이프사이클 메서드
    /// 게임시스템 초기화를 비동기적으로 시작
    /// </summary>
    void Awake()
    {
        InitAsync();
    }

    /// <summary>
    /// 게임시스템의 비동기 초기화 메서드
    /// 서비스 로케이터에서 필요한 의존성들을 가져오고, 이벤트 구독을 설정한 후 게임을 시작
    /// - 몬스터 처치 시 골드 획득 처리
    /// - 플레이어 사망 시 게임 재시작 처리
    /// - 스탯 변경 이벤트 처리
    /// - 환생 이벤트 처리
    /// </summary>
    async void InitAsync()
    {
        (var gameInitData, var levelSystem, var accountContext, var stageRepo, var statSystem, var rebirthSystem) =
             await SL.Global.GetServiceAsync<InitData, LevelSystem, AccountContext, IDataRepository<string, StageData>, StatUpgradeSystem, RebirthStatUpgradeSystem>();

        _levelSystem = levelSystem;
        _accountContext = accountContext;
        _stageDataRepository = stageRepo;
        _initData = gameInitData;
        _statUpgradeSystem = statSystem;
        _rebirthStatUpgradeSystem = rebirthSystem;

        // 몬스터 처치 시 골드 획득 이벤트 구독
        EventBus.Global.Subscribe<OnDeadMonsterEvent>(evt =>
        {
            var gold = BigNumberExpressionEvaluator.Evaluate(_initData.getGoldExpression,
                ("value", evt.MonsterData.initGold),
                ("stage", _currentStage),
                ("chapter", _currentChapter));

            if (evt.IsOneShot)
            {
                gold *= _initData.oneShotBonusMultiplier;
            }

            if (evt.IsSpecialMonster)
            {
                gold *= _initData.specialMonsterBonusMultiplier;
            }

            _accountContext.Gold += gold;
        }).AddToDestroy(this);

        // NOTE 플레이어 사망 후 UI 처리 완료 시 게임 재시작 이벤트 구독
        EventBus.Global.Subscribe<OnPlayerCharacterDieAfterUIEvent>(evt =>
        {
            ResetReplayGame();
        }).AddToDestroy(this);

        // NOTE 스탯 변경 이벤트 구독
        EventBus.Global.Subscribe<OnChangeStatEvent>(evt =>
        {
            PublishStatToPlayer(evt.StatId, evt.NewLevel);
        }).AddToDestroy(this);

        // NOTE 환생 버튼 클릭 이벤트 구독
        EventBus.Global.Subscribe<OnClickRebirthEvent>(async e =>
        {
            _playerTransform.GetComponent<MoveComponent>().SetStop(true);
            await EventBus.Global.PublishAsync(new OnPlayerRebirthEvent());
            _accountContext.Rebirth();
            var rebirthPoint = BigNumberExpressionEvaluator.Evaluate(_initData.rebirthExpression,
                ("chapter", _currentChapter),
                ("stage", _currentStage),
                ("conditionChapter", _initData.rebirthConditionChapter));

            if (rebirthPoint > 0)
            {
                _accountContext.RebirthPoint += rebirthPoint;
            }
            else
            {
                Debug.LogError("No RebirthPoint gained");
            }

            ResetReplayGame();
        }).AddToDestroy(this);

        InitAndPlayGame();
    }

    /// <summary>
    /// 현재 챕터를 설정하는 메서드 (최고 챕터 갱신 없음)
    /// </summary>
    /// <param name="newChapter">설정할 새 챕터 번호</param>
    public void SetChapter(int newChapter)
    {
        SetChapter(newChapter, false);
    }

    /// <summary>
    /// 현재 챕터를 설정하고 선택적으로 최고 챕터를 갱신하는 메서드
    /// 챕터에 해당하는 스테이지 데이터를 로드하며, 데이터가 없으면 순환하여 찾음
    /// </summary>
    /// <param name="newChapter">설정할 새 챕터 번호</param>
    /// <param name="isUpdateHighestChapter">최고 챕터 기록을 갱신할지 여부</param>
    public void SetChapter(int newChapter, bool isUpdateHighestChapter)
    {
        _currentChapter = newChapter;
        _currentStageData = _stageDataRepository.GetValue($"{_currentChapter}");
        if (_currentStageData == null)
        {
            _currentStageData = _stageDataRepository.GetValue($"{(_currentChapter % _stageDataRepository.DataByKey.Count) + 1}");
            if (_currentStageData == null)
            {
                Debug.LogError($"Cannot find stage data for chapter: {_currentChapter}");
            }
        }

        if (isUpdateHighestChapter)
        {
            if (_accountContext.HighestChapter < _currentChapter)
            {
                _accountContext.HighestChapter = _currentChapter;
            }
        }
    }

    /// <summary>
    /// 게임을 리셋하고 재시작하는 메서드
    /// 모든 몬스터를 제거하고, 리셋 이벤트를 발생시킨 후 게임을 다시 초기화
    /// </summary>
    private void ResetReplayGame()
    {
        EventBus.Global.Publish(new OnBeforeReplayGameEvent());
        InitAndPlayGame();
        EventBus.Global.Publish(new OnReplayGameEvent());
    }

    /// <summary>
    /// 게임을 초기화하고 플레이를 시작하는 메서드
    /// 기존 플레이어 오브젝트를 제거하고 새 플레이어를 생성한 후, 초기 스탯을 적용하고 게임플레이를 시작
    /// </summary>
    private void InitAndPlayGame()
    {
        _levelSystem.ClearAllMonsters();
        if (_playerTransform != null)
        {
            Destroy(_playerTransform.gameObject);
        }
        GameObject playerObject = Instantiate(_playerPrefab, _playerStartPosition.transform.position, Quaternion.identity);
        _playerTransform = playerObject.transform;

        var playerEventBus = EventBus.GameObjectOf(playerObject);
        playerEventBus.Publish(_initData);

        var allStat = System.Enum.GetValues(typeof(StatUpgradeId));
        foreach (StatUpgradeId id in allStat)
        {
            PublishStat(id);
        }

        GamePlay();
    }

    /// <summary>
    /// 특정 스탯의 현재 레벨을 가져와서 플레이어에게 적용하는 메서드
    /// </summary>
    /// <param name="id">적용할 스탯의 ID</param>
    private void PublishStat(StatUpgradeId id)
    {
        var newLevel = _statUpgradeSystem.GetCurrentLevel(id);
        PublishStatToPlayer(id, newLevel);
    }

    /// <summary>
    /// 스탯 ID와 레벨에 따라 계산된 스탯 값을 플레이어에게 적용하는 메서드
    /// 환생 스탯과 일반 스탯을 구분하여 처리하고, 스탯 종류에 따라 적절한 이벤트를 발생시킴
    /// </summary>
    /// <param name="id">적용할 스탯의 ID</param>
    /// <param name="newLevel">스탯의 새로운 레벨</param>
    private void PublishStatToPlayer(StatUpgradeId id, BigNumber newLevel)
    {
        if (id == StatUpgradeId.none)
        {
            return;
        }

        BigNumber newStat;
        if (id.TryConvertRebirthStatUpgrade(out var rebirthStatId))
        {
            newStat = _rebirthStatUpgradeSystem.CalculateStat(rebirthStatId);
        }
        else
        {
            newStat = _statUpgradeSystem.CalculateStat(id, newLevel);
        }

        // NOTE 플레이어의 이벤트 버스에 스텟을 Set하는 이벤트를 발생하면, 플레이어의 컴포넌트들에서 받아서 처리
        var playerEventBus = EventBus.GameObjectOf(_playerTransform);
        switch (id)
        {
            case StatUpgradeId.attack:
                playerEventBus.Publish(new DamageStatUpgradeEvent(newStat));
                break;
            case StatUpgradeId.hp:
                playerEventBus.Publish(new HPStatUpgradeEvent(newStat));
                break;
            case StatUpgradeId.hpRegen:
                playerEventBus.Publish(new HPRegenStatUpgradeEvent(newStat));
                break;
            case StatUpgradeId.criticalChance:
                playerEventBus.Publish(new CriticalChanceStatUpgradeEvent((float)newStat));
                break;
            case StatUpgradeId.knockback:
                playerEventBus.Publish(new KnockbackStatUpgradeEvent((float)newStat));
                break;
            case StatUpgradeId.moveSpeed:
                playerEventBus.Publish(new MoveSpeedStatUpgradeEvent((float)newStat));
                break;
            case StatUpgradeId.specialMonsterChance:
                playerEventBus.Publish(new SetSpecialMonsterChanceEvent((float)newStat));
                break;

            default:
                Debug.LogError($"Not Implemented StatUpgradeId {id}");
                break;
        }

        playerEventBus.Publish(new CharacterStatUpgradeEvent(id.ToString(), newLevel, newStat));
    }

    /// <summary>
    /// 게임플레이를 시작하는 메서드
    /// 챕터 1, 스테이지 1부터 시작하며, 비동기 게임 루프들을 시작
    /// </summary>
    private void GamePlay()
    {
        SetChapter(1);
        _currentStage = 1;
        EventBus.Global.Publish(new OnChangeChapterStageEvent(_currentChapter, _currentStage, _accountContext.HighestChapter));

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken token = _cancellationTokenSource.Token;

        CheckLevelLoopAsync(token).Forget();
        CheckStageDistanceLoopAsync(token).Forget();
    }

    /// <summary>
    /// 스테이지 진행 거리를 체크하는 비동기 루프
    /// 플레이어의 이동 거리를 추적하여 스테이지와 챕터 진행을 관리
    /// 일정 거리마다 스테이지가 증가하고, 목표 거리에 도달하면 새 챕터로 진행
    /// </summary>
    /// <param name="token">작업 취소를 위한 캐싱레이션 토큰</param>
    private async UniTask CheckStageDistanceLoopAsync(CancellationToken token)
    {
        float moveDistanceOffset = _playerTransform.position.x;
        float goalDistance = _initData.nextChapterDistance;

#if UNITY_EDITOR
        while (Application.isPlaying)
#else
        while (!token.IsCancellationRequested)
#endif
        {
            float currentDistance = _playerTransform.position.x - moveDistanceOffset;
            EventBus.Global.Publish(new OnChangeStageDistanceEvent(currentDistance, goalDistance));

            if (currentDistance >= goalDistance)
            {
                moveDistanceOffset = _playerTransform.position.x;
                goalDistance += _initData.nextStageDistance;
                bool isNewRecord = _accountContext.HighestChapter < _currentChapter;
                SetChapter(_currentChapter + 1, true);
                _currentStage = 1;

                var msg = new OnChangeChapterStageEvent(_currentChapter, _currentStage, _accountContext.HighestChapter)
                    .SetIsNewChapter(true)
                    .SetNewRecord(isNewRecord);
                EventBus.Global.Publish(msg);
            }
            else
            {
                int newStage = (int)(currentDistance / _initData.nextStageDistance) + 1;
                if (newStage > _currentStage)
                {
                    _currentStage = newStage;
                    EventBus.Global.Publish(new OnChangeChapterStageEvent(_currentChapter, _currentStage, _accountContext.HighestChapter));
                }
            }

            await UniTask.Delay(100, cancellationToken: _cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// 레벨의 몬스터 상태를 체크하는 비동기 루프
    /// 현재 레벨에 몬스터가 없으면 스테이지 데이터에 따라 새 몬스터들을 스폰
    /// 몬스터 수는 최소/최대 범위 내에서 랜덤하게 결정
    /// </summary>
    /// <param name="token">작업 취소를 위한 캐싱레이션 토큰</param>
    private async UniTask CheckLevelLoopAsync(CancellationToken token)
    {
#if UNITY_EDITOR
        while (Application.isPlaying)
#else
        while (!token.IsCancellationRequested)
#endif
        {
            if (_levelSystem.CurrentLevelMonsters.Count == 0)
            {
                int spawnCount = Random.Range(_currentStageData.monsterMinCount, _currentStageData.monsterMaxCount);
                _levelSystem.SpawnMonster(_currentStageData.monsterIds, spawnCount, _playerStartPosition.transform.position);
            }
            await UniTask.Delay(1000, cancellationToken: token);
        }
    }
}
#endregion GameSystem


// ==================================================================================== //
// GameEffectSystem.cs
// ==================================================================================== //
#region GameEffectSystem
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
#endregion