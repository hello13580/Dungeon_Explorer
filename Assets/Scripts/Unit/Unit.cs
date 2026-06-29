using System;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
	private const int ACTION_POINTS_INITIAL = 0;

	private GridPosition savedPosition;

	private HealthSystem healthSystem;

	private ManaSystem manaSystem;

	private JobPointSystem jobPointSystem;

	private HitReactionSystem hitReactionSystem;

	private List<BaseAction> baseActionList;

	[SerializeField]
	private string unitName = ""; // UI에 표시할 유닛 이름

	[SerializeField]
	[TextArea(2, 5)]
	private string enemyDescription = "";

	[SerializeField]
	private int currentActionPoint;

	[SerializeField]
	private int maxActionPoint;

	[SerializeField]
	private int attackPower = 0; // 고정 피해에 더해지는 공격력 스탯

	[SerializeField]
	private int defensePower = 0; // 방어막 스킬의 고정 수치에 더해지는 방어력 스탯

	[SerializeField]
	private TeamType teamType;

	[SerializeField]
	private float initialSpeed;

	[SerializeField]
	private float currentSpeed;

	[SerializeField]
	private float actionGauge;

	[SerializeField]
	private Transform ShootPointTransform;

	[SerializeField]
	private int size = 1;

	private bool isStealthed;

	[Header("스킬 설정")]
	[SerializeField] private UnitSkillConfig skillConfig;

	// Y 스냅: 경사로처럼 높이가 다른 지형 위에 서있을 때 발밑 지면에 시각적으로 붙이기 위한 설정
	[SerializeField] private LayerMask groundSnapLayerMask;
	// 스냅 보간 속도 — 값이 클수록 빠르게 붙음
	[SerializeField] private float groundSnapSpeed = 20f;

	public static event EventHandler OnAnyActionPointsChanged;

	public static event EventHandler OnAnyUnitSpawned;

	public static event EventHandler OnAnyUnitDead;

	public static event EventHandler OnAnySkillsChanged;

	private void LateUpdate()
	{
		SnapToGround();
	}

	private void SnapToGround()
	{
		// groundSnapLayerMask가 설정되지 않으면 스냅하지 않음
		if (groundSnapLayerMask == 0) return;

		// 현재 위치보다 2유닛 위에서 아래로 레이캐스트
		// 위에서 쏘는 이유: 경사로 표면이 현재 Y보다 위에 있을 수 있어서
		// 아래쪽 3유닛 거리 내의 첫 번째 지면을 찾음 (y+2 ~ y-1 범위)
		Vector3 rayOrigin = transform.position + Vector3.up * 2f;
		if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3f, groundSnapLayerMask))
		{
			float targetY = hit.point.y;

			// 1.5유닛 이상 차이나면 스냅하지 않음
			// → 다른 층의 지면으로 끌려가는 것을 방지
			if (Mathf.Abs(targetY - transform.position.y) > 1.5f) return;

			// 부드럽게 보간해서 지면에 붙임 (즉시 이동하면 이동 중 떨림 발생)
			float snappedY = Mathf.Lerp(transform.position.y, targetY, groundSnapSpeed * Time.deltaTime);
			transform.position = new Vector3(transform.position.x, snappedY, transform.position.z);
		}
	}

	private void Awake()
	{
		healthSystem = GetComponent<HealthSystem>();
		manaSystem = GetComponent<ManaSystem>();
		jobPointSystem = GetComponent<JobPointSystem>();
		hitReactionSystem = GetComponent<HitReactionSystem>();

		// 활성화된 컴포넌트만 기본 스킬로 등록 — 비활성은 나중에 보상으로 습득
		baseActionList = new List<BaseAction>();
		foreach (BaseAction action in GetComponents<BaseAction>())
		{
			if (action.enabled)
				baseActionList.Add(action);
		}

		currentActionPoint = maxActionPoint;
		currentSpeed = initialSpeed;
		actionGauge = 0f;
	}

	private void Start()
	{
		savedPosition = LevelGrid.Instance.GetGridPosition(transform.position);
		float cellSize = LevelGrid.Instance.GetCellSize();
		float num = (float)(size - 1) * cellSize * 0.5f;
		Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(savedPosition);
		transform.position = new Vector3(worldPosition.x + num, transform.position.y, worldPosition.z + num);
		LevelGrid.Instance.AddUnitAtGridPosition(savedPosition, this);
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
		healthSystem.OnUnitDeath += HealthSystem_OnUnitDeath;
		ApplyLearnedSkills();
		Unit.OnAnyUnitSpawned?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// 새 스테이지 로드 시 PartyManager가 호출.
	/// 위치를 스폰 포인트로 이동하고 LevelGrid / TurnSystem에 재등록한다.
	/// </summary>
	public void RegisterForNewStage(Vector3 spawnPosition)
	{
		// 그리드 위치 계산 및 스냅
		GridPosition newGridPos = LevelGrid.Instance.GetGridPosition(spawnPosition);
		float cellSize = LevelGrid.Instance.GetCellSize();
		float offset = (float)(size - 1) * cellSize * 0.5f;
		Vector3 worldPos = LevelGrid.Instance.GetWorldPosition(newGridPos);
		transform.position = new Vector3(worldPos.x + offset, spawnPosition.y, worldPos.z + offset);
		savedPosition = newGridPos;

		// LevelGrid에 등록
		LevelGrid.Instance.AddUnitAtGridPosition(savedPosition, this);

		// TurnSystem 이벤트 중복 구독 방지 후 재구독
		TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;

		// 마나 초기량으로 초기화
		manaSystem?.ResetToInitialMana();

		// 액션 포인트 초기화
		currentActionPoint = maxActionPoint;

		// UnitManager에 재등록
		Unit.OnAnyUnitSpawned?.Invoke(this, EventArgs.Empty);
	}

	public UnitSkillConfig GetSkillConfig() => skillConfig;

	/// <summary>
	/// actionTypeName에 해당하는 비활성 컴포넌트를 찾아 활성화하고 액션 목록에 등록한다.
	/// SkillUnlockManager와 ApplyLearnedSkills에서 호출된다.
	/// </summary>
	public bool UnlockSkillByTypeName(string actionTypeName)
	{
		System.Type type = FindActionType(actionTypeName);
		if (type == null)
		{
			Debug.LogWarning($"[Unit] 타입 '{actionTypeName}'을 찾을 수 없습니다.");
			return false;
		}

		BaseAction action = (BaseAction)GetComponent(type);
		if (action == null)
		{
			Debug.LogWarning($"[Unit] {name}에 {actionTypeName} 컴포넌트가 없습니다. 프리팹에 비활성 상태로 부착되어 있어야 합니다.");
			return false;
		}

		if (action.enabled) return false; // 이미 활성화됨

		action.enabled = true;
		baseActionList.Add(action);
		OnAnySkillsChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	private void ApplyLearnedSkills()
	{
		if (skillConfig == null || PartySkillData.Instance == null) return;

		foreach (SkillDefinition skillDef in skillConfig.learnableSkills)
		{
			if (PartySkillData.Instance.IsLearned(skillConfig.unitClassId, skillDef.actionTypeName))
				UnlockSkillByTypeName(skillDef.actionTypeName);
		}
	}

	private static System.Type FindActionType(string typeName)
	{
		foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			System.Type type = assembly.GetType(typeName);
			if (type != null) return type;
		}
		return null;
	}

	public T GetAction<T>() where T : BaseAction
	{
		foreach (BaseAction baseAction in baseActionList)
		{
			if (baseAction is T)
			{
				return baseAction as T;
			}
		}
		return null;
	}

	public T AddSkill<T>() where T : BaseAction
	{
		T action = gameObject.AddComponent<T>();
		baseActionList.Add(action);
		OnAnySkillsChanged?.Invoke(this, EventArgs.Empty);
		return action;
	}

	public void RemoveSkill<T>() where T : BaseAction
	{
		T action = GetAction<T>();
		if (action != null)
		{
			baseActionList.Remove(action);
			Destroy(action);
			OnAnySkillsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public bool IsStealthed()
	{
		return isStealthed;
	}

	public void SetStealth(bool state)
	{
		isStealthed = state;
	}

	public GridPosition GetGridPosition()
	{
		return savedPosition;
	}

	public Vector3 GetWorldPosition()
	{
		Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(GetGridPosition());
		float cellSize = LevelGrid.Instance.GetCellSize();
		float num = (float)(size - 1) * cellSize * 0.5f;
		return worldPosition + new Vector3(num, 0f, num);
	}

	public BaseAction[] GetBaseActionsArray()
	{
		return baseActionList.ToArray();
	}

	public bool CanSpendActionPointsToTakeAction(BaseAction baseAction)
	{
		return currentActionPoint >= baseAction.GetActionPointCost();
	}

	public bool CanSpendManaToTakeAction(BaseAction baseAction)
	{
		if (manaSystem == null) return true;
		return manaSystem.CanSpendMana(baseAction.GetManaCost());
	}

	/// <summary>직업 포인트가 충분한지 확인. JobPointSystem이 없으면 항상 true.</summary>
	public bool CanSpendJobPointsToTakeAction(BaseAction baseAction)
	{
		return baseAction.HasEnoughJobPoints();
	}

	/// <summary>AP·마나·JP 세 자원을 모두 충족할 때만 액션을 실행할 수 있다.</summary>
	public bool CanTakeAction(BaseAction baseAction)
	{
		return CanSpendActionPointsToTakeAction(baseAction)
			&& CanSpendManaToTakeAction(baseAction)
			&& CanSpendJobPointsToTakeAction(baseAction);
	}

	public bool SpendActionPoint(BaseAction baseAction)
	{
		if (!CanTakeAction(baseAction)) return false;

		currentActionPoint -= baseAction.GetActionPointCost();
		Unit.OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);

		if (manaSystem != null)
			manaSystem.SpendMana(baseAction.GetManaCost());

		// JP 소모 — jobPointCost가 0이면 아무 일도 일어나지 않는다
		if (jobPointSystem != null)
			jobPointSystem.SpendJobPoints(baseAction.GetRequiredJobClass(), baseAction.GetJobPointCost());


		return true;
	}

	public ManaSystem GetManaSystem() => manaSystem;

	/// <summary>직업 포인트 시스템 참조. JobPointSystem 컴포넌트가 없으면 null.</summary>
	public JobPointSystem GetJobPointSystem() => jobPointSystem;

	public int GetCurrentActionPoint() => currentActionPoint;
	public int GetMaxActionPoint() => maxActionPoint;
	public string GetUnitName() => unitName;
	public string GetEnemyDescription() => enemyDescription;

	/// <summary>액션 포인트를 최대치까지 회복한다.</summary>
	public void RestoreActionPoints()
	{
		currentActionPoint = maxActionPoint;
		OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>액션 포인트를 지정한 양만큼 회복한다. 최대치를 초과하지 않는다.</summary>
	public void RestoreActionPoints(int amount)
	{
		currentActionPoint = Mathf.Min(currentActionPoint + amount, maxActionPoint);
		OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>공격 스킬의 고정 피해에 더해지는 공격력 스탯. 일시적 버프를 포함한다.</summary>
	public int GetAttackPower()
	{
		AttackBuffSystem abs = GetComponent<AttackBuffSystem>();
		return attackPower + (abs != null ? abs.GetTotalBonus() : 0);
	}

	/// <summary>
	/// 최종 피해를 계산한다. 고정 피해 + 공격력 + 약화 등 디버프 배율을 한 번에 적용.
	/// 모든 공격 액션은 (damage + unit.GetAttackPower()) 대신 이 메서드를 사용한다.
	/// </summary>
	public int CalculateDamage(int baseDamage)
	{
		float multiplier = 1f;
		StatusEffectSystem ses = GetComponent<StatusEffectSystem>();
		if (ses != null) multiplier = ses.GetOutgoingDamageMultiplier();
		return Mathf.RoundToInt((baseDamage + GetAttackPower()) * multiplier);
	}
	/// <summary>방어막 스킬의 고정 수치에 더해지는 방어력 스탯.</summary>
	public int GetDefensePower() => defensePower;

	private void TurnSystem_OnTurnChanged(object sender, EventArgs empty)
	{
		if (TurnSystem.Instance.GetTurnUnit() == this)
		{
			currentActionPoint = maxActionPoint;
			Unit.OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);

			manaSystem?.RegenTurn();
		}
	}

	public TeamType GetTeamType()
	{
		return teamType;
	}

	public void Damage(int damageAmount)
	{
		healthSystem.Damage(damageAmount);
	}

	public void Heal(int healAmount)
	{
		healthSystem.Heal(healAmount);
	}

	public HitReactionSystem GetHitReaction()
	{
		return hitReactionSystem;
	}

	public Collider GetCollider()
	{
		return GetComponent<Collider>();
	}

	public void HealthSystem_OnUnitDeath(object sender, EventArgs empty)
	{
		if (gameObject.layer == LayerMask.NameToLayer("Object"))
		{
			PathFinding.Instance.SetIsWalkable(savedPosition, isWalkable: true);
		}
		LevelGrid.Instance.RemoveUnitAtGridPosition(GetGridPosition(), this);
		Unit.OnAnyUnitDead?.Invoke(this, EventArgs.Empty);

		// 아군은 무력화 상태로 전환 (부활 가능), 적·오브젝트는 즉시 제거
		if (teamType == TeamType.Player)
			gameObject.SetActive(false);
		else
			Destroy(gameObject);
	}

	public float GetCurrentSpeed()
	{
		return currentSpeed;
	}

	public float GetCurrentGauge()
	{
		return actionGauge;
	}

	public void AddGauge()
	{
		actionGauge += GetCurrentSpeed();
	}

	public void SubGauge(float subAmount)
	{
		actionGauge -= subAmount;
	}

	public bool CanAct(float gaugeRequired)
	{
		return actionGauge >= gaugeRequired;
	}

	public void ResetGauge()
	{
		actionGauge = 0f;
	}

	public float GetHealthNormalized()
	{
		return healthSystem.GetHealthNormalized();
	}

	public int GetCurrentHealth()
	{
		return healthSystem.GetCurrentHealth();
	}

	public Transform GetShootPointTransform()
	{
		return ShootPointTransform;
	}

	public int GetSize()
	{
		return size;
	}

	public LayerMask GetGroundSnapLayerMask()
	{
		return groundSnapLayerMask;
	}

	public void SetGridPosition(GridPosition newGridPosition)
	{
		if (savedPosition != newGridPosition)
		{
			LevelGrid.Instance.UnitMovedGridPosition(this, savedPosition, newGridPosition);
			savedPosition = newGridPosition;
		}
	}
}
