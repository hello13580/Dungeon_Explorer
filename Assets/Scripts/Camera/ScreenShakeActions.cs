using System;
using UnityEngine;

public class ScreenShakeActions : MonoBehaviour
{
	private void Start()
	{
		ShootAction.OnAnyShooting += ShootAction_OnAnyShooting;
		GrenadeProjectile.onAnyGrenadeAction += AOEAction_onAnyAOEAction;
	}

	private void ShootAction_OnAnyShooting(object sender, ShootAction.OnShootEventArgs e)
	{
		ScreenShake.Instance.Shake();
	}

	private void AOEAction_onAnyAOEAction(object sender, EventArgs empty)
	{
		ScreenShake.Instance.Shake(0.7f);
	}
}
