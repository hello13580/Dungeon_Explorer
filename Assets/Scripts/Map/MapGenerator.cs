using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MapGenerationConfig를 바탕으로 런타임에 MapData를 생성하는 싱글톤.
/// CharacterSelectManager.StartGame()에서 Generate()를 호출한다.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance { get; private set; }

    [SerializeField] private MapGenerationConfig config;

    private const int MaxRetries = 100;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── 공개 API ────────────────────────────────────────────────

    public MapData Generate()
    {
        for (int i = 0; i < MaxRetries; i++)
        {
            MapData result = TryGenerate();
            if (result != null) return result;
        }
        Debug.LogWarning("[MapGenerator] 상점·휴식 거리 제약 만족 실패. 제약 무시하고 생성합니다.");
        return TryGenerate(ignoreConstraint: true);
    }

    // ─── 생성 로직 ───────────────────────────────────────────────

    private MapData TryGenerate(bool ignoreConstraint = false)
    {
        // 1. 레이어별 노드 수 결정 (미들 레이어 10개 고정)
        List<int> layerSizes = DistributeNodes(
            config.TotalMiddleNodeCount,
            MapGenerationConfig.MiddleLayerCount,
            config.maxNodesPerLayer
        );
        if (layerSizes == null) return null;

        // 2. 레이어별 노드 인덱스 목록 구성
        //    레이어 0 = Start(1개), 1..N = 중간, N+1 = Boss(1개)
        List<List<int>> layerNodes = new List<List<int>>();
        int nextId = 0;

        layerNodes.Add(new List<int> { nextId++ }); // Start

        foreach (int size in layerSizes)
        {
            var layer = new List<int>();
            for (int n = 0; n < size; n++) layer.Add(nextId++);
            layerNodes.Add(layer);
        }

        layerNodes.Add(new List<int> { nextId++ }); // Boss

        int totalNodes = nextId;

        // 3. 타입 배정
        List<MapNodeType> types = BuildTypes(totalNodes, layerNodes);

        // 4. 간선 생성 (방향 있는 DAG)
        List<List<int>> nextOf = BuildEdges(layerNodes, totalNodes);

        // 5. 상점·휴식 거리 제약 검사
        if (!ignoreConstraint && !CheckShopRestDistance(types, nextOf))
            return null;

        // 6. 위치 배정 (정규화 좌표: -0.5 ~ 0.5)
        Vector2[] positions = AssignPositions(layerNodes, totalNodes);

        // 7. 노드 → 미들 레이어 인덱스 매핑 (0-based, Start/Boss는 -1)
        int[] nodeToMiddleLayer = BuildNodeToMiddleLayerMap(totalNodes, layerNodes);

        // 8. MapData 조립
        return BuildMapData(totalNodes, types, positions, nextOf, layerNodes, nodeToMiddleLayer);
    }

    // ─── 헬퍼: 노드 수 분배 ──────────────────────────────────────

    private List<int> DistributeNodes(int total, int layers, int maxPerLayer)
    {
        if (total < layers)
        {
            Debug.LogError($"[MapGenerator] 노드 수({total})가 레이어 수({layers})보다 적습니다. MapGenerationConfig에서 노드 수를 {layers} 이상으로 설정하세요.");
            return null;
        }

        // 최소 1개씩 배정 후 나머지를 랜덤 분배
        var result = new List<int>(new int[layers]);
        for (int i = 0; i < layers; i++) result[i] = 1;

        int remaining = total - layers;
        int safeGuard = 0;
        while (remaining > 0 && safeGuard++ < 10000)
        {
            int layer = Random.Range(0, layers);
            if (result[layer] < maxPerLayer)
            {
                result[layer]++;
                remaining--;
            }
        }
        return result;
    }

    // ─── 헬퍼: 타입 배정 ─────────────────────────────────────────

    private List<MapNodeType> BuildTypes(int totalNodes, List<List<int>> layerNodes)
    {
        var types = new List<MapNodeType>(new MapNodeType[totalNodes]);

        // 시작 노드
        types[layerNodes[0][0]] = MapNodeType.Start;

        // 보스 노드
        types[layerNodes[layerNodes.Count - 1][0]] = MapNodeType.Boss;

        // 중간 노드 타입 목록 셔플 후 배정
        var middleTypes = new List<MapNodeType>();
        for (int i = 0; i < config.combatCount; i++) middleTypes.Add(MapNodeType.Combat);
        for (int i = 0; i < config.eliteCount;  i++) middleTypes.Add(MapNodeType.Elite);
        for (int i = 0; i < config.eventCount;  i++) middleTypes.Add(MapNodeType.Event);
        for (int i = 0; i < config.shopCount;   i++) middleTypes.Add(MapNodeType.Shop);
        for (int i = 0; i < config.restCount;   i++) middleTypes.Add(MapNodeType.Rest);
        Shuffle(middleTypes);

        int typeIdx = 0;
        for (int l = 1; l < layerNodes.Count - 1; l++)
        {
            foreach (int nodeId in layerNodes[l])
                types[nodeId] = middleTypes[typeIdx++];
        }
        return types;
    }

    // ─── 헬퍼: 간선 생성 ─────────────────────────────────────────

    private List<List<int>> BuildEdges(List<List<int>> layerNodes, int totalNodes)
    {
        var nextOf = new List<List<int>>(totalNodes);
        for (int i = 0; i < totalNodes; i++) nextOf.Add(new List<int>());

        for (int l = 0; l < layerNodes.Count - 1; l++)
            GenerateLayerEdges(layerNodes[l], layerNodes[l + 1], nextOf);

        return nextOf;
    }

    private void GenerateLayerEdges(List<int> from, List<int> to, List<List<int>> nextOf)
    {
        int fromCount = from.Count;
        int toCount   = to.Count;

        for (int fi = 0; fi < fromCount; fi++)
        {
            // 상대 위치 기반으로 대응되는 to 인덱스 계산 (교차 방지)
            float rel   = fromCount == 1 ? 0.5f : (float)fi / (fromCount - 1);
            int   toIdx = Mathf.RoundToInt(rel * (toCount - 1));
            AddEdge(from[fi], to[toIdx], nextOf);

            // 20% 확률로 바로 옆 인덱스(+1)에만 추가 연결 — 먼 대각선 방지
            if (toIdx + 1 < toCount && Random.value < 0.2f)
                AddEdge(from[fi], to[toIdx + 1], nextOf);
        }

        // 모든 toNode가 최소 1개의 incoming을 갖도록 보장
        for (int ti = 0; ti < toCount; ti++)
        {
            bool hasIncoming = false;
            foreach (int f in from)
                if (nextOf[f].Contains(to[ti])) { hasIncoming = true; break; }

            if (!hasIncoming)
            {
                // 상대 위치상 가장 가까운 fromNode에서 연결
                float rel = toCount == 1 ? 0.5f : (float)ti / (toCount - 1);
                int   fi  = Mathf.RoundToInt(rel * (fromCount - 1));
                AddEdge(from[fi], to[ti], nextOf);
            }
        }
    }

    private void AddEdge(int from, int to, List<List<int>> nextOf)
    {
        if (!nextOf[from].Contains(to))
            nextOf[from].Add(to);
    }

    // ─── 헬퍼: 상점·휴식 거리 제약 ──────────────────────────────

    private bool CheckShopRestDistance(List<MapNodeType> types, List<List<int>> nextOf)
    {
        int n = types.Count;
        int minDist = config.minShopRestDistance;

        for (int start = 0; start < n; start++)
        {
            bool isShop = types[start] == MapNodeType.Shop;
            bool isRest = types[start] == MapNodeType.Rest;
            if (!isShop && !isRest) continue;

            // BFS로 최단 거리 계산
            int[] dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = -1;
            dist[start] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int next in nextOf[cur])
                {
                    if (dist[next] != -1) continue;
                    dist[next] = dist[cur] + 1;
                    queue.Enqueue(next);
                }
            }

            // 반대 타입과 너무 가까우면 실패
            MapNodeType opposite = isShop ? MapNodeType.Rest : MapNodeType.Shop;
            for (int i = 0; i < n; i++)
            {
                if (types[i] == opposite && dist[i] >= 0 && dist[i] < minDist)
                    return false;
            }
        }
        return true;
    }

    // ─── 헬퍼: UI 위치 배정 ──────────────────────────────────────

    // 정규화 좌표(-0.5~0.5)로 저장. MapUI에서 실제 컨테이너 크기를 곱해 픽셀 좌표로 변환한다.
    private Vector2[] AssignPositions(List<List<int>> layerNodes, int totalNodes)
    {
        var positions = new Vector2[totalNodes];
        int totalLayers = layerNodes.Count;
        float margin = 0.08f; // 전체 크기 대비 여백 비율

        for (int l = 0; l < totalLayers; l++)
        {
            float x = Mathf.Lerp(-0.5f + margin, 0.5f - margin,
                          (float)l / Mathf.Max(1, totalLayers - 1));
            var nodes = layerNodes[l];

            for (int n = 0; n < nodes.Count; n++)
            {
                float y = nodes.Count == 1
                    ? 0f
                    : Mathf.Lerp(0.5f - margin, -0.5f + margin,
                          (float)n / (nodes.Count - 1));

                positions[nodes[n]] = new Vector2(x, y);
            }
        }
        return positions;
    }

    // ─── 헬퍼: 노드 → 미들 레이어 인덱스 매핑 ───────────────────

    /// <summary>
    /// 각 노드가 몇 번째 미들 레이어에 속하는지 반환한다.
    /// Start(layerNodes[0])와 Boss(layerNodes[last])는 -1.
    /// 미들 레이어는 0-based (layerNodes[1] → 0, layerNodes[2] → 1, ...).
    /// </summary>
    private int[] BuildNodeToMiddleLayerMap(int totalNodes, List<List<int>> layerNodes)
    {
        var map = new int[totalNodes];
        for (int i = 0; i < totalNodes; i++) map[i] = -1;

        for (int l = 1; l < layerNodes.Count - 1; l++)
        {
            int middleIdx = l - 1; // 0-based 미들 레이어 인덱스
            foreach (int nodeId in layerNodes[l])
                map[nodeId] = middleIdx;
        }
        return map;
    }

    // ─── 헬퍼: MapData 조립 ──────────────────────────────────────

    private MapData BuildMapData(int totalNodes, List<MapNodeType> types, Vector2[] positions,
                                  List<List<int>> nextOf, List<List<int>> layerNodes,
                                  int[] nodeToMiddleLayer)
    {
        MapData mapData = ScriptableObject.CreateInstance<MapData>();
        var nodes = new MapNodeData[totalNodes];

        // 레이어별 스테이지 풀 미리 생성
        int layerCount = MapGenerationConfig.MiddleLayerCount;
        var combatPools = new ShuffledPool<StageData>[layerCount];
        var elitePools  = new ShuffledPool<StageData>[layerCount];
        for (int l = 0; l < layerCount; l++)
        {
            LayerStageConfig lc = (config.layers != null && l < config.layers.Length)
                ? config.layers[l] : null;
            combatPools[l] = new ShuffledPool<StageData>(lc?.combatStages);
            elitePools[l]  = new ShuffledPool<StageData>(lc?.eliteStages);
        }

        var eventPool = new ShuffledPool<EventNodeData>(config.eventDatas);

        for (int i = 0; i < totalNodes; i++)
        {
            var node = ScriptableObject.CreateInstance<MapNodeData>();
            node.nodeId          = $"node_{i}";
            node.nodeType        = types[i];
            node.position        = positions[i];
            node.nextNodeIndices = nextOf[i].ToArray();

            int ml = nodeToMiddleLayer[i]; // 미들 레이어 인덱스 (-1이면 Start/Boss)

            switch (types[i])
            {
                case MapNodeType.Combat:
                    if (ml >= 0) node.stageData = combatPools[ml].Next();
                    break;
                case MapNodeType.Elite:
                    if (ml >= 0) node.stageData = elitePools[ml].Next();
                    break;
                case MapNodeType.Boss:
                    node.stageData = config.bossStage;
                    break;
                case MapNodeType.Event:
                    node.eventData = eventPool.Next();
                    break;
            }

            nodes[i] = node;
        }

        mapData.nodes            = nodes;
        mapData.startNodeIndices = layerNodes[0].ToArray();
        return mapData;
    }

    // ─── 유틸 ─────────────────────────────────────────────────────

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>풀이 비면 다시 셔플해서 재사용하는 순환 풀.</summary>
    private class ShuffledPool<T>
    {
        private readonly T[] _items;
        private int _idx;

        public ShuffledPool(T[] items)
        {
            _items = (items != null && items.Length > 0) ? (T[])items.Clone() : new T[0];
            _idx   = _items.Length;
        }

        public T Next()
        {
            if (_items.Length == 0) return default;
            if (_idx >= _items.Length)
            {
                for (int i = _items.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_items[i], _items[j]) = (_items[j], _items[i]);
                }
                _idx = 0;
            }
            return _items[_idx++];
        }
    }
}
