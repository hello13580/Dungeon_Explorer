using System;
using System.Collections.Generic;
using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    [SerializeField] private int damage = 20;
    [SerializeField] private float hitForce = 3000f;
    [SerializeField] private Transform grenadeExplodeVFXPrefab;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private AnimationCurve arcYAnimationCurve; // 포물선 높이를 조절하는 커브

    private AOEAction aoeAction;
    private Vector3 targetPosition;
    private float moveSpeed = 25f;
    private int damageRadius;

    private float totalDistance;
    private float spawnY;
    private Vector3 positionXZ; // 높이를 제외한 X, Z 축의 현재 위치

    private Action onGrenadeBehaviourComplete;
    public static event EventHandler onAnyGrenadeAction;

    private void Update()
    {
        MoveToTarget();
    }

    private void MoveToTarget()
    {
        // 1. XZ 평면상에서의 이동 방향 및 위치 계산
        Vector3 moveDir = (targetPosition - positionXZ).normalized;
        positionXZ += moveDir * moveSpeed * Time.deltaTime;

        // 2. 전체 이동 거리 중 현재 진행률(0~1) 계산
        float distanceToTarget = Vector3.Distance(positionXZ, new Vector3(targetPosition.x, positionXZ.y, targetPosition.z));
        float moveProgress = 1f - (distanceToTarget / totalDistance);

        // 3. 포물선 높이 계산
        // Lerp로 기본 높이를 맞추고, AnimationCurve를 더해 "점프"하는 느낌을 줌
        float currentHeight = Mathf.Lerp(spawnY, targetPosition.y, moveProgress);
        float arcHeight = arcYAnimationCurve.Evaluate(moveProgress);

        transform.position = new Vector3(positionXZ.x, currentHeight + arcHeight, positionXZ.z);

        // 4. 타겟 지점 도달 판정 (오차 범위 0.2f)
        float reachingDistance = 0.2f;
        if (Vector3.Distance(positionXZ, new Vector3(targetPosition.x, positionXZ.y, targetPosition.z)) < reachingDistance)
        {
            Explode();
        }
    }

    private void Explode()
    {
        GridPosition targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);

        // AOEAction에 정의된 범위 내 그리드 리스트 가져오기
        List<GridPosition> affectedGridPositions = aoeAction.GetDamageAffectedGridPosition(targetGridPosition, damageRadius);

        HashSet<Unit> unitSet = new HashSet<Unit>(); // 중복 데미지 방지용

        foreach (GridPosition gridPos in affectedGridPositions)
        {
            List<Unit> unitListAtPosition = LevelGrid.Instance.GetUnitListAtGridPosition(gridPos);
            foreach (Unit targetUnit in unitListAtPosition)
            {
                if (!unitSet.Contains(targetUnit))
                {
                    unitSet.Add(targetUnit);

                    // 넉백 방향 계산 (폭발 중심지에서 유닛 방향으로)
                    Vector3 knockbackDir = (targetUnit.transform.position - targetPosition).normalized;
                    targetUnit.GetHitReaction().SetHitDirection(knockbackDir);
                    targetUnit.GetHitReaction().SetHitForce(hitForce);

                    targetUnit.Damage(damage);
                }
            }
        }

        // 잔상(Trail) 처리: 부모를 해제하여 폭발 후에도 연기가 잠시 남게 함
        if (trailRenderer != null)
        {
            trailRenderer.transform.parent = null;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        // 폭발 이펙트 생성
        Instantiate(grenadeExplodeVFXPrefab, transform.position, Quaternion.identity);

        // 스크립트 및 오브젝트 정리
        onAnyGrenadeAction?.Invoke(this, EventArgs.Empty);
        onGrenadeBehaviourComplete?.Invoke();

        Destroy(gameObject);
    }

    public void Setup(GridPosition targetGridPosition, int damageRadius, AOEAction aoeAction, Action onGrenadeBehaviourComplete)
    {
        this.damageRadius = damageRadius;
        this.aoeAction = aoeAction;
        this.onGrenadeBehaviourComplete = onGrenadeBehaviourComplete;

        targetPosition = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
        positionXZ = transform.position;
        spawnY = positionXZ.y;

        // XZ 평면상의 순수 수평 거리 계산
        Vector3 targetPosXZ = targetPosition;
        targetPosXZ.y = spawnY;
        totalDistance = Vector3.Distance(positionXZ, targetPosXZ);

        if (totalDistance <= 0f) totalDistance = 0.01f;
    }
}