using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
    }

    public void ShootArrow()
    {
        bowAction?.ShootArrow();
    }
}
