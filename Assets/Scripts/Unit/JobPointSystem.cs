using System;
using UnityEngine;

/// <summary>
/// 직업 포인트(JP)를 관리하는 컴포넌트.
/// JP는 직업별로 구분된다. 예를 들어 궁수 JP를 소모하는 스킬은
/// classId가 "Archer"인 유닛만 사용할 수 있다.
/// JP는 새 게임 시작 시(유닛 인스턴시에이트) 최대치로 시작하고
/// 스테이지 클리어 후에도 회복되지 않는다. 새 게임을 시작해야만 최대치로 돌아온다.
/// 액션 포인트(AP)가 매 턴 회복되는 것과 달리 JP는 한 번 쓰면 게임 내내 유지된다.
/// </summary>
public class JobPointSystem : MonoBehaviour
{
    [Tooltip("이 유닛의 직업. 같은 직업 JP를 요구하는 스킬만 사용할 수 있다.")]
    [SerializeField] private JobClass jobClass;

    [SerializeField] private int maxJobPoints = 3;

    private int currentJobPoints;

    /// <summary>현재 JP가 변경될 때 발생. UI 갱신에 사용.</summary>
    public event EventHandler OnJobPointsChanged;

    private void Awake()
    {
        // 유닛이 생성되는 시점(새 게임 시작)에 JP를 최대치로 초기화
        currentJobPoints = maxJobPoints;
    }

    // ─── 공개 API ────────────────────────────────────────────────

    public JobClass GetJobClass() => jobClass;
    public int GetCurrentJobPoints() => currentJobPoints;
    public int GetMaxJobPoints() => maxJobPoints;

    /// <summary>
    /// 직업이 일치하고 JP가 충분한지 확인한다.
    /// requiredClass가 Common이면 직업 상관없이 JP만 체크한다.
    /// requiredClass가 None이면 항상 true (JP 소모 없는 액션).
    /// </summary>
    public bool CanSpend(JobClass requiredClass, int cost)
    {
        if (requiredClass == JobClass.None) return true;

        // Common은 어떤 직업이든 사용 가능
        if (requiredClass != JobClass.Common && jobClass != requiredClass)
            return false;

        return currentJobPoints >= cost;
    }

    /// <summary>
    /// JP를 소모한다. 직업 불일치 또는 JP 부족이면 소모하지 않고 false를 반환한다.
    /// </summary>
    public bool SpendJobPoints(JobClass requiredClass, int cost)
    {
        if (!CanSpend(requiredClass, cost)) return false;

        currentJobPoints -= cost;
        OnJobPointsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
