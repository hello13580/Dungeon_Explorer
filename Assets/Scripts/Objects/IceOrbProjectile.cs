using System;
using UnityEngine;

public class IceOrbProjectile : MonoBehaviour
{
    public class OnOrbHitEventArgs : EventArgs
    {
        public Vector3 hitPosition;
    }

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform hitVFXPrefab;

    private Vector3 targetPosition;
    private float orbSpeed = 30f;
    private bool hasHit = false;
    private bool setupCalled = false;

    public event EventHandler<OnOrbHitEventArgs> OnOrbHit;

    public void Setup(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
        setupCalled = true;
    }

    private void Update()
    {
        if (hasHit) return;
        if (!setupCalled) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, orbSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            hasHit = true;

            OnOrbHit?.Invoke(this, new OnOrbHitEventArgs { hitPosition = targetPosition });

            if (hitVFXPrefab != null)
            {
                GameObject vfx = Instantiate(hitVFXPrefab, targetPosition, Quaternion.identity).gameObject;
                Destroy(vfx, 3f);
            }

            if (trailRenderer != null)
            {
                trailRenderer.transform.parent = null;
                Destroy(trailRenderer.gameObject, trailRenderer.time);
            }

            Destroy(gameObject);
        }
    }
}

