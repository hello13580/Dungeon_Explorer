using System;
using UnityEngine;

public class UnitAnimator : MonoBehaviour
{
	[SerializeField]
	private Animator unitAnimator;

	private Vector3 hitPosition;

	private void Awake()
	{
		if (TryGetComponent<MoveAction>(out var moveAction))
		{
			moveAction.OnStartMoving += MoveAction_OnStartMoving;
			moveAction.OnStopMoving += MoveAction_OnStopMoving;
			moveAction.OnChangeFloorsStarted += moveAction_OnChangeFloorsStarted;
		}
		if (TryGetComponent<ShootAction>(out var shootAction))
		{
			shootAction.OnStartShooting += ShootAction_OnStartShooting;
		}

        if (TryGetComponent<BowAction>(out var bowAction))
        {
            bowAction.OnStartDrawing += BowAction_OnStartShooting;
        }
        if (TryGetComponent<MeleeAction>(out var meleeAction))
		{
			meleeAction.OnSwordActionStarted += MeleeAction_OnSwordActionStarted;
			meleeAction.OnSwordActionEnded += MeleeAction_OnSwordActionEnded;
        }
	}

    private void BowAction_OnStartShooting(object sender, BowAction.OnShootEventArgs e)
    {
		unitAnimator.SetTrigger("isDrawing");
    }

    private void MeleeAction_OnSwordActionEnded(object sender, EventArgs e)
    {
        //unitAnimator.applyRootMotion = false;
    }

    private void moveAction_OnChangeFloorsStarted(object sender, MoveAction.OnChangeFloorStartedEventArgs e)
	{
		if (e.unitGridPosition.floor <= e.targetPosition.floor)
		{
			unitAnimator.SetTrigger("JumpUp");
		}
		else
		{
			unitAnimator.SetTrigger("JumpDown");
		}
	}

	private void MeleeAction_OnSwordActionStarted(object sender, EventArgs e)
	{
		//unitAnimator.applyRootMotion= true;
        unitAnimator.SetTrigger("SwordSlash");
	}

	private void MoveAction_OnStartMoving(object sender, EventArgs empty)
	{
		unitAnimator.SetBool("IsWalking", true);
	}

	private void MoveAction_OnStopMoving(object sender, EventArgs empty)
	{
		unitAnimator.SetBool("IsWalking", false);
	}

	private void ShootAction_OnStartShooting(object sender, ShootAction.OnShootEventArgs shootEventArgs)
	{
		unitAnimator.SetTrigger("Shoot");
	}
}
