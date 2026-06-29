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

    [Header("턴 이동")]
    [SerializeField] private bool moveOnPlayerTurn = true;
    // 스프링 강도 — 클수록 빠르게 도달
    [SerializeField] private float springStrength = 90f;
    // 감쇠 계수 — 2*sqrt(springStrength)이면 임계감쇠(반동 없음), 그보다 작으면 약간 오버슈트
    [SerializeField] private float springDamping = 16f;

    private float targetZoom;
    private float initialZoom = 15f;
    private Vector3 initialAngle;
    private float zoomVelocity;

    // 스프링 이동용
    private Vector3 springTarget;
    private Vector3 springVelocity;
    private bool springActive = false;

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
            follow.FollowOffset = new Vector3(follow.FollowOffset.x, initialZoom, follow.FollowOffset.z);
    }

    private void Start()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    private void TurnSystem_OnTurnChanged(object sender, System.EventArgs e)
    {
        if (!TurnSystem.Instance.IsPlayerTurn())
        {
            // [버그 수정] 적 턴 시작 시 스프링을 즉시 취소한다.
            //   플레이어 턴에 시작된 스프링이 완전히 감쇠되기 전에 적 턴이 시작되면,
            //   잔여 velocity가 적 행동 중에도 카메라를 계속 밀어 화면이 흔들리는 버그가 있었다.
            springActive = false;
            springVelocity = Vector3.zero;
            return;
        }

        if (!moveOnPlayerTurn) return;

        Unit turnUnit = TurnSystem.Instance.GetTurnUnit();
        if (turnUnit == null) return;

        MoveToPosition(turnUnit.GetWorldPosition());
    }

    /// <summary>
    /// 카메라를 지정 월드 좌표 XZ로 스프링 물리를 사용해 부드럽게 이동시킨다.
    /// Y(높이)는 현재 값을 유지하며, springStrength / springDamping 으로 감도와 반동을 조절한다.
    /// </summary>
    public void MoveToPosition(Vector3 worldPos)
    {
        // Y는 현재 카메라 높이를 그대로 유지하고 XZ만 목표로 설정한다.
        // 매번 velocity를 초기화해 이전 이동의 관성이 누적되지 않도록 한다.
        springTarget = new Vector3(worldPos.x, transform.position.y, worldPos.z);
        springVelocity = Vector3.zero;
        springActive = true;
    }

    private void HandleSpringMove()
    {
        if (!springActive) return;

        // 감쇠 스프링 시뮬레이션: F = k*(target-pos) - b*velocity
        //   springStrength(k): 클수록 빠르게 목표에 도달
        //   springDamping(b):  2*sqrt(k)보다 작으면 목표를 살짝 지나쳤다 돌아오는 오버슈트 발생
        //                      2*sqrt(k)이면 임계감쇠(반동 없음), 그보다 크면 과감쇠(느리게 수렴)
        Vector3 displacement = springTarget - transform.position;
        Vector3 acceleration = displacement * springStrength - springVelocity * springDamping;
        springVelocity += acceleration * Time.deltaTime;
        transform.position += springVelocity * Time.deltaTime;

        // 목표 근처에서 진동하지 않도록 임계값 이하이면 스냅 후 종료
        if (displacement.sqrMagnitude < 0.0001f && springVelocity.sqrMagnitude < 0.0001f)
        {
            transform.position = springTarget;
            springActive = false;
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
        HandleSpringMove();
    }

    public void EnableCameraMove() => canCameraMove = true;
    public void DisableCameraMove() => canCameraMove = false;

    private void HandleCameraMove()
    {
        Vector2 inputMoveVector = InputManager.Instance.GetCameraMoveVector();

        // 플레이어가 직접 카메라를 움직이면 스프링 이동 취소
        if (inputMoveVector != Vector2.zero)
            springActive = false;

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

        // 스테이지 시작 전 휠 조작으로 변경된 줌도 초기값으로 리셋
        targetZoom = initialZoom;
        if (follow != null)
            follow.FollowOffset = new Vector3(follow.FollowOffset.x, initialZoom, follow.FollowOffset.z);
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