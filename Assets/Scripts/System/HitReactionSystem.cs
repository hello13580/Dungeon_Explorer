using UnityEngine;

public class HitReactionSystem : MonoBehaviour
{
	private Vector3 hitDirection;

	private float hitForce;

	public Vector3 GetHitDirection()
	{
		return hitDirection;
	}

	public void SetHitDirection(Vector3 hitDirection)
	{
		this.hitDirection = hitDirection;
	}

	public float GetHitForce()
	{
		return hitForce;
	}

	public void SetHitForce(float hitForce)
	{
		this.hitForce = hitForce;
	}
}
