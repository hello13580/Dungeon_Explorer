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

        if (TryGetComponent<PiercingArrowAction>(out var piercingArrowAction))
        {
            // BowAction과 같은 활쏘기 모션을 그대로 재사용 — 동일한 트리거(isDrawing)를 쏜다.
            piercingArrowAction.OnStartDrawing += (s, e) => unitAnimator.SetTrigger("isDrawing");
        }
        if (TryGetComponent<MeleeAction>(out var meleeAction))
		{
			meleeAction.OnSwordActionStarted += MeleeAction_OnSwordActionStarted;
			meleeAction.OnSwordActionEnded += MeleeAction_OnSwordActionEnded;
        }

        if (TryGetComponent<AOEAction>(out var aoeAction))
        {
			aoeAction.OnAOEActionStarted += AOEAction_OnAOEActionStarted;
        }

        if (TryGetComponent<HealAction>(out var healAction))
        {
			healAction.OnHeal += healAction_OnHeal;
        }

        if (TryGetComponent<BarrierAction>(out var barrierAction))
        {
            barrierAction.OnBarrier += BarrierAction_OnBarrier;
        }

        if (TryGetComponent<IceOrbAction>(out var iceOrbAction))
        {
            iceOrbAction.OnStartShooting += iceOrbAction_OnStartShooting;
        }

        if (TryGetComponent<PersistentAOEAction>(out var persistentAOEAction))
        {
            persistentAOEAction.OnCastStarted += PersistentAOEAction_OnCastStarted;
        }

        if (TryGetComponent<TeleportAction>(out var teleportAction))
        {
            teleportAction.OnTeleportCompleted += TeleportAction_OnTeleportCompleted;
        }

        if (TryGetComponent<DashAttackAction>(out var dashAttackAction))
        {
            dashAttackAction.OnDashMoveStarted  += DashAttackAction_OnDashMoveStarted;
            dashAttackAction.OnDashMoveEnded    += DashAttackAction_OnDashMoveEnded;
            dashAttackAction.OnDashAttackStarted += DashAttackAction_OnDashAttackStarted;
        }

        if (TryGetComponent<LeapAction>(out var leapAction))
        {
            leapAction.OnLeapStarted  += (s, e) => unitAnimator.SetTrigger("JumpStart");
            leapAction.OnLeapLanding  += (s, e) => unitAnimator.SetTrigger("JumpEnd");
        }

        if (TryGetComponent<SelfAttackBuffAction>(out var selfAttackBuffAction))
        {
            selfAttackBuffAction.OnSelfBuffStarted += SelfAttackBuffAction_OnSelfBuffStarted;
        }

        if (TryGetComponent<SelfAttackStackBuffAction>(out var selfAttackStackBuffAction))
        {
            selfAttackStackBuffAction.OnSelfBuffStackStarted += SelfAttackStackBuffAction_OnSelfBuffStackStarted;
        }
    }

    private void SelfAttackBuffAction_OnSelfBuffStarted(object sender, EventArgs e)
    {
        unitAnimator.SetTrigger("isSelfBuff");
    }

    private void SelfAttackStackBuffAction_OnSelfBuffStackStarted(object sender, EventArgs e)
    {
        unitAnimator.SetTrigger("isSelfBuffStack");
    }

    private IceOrbAction iceOrbAction;

    private void iceOrbAction_OnStartShooting(object sender, IceOrbAction.OnShootEventArgs e)
    {
        unitAnimator.SetTrigger("isIceOrb");
    }

    /// <summary>
    /// 애니메이션 이벤트에서 직접 호출. 이 시점에 얼음 구체가 발사됨.
    /// </summary>
    public void FireIceOrb()
    {
        if (TryGetComponent<IceOrbAction>(out var action))
            action.ShootOrb();
    }

    private void TeleportAction_OnTeleportCompleted(object sender, GridPosition e)
    {
        unitAnimator.SetTrigger("isTeleport");
    }

    private void PersistentAOEAction_OnCastStarted(object sender, EventArgs e)
    {
        unitAnimator.SetTrigger("isFireZone");
    }

    /// <summary>
    /// 애니메이션 이벤트에서 직접 호출. 이 시점에 불꽃 장판이 생성됨.
    /// </summary>
    public void SpawnFireZone()
    {
        if (TryGetComponent<PersistentAOEAction>(out var action))
            action.SpawnZoneFromAnimation();
    }

    private void BarrierAction_OnBarrier(object sender, BarrierAction.OnBarrierEventArgs e)
    {
		unitAnimator.SetTrigger("isBarriering");
		Debug.Log("�踮�� ����");
    }

    private void healAction_OnHeal(object sender, HealAction.OnHealEventArgs e)
    {
        unitAnimator.SetTrigger("isHealing");
    }

    private void AOEAction_OnAOEActionStarted(object sender, EventArgs e)
    {
        unitAnimator.SetTrigger("isThrowing");
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

	// 돌진 이동 중 달리기 애니메이션 — IsWalking bool 재활용 (별도 파라미터 원하면 변경)
	private void DashAttackAction_OnDashMoveStarted(object sender, EventArgs e)
	{
		unitAnimator.SetBool("IsWalking", true);
	}

	private void DashAttackAction_OnDashMoveEnded(object sender, EventArgs e)
	{
		unitAnimator.SetBool("IsWalking", false);
	}

	// 돌진 공격 시점 — SwordSlash 트리거 재활용 (별도 파라미터 원하면 변경)
	private void DashAttackAction_OnDashAttackStarted(object sender, EventArgs e)
	{
		unitAnimator.SetTrigger("SwordSlash");
	}

	private void ShootAction_OnStartShooting(object sender, ShootAction.OnShootEventArgs shootEventArgs)
	{
		unitAnimator.SetTrigger("Shoot");
	}
}
