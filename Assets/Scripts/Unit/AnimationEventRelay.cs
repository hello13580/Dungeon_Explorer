using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private BowAction bowAction;
    private PiercingArrowAction piercingArrowAction;
    private AOEAction aoeAction;
    private IceOrbAction iceOrbAction;
    private LightOrbAction lightOrbAction;
    private HolyBurstAction holyBurstAction;
    private PersistentAOEAction persistentAOEAction;
    private BlizzardAction blizzardAction;
    private LeapAction leapAction;

    private void Awake()
    {
        bowAction = GetComponentInParent<BowAction>();
        piercingArrowAction = GetComponentInParent<PiercingArrowAction>();
        aoeAction = GetComponentInParent<AOEAction>();
        iceOrbAction = GetComponentInParent<IceOrbAction>();
        lightOrbAction = GetComponentInParent<LightOrbAction>();
        holyBurstAction = GetComponentInParent<HolyBurstAction>();
        persistentAOEAction = GetComponentInParent<PersistentAOEAction>();
        blizzardAction = GetComponentInParent<BlizzardAction>();
        leapAction = GetComponentInParent<LeapAction>();
    }

    // BowAction과 PiercingArrowAction은 같은 활쏘기 애니메이션 클립을 공유하므로
    // 같은 애니메이션 이벤트(ShootArrow)에서 둘 다 호출해본다. 유닛에 붙어있는 쪽만 실제로 반응한다.
    public void ShootArrow()
    {
        bowAction?.ShootArrow();
        piercingArrowAction?.ShootArrow();
    }

    public void ThrowGrenade()
    {
        aoeAction?.ThrowGrenade();
    }

    // IceOrbAction·LightOrbAction·HolyBurstAction은 같은 시전 애니메이션 클립을 공유하므로
    // 같은 애니메이션 이벤트에서 전부 호출해본다. 유닛에 붙어있는 쪽만 실제로 반응한다.
    public void ShootIceOrb()
    {
        iceOrbAction?.ShootOrb();
        lightOrbAction?.ShootOrb();
        holyBurstAction?.ShootOrb();
    }

    public void SpawnFireZone()
    {
        persistentAOEAction?.SpawnZoneFromAnimation();
    }

    public void SpawnBlizzard()
    {
        blizzardAction?.SpawnBlizzardFromAnimation();
    }

    // JumpStart 애니메이션 이벤트에서 호출 — 이 시점부터 실제 점프 이동 시작
    public void StartLeapMovement()
    {
        leapAction?.StartLeapMovement();
    }
}
