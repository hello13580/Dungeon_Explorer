using System;
using UnityEngine;

public class IceOrbProjectile : MonoBehaviour
{
    public class OnHitEventArgs : EventArgs
    {
        public Vector3 hitPosition;
    }

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform hitVFXPrefab;
    [SerializeField] private float projectileSpeed = 20f;

    private Vector3 targetPosition;
    private Unit targetUnit;
    private int damage;
    private float slowValue;
    private int slowDuration;

    public event EventHandler<OnHitEventArgs> OnHit;

    public void Setup(Vector3 targetPosition, Unit targetUnit, int damage, float slowValue, int slowDuration)
    {
        this.targetPosition = targetPosition;
        this.targetUnit = targetUnit;
        this.damage = damage;
        this.slowValue = slowValue;
        this.slowDuration = slowDuration;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, projectileSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            Hit();
    }

    private void Hit()
    {
        if (hitVFXPrefab != null)
            Instantiate(hitVFXPrefab, targetPosition, Quaternion.identity);

        if (targetUnit != null)
        {
            // 피해
            Vector3 hitDir = (targetUnit.GetWorldPosition() - transform.position).normalized;
            targetUnit.GetHitReaction().SetHitDirection(hitDir);
            targetUnit.GetHitReaction().SetHitForce(300f);
            targetUnit.Damage(damage);

            // 둔화 디버프 (ExtendDuration — 중복 적용 시 지속 턴 누적)
            StatusEffectSystem ses = targetUnit.GetComponent<StatusEffectSystem>();
            if (ses != null)
            {
                ses.AddEffect(new StatusEffect(
                    StatusEffectType.MovementReduce,
                    slowValue,
                    slowDuration,
                    "둔화",
                    StackingMode.ExtendDuration
                ));
            }
        }

        OnHit?.Invoke(this, new OnHitEventArgs { hitPosition = targetPosition });

        if (trailRenderer != null)
        {
            trailRenderer.transform.parent = null;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        Destroy(gameObject);
    }
}
