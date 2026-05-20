using System;
using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public class OnBulletHitEventArgs : EventArgs
    {
        public Vector3 hitPosition;
        public float hitForce;
    }

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform arrowHitVFXPrefab;

    private Vector3 targetPosition;
    private float arrowSpeed = 100f;
    private float hitForce = 1000f;
    private bool hasHit = false;

    public event EventHandler<OnBulletHitEventArgs> OnArrowHit;

    public void Setup(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
    }

    private void Update()
    {
        if (hasHit) return;

        float moveStep = arrowSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveStep);

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            hasHit = true;

            OnArrowHit?.Invoke(this, new OnBulletHitEventArgs
            {
                hitPosition = targetPosition,
                hitForce = hitForce
            });

            Instantiate(arrowHitVFXPrefab, targetPosition, Quaternion.identity);

            if (trailRenderer != null)
            {
                trailRenderer.transform.parent = null;
                Destroy(trailRenderer.gameObject, trailRenderer.time);
            }

            Destroy(gameObject);
        }
    }
}
