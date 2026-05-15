using System;
using UnityEngine;

public class UnitRagdollSpawner : MonoBehaviour
{
	[SerializeField]
	private Transform ragdollPrefab;

	[SerializeField]
	private Transform originalRootBone;

	private HealthSystem healthSystem;

	private HitReactionSystem hitReactionSystem;

	private void Awake()
	{
		healthSystem = GetComponent<HealthSystem>();
		hitReactionSystem = GetComponent<HitReactionSystem>();
		healthSystem.OnUnitDeath += HealthSystem_OnUnitDeath;
	}

	private void HealthSystem_OnUnitDeath(object sender, EventArgs empty)
	{
		Instantiate(ragdollPrefab, transform.position, transform.rotation).GetComponent<UnitRagdoll>().Setup(originalRootBone, hitReactionSystem.GetHitDirection(), hitReactionSystem.GetHitForce());
	}
}
