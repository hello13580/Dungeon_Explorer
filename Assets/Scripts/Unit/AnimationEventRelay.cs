using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;
    private AOEAction aoeAction;
    private IceOrbAction iceOrbAction;
    private PersistentAOEAction persistentAOEAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
        aoeAction = GetComponentInParent<AOEAction>();
        iceOrbAction = GetComponentInParent<IceOrbAction>();
        persistentAOEAction = GetComponentInParent<PersistentAOEAction>();
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

}
