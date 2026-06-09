using UnityEngine;

/// <summary>
/// 적 유닛에 붙이는 컴포넌트.
/// 유닛이 사망하면 지정한 골드를 GoldSystem에 지급한다.
/// </summary>
public class GoldReward : MonoBehaviour
{
    [SerializeField] private int goldAmount = 10;

    private void Awake()
    {
        HealthSystem healthSystem = GetComponent<HealthSystem>();
        if (healthSystem != null)
            healthSystem.OnUnitDeath += HealthSystem_OnUnitDeath;
    }

    private void HealthSystem_OnUnitDeath(object sender, System.EventArgs e)
    {
        if (GoldSystem.Instance != null)
            GoldSystem.Instance.AddGold(goldAmount);
    }

    public int GetGoldAmount() => goldAmount;
}
