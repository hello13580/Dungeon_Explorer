using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;
    private AOEAction aoeAction;
    private IceOrbAction iceOrbAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
        aoeAction = GetComponentInParent<AOEAction>();
        iceOrbAction = GetComponentInParent<IceOrbAction>();
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

}
