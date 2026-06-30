using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAction : MonoBehaviour
{
	protected Unit unit;

	protected Action onActionComplete;

	[SerializeField]
	private string actionName = "";

	[SerializeField]
	protected int actionCost;

	[SerializeField]
	protected int manaCost = 0;

	/// <summary>
	/// 이 액션을 사용할 때 소모하는 직업 포인트(JP).
	/// 0이면 JP를 소모하지 않는 일반 액션이다.
	/// </summary>
	[SerializeField]
	protected int jobPointCost = 0;

	/// <summary>
	/// 이 액션이 요구하는 직업.
	/// None이면 JP를 소모하지 않는 일반 액션이다.
	/// Common이면 어떤 직업이든 JP만 있으면 사용 가능하다.
	/// 특정 직업(Warrior 등)으로 설정하면 해당 직업 유닛만 사용 가능하다.
	/// </summary>
	[SerializeField]
	protected JobClass requiredJobClass = JobClass.None;

	/// <summary>
	/// 적 AI가 이 액션을 평가할 때 actionValue에 더해지는 보너스.
	/// 같은 액션 스크립트라도 유닛마다 별도 컴포넌트 인스턴스이므로,
	/// 이 값을 유닛별로 다르게 설정해 액션 우선순위를 유닛 특성에 맞게 조정할 수 있다.
	/// 예: 오우거의 MeleeAction은 +50, 같은 오우거의 버프 액션은 -30 등.
	/// </summary>
	[SerializeField]
	[Tooltip("적 AI 평가 시 이 액션의 actionValue에 더해지는 보너스. 유닛별로 다르게 설정 가능.")]
	protected int aiPriorityBonus = 0;

	public static event EventHandler OnAnyActionStarted;

	public static event EventHandler OnAnyActionEnded;

	/// <summary>
	/// 스킬 보상 UI에 표시할 설명 문자열을 반환한다.
	/// 수치가 있는 스킬은 override해서 unit의 실제 스탯을 반영한 문자열을 반환한다.
	/// 빈 문자열을 반환하면 SkillDefinition.description을 fallback으로 사용한다.
	/// </summary>
	public virtual string GetDescription() => "";

	protected virtual string DefaultActionName() => "";

	protected virtual void Awake()
	{
		unit = GetComponent<Unit>();
		if (string.IsNullOrEmpty(actionName))
			actionName = DefaultActionName();
	}

	public virtual string GetActionName() => actionName;

	/// <summary>
	/// 액션 분류. 대미지를 입히는 액션은 Attack을 override하고, 그 외(이동·버프·힐·유틸리티 등)는
	/// 기본값인 Tactical을 그대로 사용한다.
	/// </summary>
	public virtual ActionCategory GetActionCategory() => ActionCategory.Tactical;

	public abstract void TakeAction(GridPosition gridPosition, Action onActionComplete);

	public virtual List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
	{
		if (!LevelGrid.Instance.IsValidGridPosition(targetGridPosition))
		{
			return new List<GridPosition>();
		}
		if (!IsValidActionGridPosition(targetGridPosition))
		{
			return new List<GridPosition>();
		}
		return new List<GridPosition> { targetGridPosition };
	}

	public virtual bool IsValidActionGridPosition(GridPosition gridPosition)
	{
		return GetValidActionGridPositionList().Contains(gridPosition);
	}

	public abstract List<GridPosition> GetValidActionGridPositionList();

	public virtual List<GridPosition> GetActionRangeGridPositionList()
	{
		return GetValidActionGridPositionList();
	}

	/// <summary>
	/// 두 번째 색상으로 표시할 타일 목록. 기본값은 빈 리스트.
	/// GridSystemVisual에서 GetSecondaryHighlightColor()로 지정한 색상으로 그려진다.
	/// </summary>
	public virtual List<GridPosition> GetSecondaryHighlightGridPositionList()
	{
		return new List<GridPosition>();
	}

	public virtual GridSystemVisual.GridVisualType GetSecondaryHighlightColor()
	{
		return GridSystemVisual.GridVisualType.Yellow;
	}

	public Unit GetUnit()
	{
		return unit;
	}

	public virtual int GetActionPointCost() => actionCost;
	public int GetManaCost() => manaCost;
	public int GetJobPointCost() => jobPointCost;
	public JobClass GetRequiredJobClass() => requiredJobClass;

	/// <summary>
	/// 유닛의 직업이 일치하고 JP가 충분한지 확인한다.
	/// requiredJobClass가 None이면 체크를 건너뛴다.
	/// </summary>
	public bool HasEnoughJobPoints()
	{
		if (requiredJobClass == JobClass.None) return true;
		JobPointSystem jps = unit.GetJobPointSystem();
		// JobPointSystem이 없으면 JP를 요구하는 스킬은 사용 불가
		if (jps == null) return false;
		return jps.CanSpend(requiredJobClass, jobPointCost);
	}

	/// <summary>
	/// JP를 실제로 소모한다. SpendActionPoint() 내부에서 호출된다.
	/// 직업 불일치 또는 JP 부족이면 false를 반환한다.
	/// </summary>
	protected bool SpendJobPoints()
	{
		if (requiredJobClass == JobClass.None) return true;
		JobPointSystem jps = unit.GetJobPointSystem();
		if (jps == null) return false;
		return jps.SpendJobPoints(requiredJobClass, jobPointCost);
	}

	protected void ActionStart(Action onActionComplete)
	{
		this.onActionComplete = onActionComplete;
		BaseAction.OnAnyActionStarted?.Invoke(this, EventArgs.Empty);
	}

	protected void ActionComplete()
	{
		onActionComplete();
		BaseAction.OnAnyActionEnded?.Invoke(this, EventArgs.Empty);
	}


	public EnemyAIAction GetBestEnemyAIAction()
	{
		List<EnemyAIAction> list = new List<EnemyAIAction>();
		List<GridPosition> validList = GetValidActionGridPositionList();
		foreach (GridPosition validActionGridPosition in validList)
		{
			EnemyAIAction enemyAIAction = GetEnemyAIAction(validActionGridPosition);
			if (enemyAIAction != null)
			{
				// 유닛별 AI 우선순위 보너스 적용 (인스펙터에서 액션별로 설정)
				enemyAIAction.actionValue += aiPriorityBonus;

				// 도발 중인 유닛이 있으면 그 위치를 타겟으로 하는 액션에 압도적인 보너스
				if (TauntManager.Instance != null && TauntManager.Instance.HasActiveTaunt())
				{
					Unit taunted = TauntManager.Instance.GetTauntedUnit();
					if (taunted != null && enemyAIAction.gridPosition == taunted.GetGridPosition())
						enemyAIAction.actionValue += 500;
					else
						enemyAIAction.actionValue -= 200;
				}
				list.Add(enemyAIAction);
			}
		}
		if (list.Count > 0)
		{
			list.Sort((EnemyAIAction a, EnemyAIAction b) => b.actionValue - a.actionValue);
			return list[0];
		}
		return null;
	}

	public abstract EnemyAIAction GetEnemyAIAction(GridPosition gridPosition);
}
