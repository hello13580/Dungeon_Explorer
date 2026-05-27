using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
	[SerializeField]
	private int maxHealth = 100;

	[SerializeField]
	private int currentHealth;

	private ShootAction shootAction;

	public event EventHandler OnUnitDeath;

	public event EventHandler OnUnitDamaged;

	private void Awake()
	{
		shootAction = GetComponent<ShootAction>();
		currentHealth = maxHealth;
	}

	public void Damage(int damageAmount)
	{
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
}
