using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
	[SerializeField]
	private int maxHealth = 100;

	[SerializeField]
	private int currentHealth;

	private ShootAction shootAction;
	private BarrierSystem barrierSystem;
	private StatusEffectSystem statusEffectSystem;

	public event EventHandler OnUnitDeath;
	public event EventHandler OnUnitDamaged;
	/// <summary>실제 피해를 입을 때 발생. int = 최종 피해량.</summary>
	public event EventHandler<int> OnDamageTaken;

	private void Awake()
	{
		shootAction = GetComponent<ShootAction>();
		barrierSystem = GetComponent<BarrierSystem>();
		statusEffectSystem = GetComponent<StatusEffectSystem>();
		currentHealth = maxHealth;
	}

	/// <param name="defense">피해 감소량. 배리어 흡수 후 남은 피해에서 차감된다. 최소 1은 보장.</param>
	public void Damage(int damageAmount, int defense = 0)
	{
		// [버그 수정] 이미 사망한 유닛(currentHealth == 0)에 피해가 들어오면 즉시 반환.
		//   수정 전: currentHealth(0) -= damage → 음수 → 0으로 클램프 → Die() 재호출
		//   → OnUnitDeath 이벤트 중복 발생 → 래그돌이 여러 번 스폰되는 버그.
		//   죽은 유닛을 타겟으로 선택한 AI가 공격 애니메이션을 완료한 뒤 Melee()를 호출할 때 발생했다.
		if (currentHealth <= 0) return;

		// 피해 증폭 디버프 적용
		if (statusEffectSystem != null)
			damageAmount = Mathf.RoundToInt(damageAmount * statusEffectSystem.GetIncomingDamageMultiplier());

		if (barrierSystem != null && barrierSystem.HasBarrier())
			damageAmount = barrierSystem.AbsorbDamage(damageAmount);

		// 방어력만큼 피해 감소. 최소 1은 보장해 방어력이 높아도 무조건 1 이상 받는다.
		damageAmount = Mathf.Max(1, damageAmount - defense);

		if (damageAmount <= 0) return;

		currentHealth -= damageAmount;
		if (currentHealth < 0)
		{
			currentHealth = 0;
		}
		this.OnUnitDamaged?.Invoke(this, EventArgs.Empty);
		OnDamageTaken?.Invoke(this, damageAmount);
		if (currentHealth == 0)
		{
			Die();
		}
	}

	public void Heal(int healAmount)
	{
		currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
		OnUnitDamaged?.Invoke(this, EventArgs.Empty);
	}

	public void Die()
	{
		this.OnUnitDeath?.Invoke(this, EventArgs.Empty);
	}

	public float GetHealthNormalized()
	{
		return (float)currentHealth / (float)maxHealth;
	}

	public int GetCurrentHealth()
	{
		return currentHealth;
	}

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    /// <summary>
    /// 세이브 복원 시 HP를 직접 설정한다.
    /// Damage()·Heal()과 달리 OnUnitDamaged·OnUnitDeath 이벤트를 발생시키지 않으므로
    /// 무력화 유닛을 HP=0으로 설정해도 사망 처리가 중복 실행되지 않는다.
    /// </summary>
    public void SetHealth(int hp)
    {
        currentHealth = Mathf.Clamp(hp, 0, maxHealth);
    }
}
