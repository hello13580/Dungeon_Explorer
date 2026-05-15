using System;
using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public class OnBulletHitEventArgs : EventArgs
    {
        public Vector3 hitPosition;
        public float hitForce;
    }

    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private Transform bulletHitVFXPrefab;

    private Vector3 targetPosition;
    private float bulletSpeed = 150f;
    private float hitForce = 1000f;

    public event EventHandler<OnBulletHitEventArgs> OnBulletHit;

    public void Setup(Vector3 targetPosition)
    {
        this.targetPosition = targetPosition;
    }

    private void Update()
    {
        // 이동 거리 계산
        float moveStep = bulletSpeed * Time.deltaTime;

        // 타겟 위치로 이동
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveStep);

        // 타겟에 도달했는지 확인
        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            // 히트 이벤트 발생 (데미지 처리 등)
            OnBulletHit?.Invoke(this, new OnBulletHitEventArgs
            {
                hitPosition = targetPosition,
                hitForce = hitForce
            });

            // 피격 이펙트(VFX) 생성
            Instantiate(bulletHitVFXPrefab, targetPosition, Quaternion.identity);

            // 트레일 잔상이 끊기지 않도록 부모 해제 후 별도 파괴 처리
            if (trailRenderer != null)
            {
                trailRenderer.transform.parent = null;
                // TrailRenderer가 붙은 객체는 일정 시간 뒤에 사라지도록 설정 (잔상 시간 고려)
                Destroy(trailRenderer.gameObject, trailRenderer.time);
            }

            // 총알 오브젝트 파괴
            Destroy(gameObject);
        }
    }
}