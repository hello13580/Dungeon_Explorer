using System;
using System.Collections.Generic;
using UnityEngine;

public class GrenadeProjectile : MonoBehaviour
{
    [SerializeField] private int damage = 20;
    [SerializeField] private float hitForce = 3000f;
    [SerializeField] private Transform grenadeExplodeVFXPrefab;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private AnimationCurve arcYAnimationCurve; // ������ ���̸� �����ϴ� Ŀ��

    private AOEAction aoeAction;
    private Vector3 targetPosition;
    private float moveSpeed = 25f;
    private int damageRadius;

    private float totalDistance;
    private float spawnY;
    private Vector3 positionXZ; // ���̸� ������ X, Z ���� ���� ��ġ

    private Action onGrenadeBehaviourComplete;
    public static event EventHandler onAnyGrenadeAction;

    private void Update()
    {
        MoveToTarget();
    }

    private void MoveToTarget()
    {
        // 1. XZ ���󿡼��� �̵� ���� �� ��ġ ���
        Vector3 moveDir = (targetPosition - positionXZ).normalized;
        positionXZ += moveDir * moveSpeed * Time.deltaTime;

        // 2. ��ü �̵� �Ÿ� �� ���� �����(0~1) ���
        float distanceToTarget = Vector3.Distance(positionXZ, new Vector3(targetPosition.x, positionXZ.y, targetPosition.z));
        float moveProgress = 1f - (distanceToTarget / totalDistance);

        // 3. ������ ���� ���
        // Lerp�� �⺻ ���̸� ���߰�, AnimationCurve�� ���� "����"�ϴ� ������ ��
        float currentHeight = Mathf.Lerp(spawnY, targetPosition.y, moveProgress);
        float arcHeight = arcYAnimationCurve.Evaluate(moveProgress);

        transform.position = new Vector3(positionXZ.x, currentHeight + arcHeight, positionXZ.z);

        // 4. Ÿ�� ���� ���� ���� (���� ���� 0.2f)
        float reachingDistance = 0.2f;
        if (Vector3.Distance(positionXZ, new Vector3(targetPosition.x, positionXZ.y, targetPosition.z)) < reachingDistance)
        {
            Explode();
        }
    }

    private void Explode()
    {

        GridPosition targetGridPosition = LevelGrid.Instance.GetGridPosition(targetPosition);

        // AOEAction�� ���ǵ� ���� �� �׸��� ����Ʈ ��������
        List<GridPosition> affectedGridPositions = aoeAction.GetDamageAffectedGridPosition(targetGridPosition, damageRadius);

        HashSet<Unit> unitSet = new HashSet<Unit>(); // �ߺ� ������ ������

        foreach (GridPosition gridPos in affectedGridPositions)
        {
            List<Unit> unitListAtPosition = new List<Unit>(LevelGrid.Instance.GetUnitListAtGridPosition(gridPos));
            foreach (Unit targetUnit in unitListAtPosition)
            {
                if (!unitSet.Contains(targetUnit))
                {
                    unitSet.Add(targetUnit);

                    // �˹� ���� ��� (���� �߽������� ���� ��������)
                    Vector3 knockbackDir = (targetUnit.transform.position - targetPosition).normalized;
                    targetUnit.GetHitReaction().SetHitDirection(knockbackDir);
                    targetUnit.GetHitReaction().SetHitForce(hitForce);

                    targetUnit.Damage(damage);
                }
            }
        }

        // �ܻ�(Trail) ó��: �θ� �����Ͽ� ���� �Ŀ��� ���Ⱑ ��� ���� ��
        if (trailRenderer != null)
        {
            trailRenderer.transform.parent = null;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        // ���� ����Ʈ ����
       Instantiate(grenadeExplodeVFXPrefab, transform.position, Quaternion.identity);

        // ��ũ��Ʈ �� ������Ʈ ����
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

        // XZ ������ ���� ���� �Ÿ� ���
        Vector3 targetPosXZ = targetPosition;
        targetPosXZ.y = spawnY;
        totalDistance = Vector3.Distance(positionXZ, targetPosXZ);

        if (totalDistance <= 0f) totalDistance = 0.01f;
    }
}