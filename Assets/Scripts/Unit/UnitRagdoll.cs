using UnityEngine;

public class UnitRagdoll : MonoBehaviour
{
	[SerializeField]
	private Transform ragdollRootBone;

	public void Setup(Transform originalRootBone, Vector3 hitDirection, float hitForce)
	{
		MatchAllChildTransforms(originalRootBone, ragdollRootBone);
		ApplyKnockbackToRagdoll(ragdollRootBone, hitDirection, hitForce);
	}

	private void MatchAllChildTransforms(Transform root, Transform clone)
	{
		foreach (Transform child in root)
		{
			Transform match = clone.Find(child.name);
			if (match != null)
			{
				match.position = child.position;
				match.rotation = child.rotation;
				MatchAllChildTransforms(child, match);
			}
		}
	}

	private void ApplyKnockbackToRagdoll(Transform root, Vector3 hitDirection, float hitForce)
	{
		foreach (Transform child in root)
		{
			if (child.TryGetComponent<Rigidbody>(out var rb))
			{
				rb.AddForce(hitDirection * hitForce);
			}
			ApplyKnockbackToRagdoll(child, hitDirection, hitForce);
		}
	}
}
