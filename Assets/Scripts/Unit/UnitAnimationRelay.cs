using UnityEngine;

public class UnitAnimationRelay : MonoBehaviour
{
	[SerializeField]
	private MeleeAction meleeAction;

	public void Melee()
	{
		if (meleeAction != null)
		{
			meleeAction.Melee();
		}
		else
		{
			Debug.LogWarning("MeleeAction이 연결되지 않았습니다!");
		}
	}
}
