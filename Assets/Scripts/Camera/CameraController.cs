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

    // 카메라 이동 가능 범위 (XZ 평면)
    // SetBounds()로 설정하며, hasBounds가 false이면 범위 제한 없음
    private Vector2 boundsMin;
    private Vector2 boundsMax;
    private bool hasBounds = false;

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        // �̱��� ���� ����
        if (Instance != null && Instance != this)
        {
            Debug.LogError("There's more than one CameraController! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // �ʿ� �� ����

        // �ó׸��� ������Ʈ ����
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

        // 카메라가 바라보는 방향 기준으로 이동 (Y축 성분은 무시)
        Vector3 moveDir = transform.forward * inputMoveVector.y + transform.right * inputMoveVector.x;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // 이동 범위 제한 — hasBounds가 true일 때만 XZ를 클램프
        if (hasBounds)
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
            pos.z = Mathf.Clamp(pos.z, boundsMin.y, boundsMax.y);
            transform.position = pos;
        }
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

        // ���� ���̿��� Ÿ�� ���̷� �ε巴�� �̵� (SmoothDamp)
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

    /// <summary>
    /// 스테이지 시작 시 StageManager에서 호출.
    /// 카메라 위치·방향을 설정하고 initialAngle도 함께 갱신한다.
    ///
    /// initialAngle을 갱신하지 않으면 리셋키(HandleReturnToInitial)를 눌렀을 때
    /// 이전 스테이지나 씬 초기 각도로 돌아가는 버그가 생긴다.
    /// </summary>
    public void SetStageStart(Vector3 position, Vector3 rotation)
    {
        transform.position    = position;
        transform.eulerAngles = rotation;
        initialAngle          = rotation; // 리셋키 기준 각도도 이 스테이지 시작 각도로 교체
    }

    /// <summary>
    /// 카메라 이동 가능 범위를 설정한다. StageManager에서 스테이지 로드 시 호출.
    /// min·max 모두 Vector2(월드X, 월드Z) 형식.
    /// </summary>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        boundsMin = min;
        boundsMax = max;
        hasBounds = true;
    }
}