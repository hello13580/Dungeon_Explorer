using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private bool canCameraMove = true;
    private CinemachineFollow follow;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float maxZoom = 30f;
    [SerializeField] private float minZoom = 1f;

    private float targetZoom;
    private float initialZoom = 15f;
    private Vector3 initialAngle;
    private float zoomVelocity;

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 로직 정리
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There's more than one CameraController! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // 필요 시 유지

        // 시네마신 컴포넌트 참조
        if (cinemachineCamera != null)
        {
            follow = cinemachineCamera.GetComponent<CinemachineFollow>();
        }

        initialAngle = transform.eulerAngles;
        targetZoom = initialZoom;

        if (follow != null)
        {
            follow.FollowOffset = new Vector3(follow.FollowOffset.x, initialZoom, follow.FollowOffset.z);
        }
    }

    private void Update()
    {
        if (canCameraMove)
        {
            HandleCameraMove();
            HandleCameraRotate();
            HandleSmoothZoom();
            HandleReturnToInitial();
        }
    }

    public void EnableCameraMove() => canCameraMove = true;
    public void DisableCameraMove() => canCameraMove = false;

    private void HandleCameraMove()
    {
        Vector2 inputMoveVector = InputManager.Instance.GetCameraMoveVector();
        
        // 카메라의 전방과 우측 방향을 기준으로 이동 (Y축 영향 배제)
        Vector3 moveDir = transform.forward * inputMoveVector.y + transform.right * inputMoveVector.x;
        
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void HandleCameraRotate()
    {
        Vector3 rotationVector = Vector3.zero;
        rotationVector.y = InputManager.Instance.GetCameraRotateAmount();
        
        transform.eulerAngles += rotationVector * rotationSpeed * Time.deltaTime;
    }

    private void HandleSmoothZoom()
    {
        if (follow == null) return;

        float zoomInput = InputManager.Instance.GetCameraZoomAmount();
        if (zoomInput != 0f)
        {
            targetZoom -= zoomInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // 현재 높이에서 타겟 높이로 부드럽게 이동 (SmoothDamp)
        Vector3 currentOffset = follow.FollowOffset;
        float newHeight = Mathf.SmoothDamp(currentOffset.y, targetZoom, ref zoomVelocity, smoothTime);
        
        follow.FollowOffset = new Vector3(currentOffset.x, newHeight, currentOffset.z);
    }

    private void HandleReturnToInitial()
    {
        if (InputManager.Instance.IsCameraResetKeyDown())
        {
            targetZoom = initialZoom;
            transform.eulerAngles = initialAngle;
        }
    }

    public float GetCameraHeight()
    {
        return follow != null ? follow.FollowOffset.y : 0f;
    }
}