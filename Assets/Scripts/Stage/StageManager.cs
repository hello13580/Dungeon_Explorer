using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 스테이지 전환 전체 흐름을 관리한다.
/// 씬에 하나만 배치하면 DontDestroyOnLoad로 유지된다.
/// </summary>
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    /// <summary>스테이지 로드가 완전히 완료되고 첫 턴이 시작되기 직전에 발생.</summary>
    public static event EventHandler OnStageLoaded;

    [Header("초기 스테이지 (설정 시 게임 시작과 함께 자동 로드)")]
    [SerializeField] private StageData initialStage;

    [Header("씬 레퍼런스")]
    [Tooltip("씬에 배치된 Directional Light 오브젝트")]
    [SerializeField] private Light directionalLight;

    private GameObject currentMapInstance;

    // 현재 로드된 스테이지 정보 조회용 (외부에서 읽기만 가능)
    public StageData CurrentStageData { get; private set; }
    public bool IsLoading { get; private set; }

    // ─────────────────────────────────────────
    // 생명주기
    // ─────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("StageManager가 두 개 이상입니다! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // CharacterSelectManager가 씬에 있으면 캐릭터 선택 완료 후 StartGame()에서 로드하므로
        // 여기서 자동 로드하면 캐릭터 선택 전에 적만 스폰되어 턴 시스템이 꼬이는 문제가 생긴다.
        // CharacterSelectManager가 없을 때(테스트 등)만 initialStage를 자동 로드한다.
        if (initialStage != null && CharacterSelectManager.Instance == null)
        {
            LoadStage(initialStage);
        }
    }

    // ─────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────

    /// <summary>
    /// 외부(UI 버튼 등)에서 스테이지 전환 시 호출하는 진입점.
    /// </summary>
    public void LoadStage(StageData stageData)
    {
        if (IsLoading)
        {
            Debug.LogWarning("[StageManager] 이미 스테이지 로딩 중입니다.");
            return;
        }
        if (stageData == null)
        {
            Debug.LogError("[StageManager] StageData가 null입니다.");
            return;
        }
        StartCoroutine(LoadStageRoutine(stageData));
    }

    // ─────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────

    private IEnumerator LoadStageRoutine(StageData stageData)
    {
        IsLoading = true;
        CurrentStageData = stageData;
        Debug.Log($"[StageManager] 스테이지 로드 시작: {stageData.stageName}");

        // ── 1. 기존 유닛 제거
        // 파티원(아군)은 유지하고 적·중립만 파괴한다.
        // PartyManager가 없으면 (테스트 등) 전체 삭제.
        if (PartyManager.Instance != null && PartyManager.Instance.HasParty())
            UnitManager.Instance.ClearEnemyUnits();
        else
            UnitManager.Instance.ClearAllUnits();

        // 래그돌은 UnitManager 밖에서 독립적으로 스폰되므로 별도로 제거한다
        foreach (UnitRagdoll ragdoll in FindObjectsByType<UnitRagdoll>(FindObjectsSortMode.None))
            Destroy(ragdoll.gameObject);

        // ── 2. 기존 맵 제거
        // 맵 프리팹을 Destroy하면 그 안의 MapObject·계단 등도 함께 제거됨
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
            currentMapInstance = null;
        }

        // ── 3. TurnSystem 상태 초기화
        // 턴 번호·현재 턴 유닛·isPlayerTurn 등을 초기값으로 되돌림
        TurnSystem.Instance.ResetTurn();

        // Destroy는 프레임 말에 처리되므로 한 프레임 대기
        yield return null;

        // ── 4. LevelGrid 그리드 데이터 리셋
        // 층 수가 같으면 기존 GridObject 내용만 비움(재생성 없음), 달라지면 새로 생성
        LevelGrid.Instance.ResetGridSystems(stageData.floorAmount);

        // ── 5. 새 맵 프리팹 인스턴시에이트
        // mapSpawnPosition은 보통 Vector3.zero — 자식 오브젝트 좌표가 월드 좌표와 일치하도록
        currentMapInstance = Instantiate(stageData.mapPrefab, stageData.mapSpawnPosition, Quaternion.identity);
        MapSetup mapSetup = currentMapInstance.GetComponent<MapSetup>();

        if (mapSetup == null)
        {
            Debug.LogError($"[StageManager] '{stageData.mapPrefab.name}'에 MapSetup 컴포넌트가 없습니다.");
            IsLoading = false;
            yield break;
        }

        // Instantiate 직후엔 Awake만 실행됨 — Start()까지 기다려야 MapObject들이 LevelGrid에 등록됨
        yield return null; // Awake 완료 대기
        yield return null; // Start 완료 대기 (MapObject.Start → LevelGrid·PathFinding에 자신을 등록)

        // ── 6. Directional Light 세기 적용
        // 스테이지별 분위기(낮·밤·실내 등)에 맞는 조명 강도 설정
        if (directionalLight != null)
        {
            directionalLight.intensity = stageData.directionalLightIntensity;
        }
        else
        {
            Debug.LogWarning("[StageManager] Directional Light가 연결되지 않았습니다.");
        }

        // ── 7. PathFinding 재설정
        // 새 맵의 장애물·바닥을 Physics 스캔해서 walkable 여부를 다시 계산
        // MapObject.Start()가 완료된 이후에 실행해야 장애물이 씬에 존재함
        if (mapSetup.pathfindingLinkContainer != null)
        {
            // 계단·링크 컨테이너가 맵 프리팹 안에 있으므로 새 인스턴스 참조로 교체
            PathFinding.Instance.SetPathfindingLinkContainer(mapSetup.pathfindingLinkContainer);
        }
        PathFinding.Instance.Setup(
            LevelGrid.Instance.GetWidth(),
            LevelGrid.Instance.GetHeight(),
            LevelGrid.Instance.GetCellSize(),
            stageData.floorAmount
        );

        // ── 8. 카메라 시작 위치·로테이션 적용
        // SetStageStart는 transform 설정과 동시에 initialAngle도 갱신 →
        // 리셋키 입력 시 이 스테이지의 시작 각도로 올바르게 복귀함
        CameraController.Instance.SetStageStart(stageData.cameraStartPosition, stageData.cameraStartRotation);
        // 스테이지별 카메라 이동 가능 범위 설정
        CameraController.Instance.SetBounds(stageData.cameraBoundsMin, stageData.cameraBoundsMax);

        // ── 9. 그리드 비주얼 재생성
        // PathFinding.Setup() 이후에 호출해야 바닥 콜라이더가 씬에 존재 →
        // groundSnapLayerMask Raycast가 정확한 지면 높이를 감지해 타일을 올바른 위치에 생성함
        // (맵이 없을 때 Start()에서 생성하면 바닥을 못 찾아 y=0에 붙어버림)
        GridSystemVisual.Instance.Initialize();

        // ── 10. 유닛 스폰
        // 적은 항상 새로 스폰. 아군은 PartyManager가 있으면 기존 유닛 재배치, 없으면 프리팹 스폰.
        SpawnEnemyUnits(mapSetup, stageData);

        if (PartyManager.Instance != null && PartyManager.Instance.HasParty())
            PartyManager.Instance.PositionPartyAtSpawnPoints(mapSetup.playerSpawnPoints);
        else
            SpawnPlayerUnits(mapSetup, stageData);

        // Unit.Start()가 실행돼야 유닛들이 LevelGrid·UnitManager에 자신을 등록함
        yield return null;

        // ── 11. 스테이지 로드 완료 알림
        // ActionBusyUI 등이 isStageClear 플래그를 여기서 리셋한다
        OnStageLoaded?.Invoke(this, EventArgs.Empty);

        // ── 12. 턴 시작
        // 유닛 등록이 완료된 시점에 속도 기준으로 정렬 후 첫 턴 진행
        TurnSystem.Instance.StartStage();

        IsLoading = false;
        Debug.Log($"[StageManager] 스테이지 로드 완료: {stageData.stageName}");
    }

    private void SpawnEnemyUnits(MapSetup mapSetup, StageData stageData)
    {
        foreach (EnemySpawnInfo spawnInfo in stageData.enemySpawnInfos)
        {
            if (spawnInfo.unitPrefab == null)
            {
                Debug.LogWarning("[StageManager] 적 unitPrefab이 null입니다. 해당 항목을 건너뜁니다.");
                continue;
            }
            if (spawnInfo.spawnPointIndex >= mapSetup.enemySpawnPoints.Length)
            {
                Debug.LogWarning($"[StageManager] 적 스폰 포인트 인덱스({spawnInfo.spawnPointIndex})가 범위를 벗어났습니다.");
                continue;
            }
            Transform spawnPoint = mapSetup.enemySpawnPoints[spawnInfo.spawnPointIndex];
            Instantiate(spawnInfo.unitPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }

    /// <summary>
    /// PartyManager가 없을 때(테스트 등) StageData의 playerSpawnInfos로 아군을 직접 스폰한다.
    /// </summary>
    private void SpawnPlayerUnits(MapSetup mapSetup, StageData stageData)
    {
        foreach (PlayerSpawnInfo spawnInfo in stageData.playerSpawnInfos)
        {
            if (spawnInfo.unitPrefab == null)
            {
                Debug.LogWarning("[StageManager] 아군 unitPrefab이 null입니다. 해당 항목을 건너뜁니다.");
                continue;
            }
            if (spawnInfo.spawnPointIndex >= mapSetup.playerSpawnPoints.Length)
            {
                Debug.LogWarning($"[StageManager] 아군 스폰 포인트 인덱스({spawnInfo.spawnPointIndex})가 범위를 벗어났습니다.");
                continue;
            }
            Transform spawnPoint = mapSetup.playerSpawnPoints[spawnInfo.spawnPointIndex];
            Instantiate(spawnInfo.unitPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}
