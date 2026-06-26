using UnityEngine;

public class UnitAnimationRelay : MonoBehaviour
{
	[SerializeField]
	private MeleeAction meleeAction;

	public void StepForward()
	{
		meleeAction?.StepForward();
	}

	[SerializeField]
	private LeapAction leapAction;

	public void Melee()
	{
		if (meleeAction != null)
			meleeAction.Melee();
		else
			Debug.LogWarning("MeleeAction이 연결되지 않았습니다!");
	}

	/// <summary>공격 애니메이션 마지막 프레임에 애니메이션 이벤트로 호출.</summary>
	public void OnMeleeAnimationComplete()
	{
		meleeAction?.OnMeleeAnimationComplete();
	}

	// JumpStart 애니메이션 이벤트에서 호출 — 이 시점부터 실제 점프 이동 시작
	public void StartLeapMovement()
	{
		if (leapAction != null)
			leapAction.StartLeapMovement();
		else
			Debug.LogWarning("LeapAction이 연결되지 않았습니다!");
	}
}
