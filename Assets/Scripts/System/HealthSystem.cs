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

	public event EventHandler OnUnitDeath;

	public event EventHandler OnUnitDamaged;

	private void Awake()
	{
		shootAction = GetComponent<ShootAction>();
		barrierSystem = GetComponent<BarrierSystem>();
		currentHealth = maxHealth;
	}

	public void Damage(int damageAmount)
	{
		if (barrierSystem != null && barrierSystem.HasBarrier())
			damageAmount = barrierSystem.AbsorbDamage(damageAmount);

		if (damageAmount <= 0) return;

		currentHealth -= damageAmount;
		if (currentHealth < 0)
		{
			currentHealth = 0;
		}
		this.OnUnitDamaged?.Invoke(this, EventArgs.Empty);
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
}
