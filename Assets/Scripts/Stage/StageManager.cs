using System.Collections;
using UnityEngine;

/// <summary>
/// 스테이지 전환 전체 흐름을 관리한다.
/// 씬에 하나만 배치하면 DontDestroyOnLoad로 유지된다.
/// </summary>
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("초기 스테이지 (설정 시 게임 시작과 함께 자동 로드)")]
    [SerializeField] private StageData initialStage;

    private GameObject currentMapInstance;

    // ── 현재 로드된 스테이지 정보 조회용
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
        if (initialStage != null)
        {
            LoadStage(initialStage);
        }
    }

    // ─────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────

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

        // ── 1. 기존 유닛 제거 (리스트 먼저 비우고 GameObject Destroy)
        UnitManager.Instance.ClearAllUnits();

        // ── 2. 기존 맵 제거
        if (currentMapInstance != null)
        {
            Destroy(currentMapInstance);
            currentMapInstance = null;
        }

        // ── 3. TurnSystem 상태 초기화
        TurnSystem.Instance.ResetTurn();

        yield return null; // Destroy 처리 완료 대기

        // ── 4. LevelGrid 그리드 데이터 리셋 (새 층 수 반영)
        LevelGrid.Instance.ResetGridSystems(stageData.floorAmount);

        // ── 5. 새 맵 프리팹 인스턴시에이트
        currentMapInstance = Instantiate(stageData.mapPrefab);
        MapSetup mapSetup = currentMapInstance.GetComponent<MapSetup>();

        if (mapSetup == null)
        {
            Debug.LogError($"[StageManager] '{stageData.mapPrefab.name}'에 MapSetup 컴포넌트가 없습니다.");
            IsLoading = false;
            yield break;
        }

        yield return null; // Awake 완료 대기
        yield return null; // Start 완료 대기 (MapObject들이 LevelGrid에 등록)

        // ── 6. PathFinding 재설정 (새 맵 기준 Physics 스캔)
        if (mapSetup.pathfindingLinkContainer != null)
        {
            PathFinding.Instance.SetPathfindingLinkContainer(mapSetup.pathfindingLinkContainer);
        }
        PathFinding.Instance.Setup(
            LevelGrid.Instance.GetWidth(),
            LevelGrid.Instance.GetHeight(),
            LevelGrid.Instance.GetCellSize(),
            stageData.floorAmount
        );

        // ── 7. 유닛 스폰
        SpawnUnits(mapSetup, stageData);

        yield return null; // Unit.Start() 완료 대기 (유닛들이 LevelGrid/UnitManager에 등록)

        // ── 8. 턴 시작
        TurnSystem.Instance.StartStage();

        IsLoading = false;
        Debug.Log($"[StageManager] 스테이지 로드 완료: {stageData.stageName}");
    }

    private void SpawnUnits(MapSetup mapSetup, StageData stageData)
    {
        // 적 유닛
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

        // 아군 유닛
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
