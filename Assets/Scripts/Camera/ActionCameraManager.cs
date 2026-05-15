using System;
using UnityEngine;

public class ActionCameraManager : MonoBehaviour
{
    [SerializeField] private GameObject actionCameraGameObject;

    private void Start()
    {
        HideActionCamera();

        // 모든 액션의 시작과 종료 이벤트 구독
        BaseAction.OnAnyActionStarted += BaseAction_OnAnyActionStarted;
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
    }

    private void ShowActionCamera()
    {
        actionCameraGameObject.SetActive(true);
    }

    private void HideActionCamera()
    {
        actionCameraGameObject.SetActive(false);
    }

    private void BaseAction_OnAnyActionStarted(object sender, EventArgs empty)
    {
        // 1. ShootAction일 때만 카메라 연출 실행
        if (sender is ShootAction shootAction)
        {
            Unit shootingUnit = shootAction.GetUnit();
            Unit targetUnit = shootAction.GetTargetUnit();

            // 카메라 높이 설정 (유닛 콜라이더 중심 높이의 1.5배 정도)
            float shoulderHeight = shootingUnit.GetCollider().bounds.center.y * 1.5f;
            Vector3 cameraHeightOffset = Vector3.up * shoulderHeight;

            // 사격 방향 계산
            Vector3 shootDir = (targetUnit.GetWorldPosition() - shootingUnit.GetWorldPosition()).normalized;

            // 카메라를 옆으로 살짝 비끼게 배치 (숄더샷 느낌)
            float shoulderOffsetAmount = 0.5f;
            Vector3 shoulderOffset = Quaternion.Euler(0, 90, 0) * shootDir * shoulderOffsetAmount;

            // 최종 카메라 위치: 유닛 위치 + 높이 오프셋 + 옆 오프셋 + 뒤로 살짝(-1f)
            Vector3 cameraPosition = shootingUnit.GetWorldPosition() + cameraHeightOffset + shoulderOffset + (shootDir * -1f);

            actionCameraGameObject.transform.position = cameraPosition;

            // 타겟의 유닛 높이(어깨 선)를 바라보게 설정
            actionCameraGameObject.transform.LookAt(targetUnit.GetWorldPosition() + cameraHeightOffset);

            ShowActionCamera();
        }
        // MoveAction이나 SpinAction 등 다른 액션은 필요 시 여기에 추가 로직 작성
    }

    private void BaseAction_OnAnyActionEnded(object sender, EventArgs empty)
    {
        // ShootAction이 끝나면 카메라 숨기기
        if (sender is ShootAction)
        {
            HideActionCamera();
        }
    }

    // 메모리 누수 방지를 위한 이벤트 해제 (권장 사항)
    private void OnDestroy()
    {
        BaseAction.OnAnyActionStarted -= BaseAction_OnAnyActionStarted;
        BaseAction.OnAnyActionEnded -= BaseAction_OnAnyActionEnded;
    }
}