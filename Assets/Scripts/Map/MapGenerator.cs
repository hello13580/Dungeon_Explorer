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

        // 보스 직전 레이어는 항상 노드 1개 (모든 경로가 이 휴식 노드로 수렴)
        layerSizes[layerSizes.Count - 1] = 1;

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

        // 보스 직전 레이어는 무조건 휴식으로 고정
        int preBossLayer = layerNodes.Count - 2;
        foreach (int nodeId in layerNodes[preBossLayer])
            types[nodeId] = MapNodeType.Rest;

        // [버그 수정] 전투/엘리트를 레이어 풀 유무와 무관하게 전체에서 무작위 배정하던 방식 →
        // 레이어별 스테이지 풀(LayerStageConfig)이 실제로 존재하는 레이어에만 그 타입을 배정한다.
        // 수정 전: 레이어에 엘리트 스테이지가 0개인데도 Elite 타입이 배정되어
        //          node.stageData가 null이 되고, 클릭 시 스테이지 로드 없이 다음으로 넘어가버렸다.

        // 보스 직전 레이어를 제외한 모든 미들 레이어 노드를 슬롯으로 모은다.
        // 슬롯에는 어느 미들 레이어(0-based, layers 배열 인덱스와 동일)에 속하는지 함께 기록한다.
        var slots = new List<(int nodeId, int middleLayer)>();
        for (int l = 1; l < layerNodes.Count - 1; l++)
        {
            if (l == preBossLayer) continue;
            int middleLayer = l - 1;
            foreach (int nodeId in layerNodes[l])
                slots.Add((nodeId, middleLayer));
        }
        Shuffle(slots);

        bool LayerHasCombat(int ml) => ml >= 0 && config.layers != null && ml < config.layers.Length &&
            config.layers[ml] != null && config.layers[ml].combatStages != null && config.layers[ml].combatStages.Length > 0;
        bool LayerHasElite(int ml) => ml >= 0 && config.layers != null && ml < config.layers.Length &&
            config.layers[ml] != null && config.layers[ml].eliteStages != null && config.layers[ml].eliteStages.Length > 0;

        var assigned = new bool[totalNodes];
        int remainingCombat = config.combatCount;
        int remainingElite = config.eliteCount;

        // 1) 전투 배정 — 전투 풀이 있는 레이어의 슬롯에만
        foreach (var slot in slots)
        {
            if (remainingCombat <= 0) break;
            if (assigned[slot.nodeId] || !LayerHasCombat(slot.middleLayer)) continue;
            types[slot.nodeId] = MapNodeType.Combat;
            assigned[slot.nodeId] = true;
            remainingCombat--;
        }

        // 2) 엘리트 배정 — 엘리트 풀이 있는 레이어의 슬롯에만
        foreach (var slot in slots)
        {
            if (remainingElite <= 0) break;
            if (assigned[slot.nodeId] || !LayerHasElite(slot.middleLayer)) continue;
            types[slot.nodeId] = MapNodeType.Elite;
            assigned[slot.nodeId] = true;
            remainingElite--;
        }

        // 풀이 있는 레이어의 슬롯이 부족해 다 배정하지 못한 경우 — 이벤트로 대체하고 경고
        if (remainingCombat > 0)
            Debug.LogWarning($"[MapGenerator] 전투 스테이지 풀이 있는 레이어 슬롯이 부족해 {remainingCombat}개를 배정하지 못했습니다. 이벤트로 대체합니다.");
        if (remainingElite > 0)
            Debug.LogWarning($"[MapGenerator] 엘리트 스테이지 풀이 있는 레이어 슬롯이 부족해 {remainingElite}개를 배정하지 못했습니다. 이벤트로 대체합니다.");

        // 3) 나머지(이벤트·상점·휴식 + 못 배정된 전투/엘리트분)를 남은 슬롯에 무작위 배정
        // 단, 첫 번째 미들 레이어(middleLayer == 0)에는 이벤트·상점·휴식을 배정하지 않고 전투로 강제한다.
        var remainingTypes = new List<MapNodeType>();
        for (int i = 0; i < config.eventCount; i++) remainingTypes.Add(MapNodeType.Event);
        for (int i = 0; i < config.shopCount;  i++) remainingTypes.Add(MapNodeType.Shop);
        for (int i = 0; i < config.restCount;  i++) remainingTypes.Add(MapNodeType.Rest);
        for (int i = 0; i < remainingCombat + remainingElite; i++) remainingTypes.Add(MapNodeType.Event);
        Shuffle(remainingTypes);

        int idx = 0;
        foreach (var slot in slots)
        {
            if (assigned[slot.nodeId]) continue;

            // 첫 레이어는 전투 전용 — Event/Rest/Shop 배정 금지
            if (slot.middleLayer == 0)
            {
                types[slot.nodeId] = MapNodeType.Combat;
                assigned[slot.nodeId] = true;
                continue;
            }

            types[slot.nodeId] = remainingTypes[idx++];
            assigned[slot.nodeId] = true;
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

        // incoming 카운트 추적
        var incomingCount = new int[toCount];

        for (int fi = 0; fi < fromCount; fi++)
        {
            // 상대 위치 기반으로 대응되는 to 인덱스 계산 (교차 방지)
            float rel   = fromCount == 1 ? 0.5f : (float)fi / (fromCount - 1);
            int   toIdx = Mathf.RoundToInt(rel * (toCount - 1));
            AddEdge(from[fi], to[toIdx], nextOf);
            incomingCount[toIdx]++;

            // 20% 확률로 옆 인덱스(+1)에 추가 연결 — 이미 연결된 노드면 생략
            if (toIdx + 1 < toCount && incomingCount[toIdx + 1] == 0 && Random.value < 0.2f)
            {
                AddEdge(from[fi], to[toIdx + 1], nextOf);
                incomingCount[toIdx + 1]++;
            }
        }

        // 고립된 to 노드(incoming 없음)에만 최소 1개 연결 보장
        for (int ti = 0; ti < toCount; ti++)
        {
            if (incomingCount[ti] > 0) continue;

            float rel = toCount == 1 ? 0.5f : (float)ti / (toCount - 1);
            int   fi  = Mathf.RoundToInt(rel * (fromCount - 1));
            AddEdge(from[fi], to[ti], nextOf);
            incomingCount[ti]++;
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

            // 같은 타입끼리(휴식↔휴식, 상점↔상점)만 거리 제약 적용
            MapNodeType sameType = isShop ? MapNodeType.Shop : MapNodeType.Rest;
            for (int i = 0; i < n; i++)
            {
                if (i == start) continue;
                if (types[i] == sameType && dist[i] >= 0 && dist[i] < minDist)
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
