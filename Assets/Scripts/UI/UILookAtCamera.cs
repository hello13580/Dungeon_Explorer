using UnityEngine;

public class UILookAtCamera : MonoBehaviour
{
    [Tooltip("체력바 등이 거꾸로 보일 경우 체크하세요.")]
    [SerializeField] private bool invert;

    private Transform cameraTransform;

    private void Awake()
    {
        // 메인 카메라의 트랜스폼 참조 (Camera.main은 내부적으로 호출 비용이 있으므로 캐싱 권장)
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        if (invert)
        {
            // 카메라가 바라보는 방향의 반대 방향을 계산
            Vector3 dirToCamera = cameraTransform.position - transform.position;

            // 현재 위치에서 카메라 반대 방향에 있는 가상의 지점을 바라보게 함 (결과적으로 뒤로 도는 효과)
            transform.LookAt(transform.position - dirToCamera);
        }
        else
        {
            // 단순히 카메라 위치를 바라봄
            transform.LookAt(cameraTransform);
        }
    }
}