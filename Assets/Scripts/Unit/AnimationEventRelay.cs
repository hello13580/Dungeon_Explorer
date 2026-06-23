using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;
    private AOEAction aoeAction;
    private IceOrbAction iceOrbAction;
    private PersistentAOEAction persistentAOEAction;
    private LeapAction leapAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
        aoeAction = GetComponentInParent<AOEAction>();
        iceOrbAction = GetComponentInParent<IceOrbAction>();
        persistentAOEAction = GetComponentInParent<PersistentAOEAction>();
        leapAction = GetComponentInParent<LeapAction>();
    }

    public void ShootArrow()
    {
        bowAction?.ShootArrow();
    }

    public void ThrowGrenade()
    {
        aoeAction?.ThrowGrenade();
    }

    public void ShootIceOrb()
    {
        iceOrbAction.ShootOrb();
    }

    public void SpawnFireZone()
    {
        persistentAOEAction?.SpawnZoneFromAnimation();
    }

    // JumpStart 애니메이션 이벤트에서 호출 — 이 시점부터 실제 점프 이동 시작
    public void StartLeapMovement()
    {
        leapAction?.StartLeapMovement();
    }
}
