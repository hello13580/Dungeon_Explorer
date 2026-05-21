using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;
    private AOEAction aoeAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
        aoeAction = GetComponentInParent<AOEAction>();
    }

    public void ShootArrow()
    {
        bowAction?.ShootArrow();
    }

    public void ThrowGrenade()
    {
        aoeAction?.ThrowGrenade();
    }
    
}
