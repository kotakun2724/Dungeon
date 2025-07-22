// ──────────────────────────────────────────────────────────────────────
//  DungeonGenerator.cs  –  Edge‑wall 方式・フルスクリプト
//  ▶ BSP + MST で全室接続、幅 2 セル通路
//  ▶ “壁セル” を持たず Floor/Empty の境界に沿って壁プレハブを生成
//  ▶ 床と壁の厚み差を自動補正し、隙間ゼロ
//  ▶ Inspector で生成部屋数・通路幅・厚み自動補正・乱数シードなどを調整
//  ▶ Clear Dungeon で生成プレハブを一括削除
// ──────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DungeonGenerator : MonoBehaviour
{
    /*──────────────────────────── Inspector ───────────────────────────*/
    [Header("Single Prefabs (Legacy)")]
    [SerializeField] private GameObject roomFloorPrefab;
    [SerializeField] private GameObject roomWallPrefab;
    [SerializeField] private GameObject roomCeilingPrefab;
    [SerializeField] private GameObject pathFloorPrefab;
    [SerializeField] private GameObject pathWallPrefab;
    [SerializeField] private GameObject pathCeilingPrefab;

    [Header("Multiple Prefabs from Resources")]
    [Tooltip("複数Prefabを使用するか（単一Prefabを無効化）")]
    [SerializeField] private bool useMultiplePrefabs = false;
    [Tooltip("Resources/フォルダ内の部屋床Prefabパス（例: Floor/Room）")]
    [SerializeField] private string roomFloorPrefabPath = "Floor/Room";
    [Tooltip("Resources/フォルダ内の部屋壁Prefabパス（例: Wall/Room）")]
    [SerializeField] private string roomWallPrefabPath = "Wall/Room";
    [Tooltip("Resources/フォルダ内の部屋天井Prefabパス（例: Ceiling/Room）")]
    [SerializeField] private string roomCeilingPrefabPath = "Ceiling/Room";
    [Tooltip("Resources/フォルダ内の通路床Prefabパス（例: Floor/Path）")]
    [SerializeField] private string pathFloorPrefabPath = "Floor/Path";
    [Tooltip("Resources/フォルダ内の通路壁Prefabパス（例: Wall/Path）")]
    [SerializeField] private string pathWallPrefabPath = "Wall/Path";
    [Tooltip("Resources/フォルダ内の通路天井Prefabパス（例: Ceiling/Path）")]
    [SerializeField] private string pathCeilingPrefabPath = "Ceiling/Path";
    [Tooltip("Prefab選択のデバッグログを表示するか")]
    [SerializeField] private bool debugPrefabSelection = false;
    [Tooltip("詳細なデバッグログを表示するか（問題解決後はfalseに）")]
    [SerializeField] private bool verboseDebugLogs = true;

    [Header("Global size (cells)")]
    [SerializeField] private Vector2Int dungeonSize = new(64, 64);

    [Header("Room settings")]
    [SerializeField] private int minRoomSize = 4;
    [SerializeField] private int maxRoomSize = 12;
    [Range(0f, 1f)][SerializeField] private float density = .5f;

    [Header("Room limit")]
    [Tooltip("生成する部屋数 (0 以下で無制限)")]
    [SerializeField] private int targetRoomCount = 30;

    [Header("Corridor settings")]
    [Range(0f, 1f)][SerializeField] private float connectivity = .25f;
    [SerializeField] private bool straightCorridors = true;
    [SerializeField] private int corridorWidth = 2;       // 1 or 2
    [Tooltip("長い直線通路を防ぐための最大直線長")]
    [SerializeField] private int maxStraightLength = 3;
    [Tooltip("通路にランダムな曲がりを追加する確率 (0-1)")]
    [Range(0f, 1f)][SerializeField] private float bendProbability = 0.8f;

    [Header("Tile / Scale")]
    [SerializeField] private float cellSize = 4.55f;     // 1 マス = 4.55 m
    [SerializeField] private bool autoOffsets = true;     // 厚み自動補正

    [Header("Floor & Ceiling Settings")]
    [Tooltip("天井を生成するか")]
    [SerializeField] private bool generateCeilings = true;
    [Tooltip("天井の高さ（床からの距離）")]
    [SerializeField] private float ceilingHeight = 3.0f;

    [Header("Floor & Ceiling Rotation")]
    [Tooltip("床Prefabをランダムに回転させるか")]
    [SerializeField] private bool enableFloorRotation = true;
    [Tooltip("天井Prefabをランダムに回転させるか")]
    [SerializeField] private bool enableCeilingRotation = true;
    [Tooltip("部屋の床を回転させるか")]
    [SerializeField] private bool rotateRoomFloors = true;
    [Tooltip("部屋の天井を回転させるか")]
    [SerializeField] private bool rotateRoomCeilings = true;
    [Tooltip("通路の床を回転させるか")]
    [SerializeField] private bool rotatePathFloors = true;
    [Tooltip("通路の天井を回転させるか")]
    [SerializeField] private bool rotatePathCeilings = true;

    [Header("Debug / Seed")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 0;

    [Header("Room Adjacent Path Settings")]
    [Tooltip("部屋に隣接するPathタグを変更するか")]
    [SerializeField] private bool changeRoomAdjacentPaths = true;
    [Tooltip("部屋に隣接するPathの新しいタグ名")]
    [SerializeField] private string roomAdjacentPathTag = "Room_Adjacent_Path";
    [Tooltip("8方向チェック（true）か4方向チェック（false）")]
    [SerializeField] private bool use8DirectionCheck = true;
    [Tooltip("元のPathタグを完全に除去するか（false=隣接していない通路は「Path」のまま）")]
    [SerializeField] private bool removeOriginalPathTag = false;

    [Header("Random Eventable Path Settings")]
    [Tooltip("Isolated_PathからランダムにEventable_Pathに変更するか")]
    [SerializeField] private bool enableRandomEventablePaths = true;
    [Tooltip("Eventable_Pathに変更する数（0=自動計算、-1=全て）")]
    [SerializeField] private int eventablePathCount = 5;
    [Tooltip("自動計算時の変更割合（0.0-1.0）")]
    [Range(0f, 1f)][SerializeField] private float eventablePathRatio = 0.3f;

    [Header("Eventable Path Debug")]
    [Tooltip("デバッグ用：Room隣接チェックを無効化")]
    [SerializeField] private bool ignoreRoomAdjacency = false;
    [Tooltip("デバッグ用：最小連続長を変更（通常は3）")]
    [SerializeField] private int minConsecutiveLength = 3;
    [Tooltip("デバッグ用：最大連続長を変更（通常は5）")]
    [SerializeField] private int maxConsecutiveLength = 5;

    /*──────────────────────────── Internal ───────────────────────────*/
    private enum CellType { Empty, RoomFloor, PathFloor }              // 壁セルを廃止

    private CellType[,] grid;
    private readonly List<RectInt> rooms = new();
    private readonly List<Vector2Int> roomCenters = new();
    private readonly List<Vector2Int> isolatedPaths = new();
    private System.Random rng;

    // Multiple Prefabs Arrays
    private GameObject[] roomFloorPrefabs;
    private GameObject[] roomWallPrefabs;
    private GameObject[] roomCeilingPrefabs;
    private GameObject[] pathFloorPrefabs;
    private GameObject[] pathWallPrefabs;
    private GameObject[] pathCeilingPrefabs;

    // オフセット & スナップ用
    private float halfCell;
    private float roomFloorHalfT, pathFloorHalfT, roomWallHalfT, pathWallHalfT;
    private float roomCeilingHalfT, pathCeilingHalfT;
    private float roomFloorYOffset, pathFloorYOffset, roomWallYOffset, pathWallYOffset;
    private float roomCeilingYOffset, pathCeilingYOffset;
    private float roomThicknessDiff, pathThicknessDiff;

    /*==================================================================*/
    #region Public API
    /*==================================================================*/
    public void Generate()
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        InitializePrefabs();
        CalcOffsetsAndThickness();
        Clear();

        rng = randomSeed ? new System.Random() : new System.Random(seed);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        SplitArea(new RectInt(Vector2Int.zero, dungeonSize));
        Debug.Log($"BSP Split: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        CarveRooms();
        Debug.Log($"Room Generation: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        ConnectAllRooms();
        Debug.Log($"MST Connection: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        InstantiateFloors();
        Debug.Log($"Floor Generation: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        InstantiateCeilings();
        Debug.Log($"Ceiling Generation: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        HandleRoomAdjacentPaths();
        Debug.Log($"Room Adjacent Path Processing: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        HandleRandomEventablePaths();
        Debug.Log($"Random Eventable Path Processing: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        UpdateEventablePaths();
        Debug.Log($"Eventable Path Detection: {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        InstantiateWalls();
        Debug.Log($"Wall Generation: {sw.ElapsedMilliseconds}ms");

        totalStopwatch.Stop();
        Debug.Log($"=== Total Generation Time: {totalStopwatch.ElapsedMilliseconds}ms ===");
    }
    #endregion

    /*==================================================================*/
    #region  Prefab Management
    /*==================================================================*/
    private void InitializePrefabs()
    {
        if (verboseDebugLogs)
        {
            Debug.Log($"=== InitializePrefabs START ===");
            Debug.Log($"useMultiplePrefabs: {useMultiplePrefabs}");
        }

        if (!useMultiplePrefabs)
        {
            if (verboseDebugLogs)
            {
                Debug.Log("Using single prefabs (legacy mode)");
            }
            return;
        }

        Debug.Log("Loading multiple prefabs from Resources...");
        if (verboseDebugLogs)
        {
            Debug.Log($"Paths - RoomFloor: '{roomFloorPrefabPath}', RoomWall: '{roomWallPrefabPath}', RoomCeiling: '{roomCeilingPrefabPath}', PathFloor: '{pathFloorPrefabPath}', PathWall: '{pathWallPrefabPath}', PathCeiling: '{pathCeilingPrefabPath}'");
        }

        // Resources.LoadAllを使用してPrefabを読み込み
        roomFloorPrefabs = LoadPrefabsFromPath(roomFloorPrefabPath, "Room Floor");
        roomWallPrefabs = LoadPrefabsFromPath(roomWallPrefabPath, "Room Wall");
        roomCeilingPrefabs = LoadPrefabsFromPath(roomCeilingPrefabPath, "Room Ceiling");
        pathFloorPrefabs = LoadPrefabsFromPath(pathFloorPrefabPath, "Path Floor");
        pathWallPrefabs = LoadPrefabsFromPath(pathWallPrefabPath, "Path Wall");
        pathCeilingPrefabs = LoadPrefabsFromPath(pathCeilingPrefabPath, "Path Ceiling");

        Debug.Log($"Loaded prefabs - RoomFloor: {roomFloorPrefabs?.Length ?? 0}, " +
                 $"RoomWall: {roomWallPrefabs?.Length ?? 0}, " +
                 $"RoomCeiling: {roomCeilingPrefabs?.Length ?? 0}, " +
                 $"PathFloor: {pathFloorPrefabs?.Length ?? 0}, " +
                 $"PathWall: {pathWallPrefabs?.Length ?? 0}, " +
                 $"PathCeiling: {pathCeilingPrefabs?.Length ?? 0}");

        // 各配列の詳細を表示（verboseモードのみ）
        if (verboseDebugLogs)
        {
            if (roomFloorPrefabs != null && roomFloorPrefabs.Length > 0)
            {
                Debug.Log($"Room Floor Prefabs: {string.Join(", ", System.Array.ConvertAll(roomFloorPrefabs, p => p.name))}");
            }
            if (roomCeilingPrefabs != null && roomCeilingPrefabs.Length > 0)
            {
                Debug.Log($"Room Ceiling Prefabs: {string.Join(", ", System.Array.ConvertAll(roomCeilingPrefabs, p => p.name))}");
            }
            if (pathFloorPrefabs != null && pathFloorPrefabs.Length > 0)
            {
                Debug.Log($"Path Floor Prefabs: {string.Join(", ", System.Array.ConvertAll(pathFloorPrefabs, p => p.name))}");
            }
            if (pathCeilingPrefabs != null && pathCeilingPrefabs.Length > 0)
            {
                Debug.Log($"Path Ceiling Prefabs: {string.Join(", ", System.Array.ConvertAll(pathCeilingPrefabs, p => p.name))}");
            }
        }
    }

    private GameObject[] LoadPrefabsFromPath(string path, string prefabType)
    {
        if (verboseDebugLogs)
        {
            Debug.Log($"--- LoadPrefabsFromPath for {prefabType} ---");
            Debug.Log($"Input path: '{path}'");
        }

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"{prefabType} path is empty!");
            return null;
        }

        try
        {
            if (verboseDebugLogs)
            {
                Debug.Log($"Attempting Resources.LoadAll<GameObject>(\"{path}\")...");
            }

            GameObject[] prefabs = Resources.LoadAll<GameObject>(path);

            if (verboseDebugLogs)
            {
                Debug.Log($"Resources.LoadAll returned: {(prefabs == null ? "null" : $"array with {prefabs.Length} items")}");
            }

            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning($"No {prefabType} prefabs found at path: Resources/{path}");
                Debug.LogWarning($"Make sure the folder exists: Assets/Resources/{path}/");
                Debug.LogWarning($"And contains GameObjects with Prefab components");
                return null;
            }

            Debug.Log($"Successfully loaded {prefabs.Length} {prefabType} prefabs from Resources/{path}");
            if (verboseDebugLogs)
            {
                for (int i = 0; i < prefabs.Length; i++)
                {
                    Debug.Log($"  [{i}] {prefabs[i].name}");
                }
            }
            return prefabs;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load {prefabType} prefabs from Resources/{path}: {e.Message}");
            if (verboseDebugLogs)
            {
                Debug.LogError($"Exception details: {e}");
            }
            return null;
        }
    }

    private GameObject GetRandomPrefab(GameObject[] prefabArray, GameObject fallbackPrefab, string prefabType)
    {
        // 詳細なデバッグ情報（verboseDebugLogsがtrueの場合のみ）
        if (verboseDebugLogs)
        {
            Debug.Log($"GetRandomPrefab called for {prefabType}:");
            Debug.Log($"  useMultiplePrefabs: {useMultiplePrefabs}");
            Debug.Log($"  prefabArray: {(prefabArray == null ? "null" : $"Length={prefabArray.Length}")}");
            Debug.Log($"  fallbackPrefab: {(fallbackPrefab == null ? "null" : fallbackPrefab.name)}");
        }

        if (useMultiplePrefabs && prefabArray != null && prefabArray.Length > 0)
        {
            int randomIndex = rng.Next(prefabArray.Length);
            GameObject selectedPrefab = prefabArray[randomIndex];

            if (verboseDebugLogs || debugPrefabSelection)
            {
                Debug.Log($"✓ MULTIPLE MODE: Selected {prefabType}: {selectedPrefab.name} (index {randomIndex} from {prefabArray.Length} options)");
            }
            return selectedPrefab;
        }

        if (fallbackPrefab == null)
        {
            Debug.LogError($"No {prefabType} prefab available! Please set single prefab or configure multiple prefabs path.");
        }
        else
        {
            if (verboseDebugLogs || debugPrefabSelection)
            {
                Debug.Log($"✓ SINGLE MODE: Using single {prefabType}: {fallbackPrefab.name}");
                Debug.Log($"  Reason: {(!useMultiplePrefabs ? "useMultiplePrefabs=false" : prefabArray == null ? "prefabArray is null" : "prefabArray is empty")}");
            }
        }

        return fallbackPrefab;
    }

    [ContextMenu("Reload Multiple Prefabs")]
    public void ReloadMultiplePrefabs()
    {
        if (!useMultiplePrefabs)
        {
            Debug.Log("Multiple prefabs mode is disabled.");
            return;
        }

        Debug.Log("=== Reloading Multiple Prefabs ===");
        InitializePrefabs();
    }

    [ContextMenu("List Loaded Prefabs")]
    public void ListLoadedPrefabs()
    {
        if (!useMultiplePrefabs)
        {
            Debug.Log("Multiple prefabs mode is disabled.");
            return;
        }

        Debug.Log("=== Currently Loaded Prefabs ===");
        LogPrefabArray("Room Floor", roomFloorPrefabs);
        LogPrefabArray("Room Wall", roomWallPrefabs);
        LogPrefabArray("Room Ceiling", roomCeilingPrefabs);
        LogPrefabArray("Path Floor", pathFloorPrefabs);
        LogPrefabArray("Path Wall", pathWallPrefabs);
        LogPrefabArray("Path Ceiling", pathCeilingPrefabs);
    }

    [ContextMenu("Test Multiple Prefabs")]
    public void TestMultiplePrefabs()
    {
        Debug.Log("=== Testing Multiple Prefabs System ===");
        Debug.Log($"useMultiplePrefabs: {useMultiplePrefabs}");

        if (!useMultiplePrefabs)
        {
            Debug.LogWarning("useMultiplePrefabs is FALSE. Enable it to use multiple prefabs.");
            return;
        }

        // Test initialization
        InitializePrefabs();

        // Test random selection
        Debug.Log("--- Testing Random Selection ---");
        for (int i = 0; i < 3; i++)
        {
            Debug.Log($"Test {i + 1}:");
            GameObject roomFloor = GetRandomPrefab(roomFloorPrefabs, roomFloorPrefab, "Room Floor");
            GameObject pathFloor = GetRandomPrefab(pathFloorPrefabs, pathFloorPrefab, "Path Floor");
            GameObject roomCeiling = GetRandomPrefab(roomCeilingPrefabs, roomCeilingPrefab, "Room Ceiling");
            GameObject pathCeiling = GetRandomPrefab(pathCeilingPrefabs, pathCeilingPrefab, "Path Ceiling");
            Debug.Log($"  Room Floor: {(roomFloor != null ? roomFloor.name : "null")}");
            Debug.Log($"  Path Floor: {(pathFloor != null ? pathFloor.name : "null")}");
            Debug.Log($"  Room Ceiling: {(roomCeiling != null ? roomCeiling.name : "null")}");
            Debug.Log($"  Path Ceiling: {(pathCeiling != null ? pathCeiling.name : "null")}");
        }
    }

    private void LogPrefabArray(string prefabType, GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.Log($"{prefabType}: No prefabs loaded");
            return;
        }

        Debug.Log($"{prefabType} ({prefabs.Length} prefabs):");
        for (int i = 0; i < prefabs.Length; i++)
        {
            Debug.Log($"  [{i}] {prefabs[i].name}");
        }
    }
    #endregion

    /*==================================================================*/
    #region Clear
    /*==================================================================*/
    public void Clear()
    {
        while (transform.childCount > 0)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(transform.GetChild(0).gameObject);
            else
                Destroy(transform.GetChild(0).gameObject);
#else
            Destroy(transform.GetChild(0).gameObject);
#endif
        }
        grid = new CellType[dungeonSize.x, dungeonSize.y];
        rooms.Clear();
        roomCenters.Clear();
        isolatedPaths.Clear();
    }
    #endregion

    /*==================================================================*/
    #region  Offset & Thickness
    /*==================================================================*/
    private void CalcOffsetsAndThickness()
    {
        halfCell = cellSize * .5f;
        roomFloorHalfT = roomFloorPrefab.transform.localScale.y * .5f;
        pathFloorHalfT = pathFloorPrefab.transform.localScale.y * .5f;
        roomWallHalfT = roomWallPrefab.transform.localScale.z * .5f;  // 壁 Cube は X=90° 回転前提
        pathWallHalfT = pathWallPrefab.transform.localScale.z * .5f;  // 壁 Cube は X=90° 回転前提

        // 天井の厚みを計算（Prefabがある場合のみ）
        if (generateCeilings && roomCeilingPrefab != null)
            roomCeilingHalfT = roomCeilingPrefab.transform.localScale.y * .5f;
        if (generateCeilings && pathCeilingPrefab != null)
            pathCeilingHalfT = pathCeilingPrefab.transform.localScale.y * .5f;

        roomThicknessDiff = roomFloorHalfT - roomWallHalfT;   // 正なら床の方が厚い
        pathThicknessDiff = pathFloorHalfT - pathWallHalfT;   // 正なら床の方が厚い

        if (!autoOffsets)
        {
            roomFloorYOffset = pathFloorYOffset = roomWallYOffset = pathWallYOffset = 0f;
            roomCeilingYOffset = pathCeilingYOffset = 0f;
            return;
        }

        // Pivot = 中央 -> 床: −半厚, 壁: +半厚 (下面を y=0)
        roomFloorYOffset = -roomFloorHalfT;
        pathFloorYOffset = -pathFloorHalfT;
        roomWallYOffset = roomWallHalfT;
        pathWallYOffset = pathWallHalfT;

        // 天井: 指定高さ + 半厚 (上面を ceilingHeight に配置)
        roomCeilingYOffset = ceilingHeight + roomCeilingHalfT;
        pathCeilingYOffset = ceilingHeight + pathCeilingHalfT;
    }
    #endregion

    /*==================================================================*/
    #region  BSP & Room Generation
    /*==================================================================*/
    private void SplitArea(RectInt area, int depth = 0)
    {
        if (depth > 8 || area.width < maxRoomSize * 2 || area.height < maxRoomSize * 2)
        {
            rooms.Add(area);
            return;
        }

        bool splitHoriz = area.width > area.height;
        int splitPos = splitHoriz
            ? rng.Next(minRoomSize, area.width - minRoomSize)
            : rng.Next(minRoomSize, area.height - minRoomSize);

        RectInt a, b;
        if (splitHoriz)
        {
            a = new RectInt(area.x, area.y, splitPos, area.height);
            b = new RectInt(area.x + splitPos, area.y, area.width - splitPos, area.height);
        }
        else
        {
            a = new RectInt(area.x, area.y, area.width, splitPos);
            b = new RectInt(area.x, area.y + splitPos, area.width, area.height - splitPos);
        }

        SplitArea(a, depth + 1);
        SplitArea(b, depth + 1);
    }

    private void CarveRooms()
    {
        // BSP リーフをシャッフルし、targetRoomCount だけ採用
        List<RectInt> shuffled = new(rooms);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int swap = rng.Next(i, shuffled.Count);
            (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
        }

        int carved = 0;
        foreach (RectInt leaf in shuffled)
        {
            if (targetRoomCount > 0 && carved >= targetRoomCount) break;

            // density を使用して部屋生成の確率を制御
            if (rng.NextDouble() > density) continue;

            int rw = Mathf.Clamp(rng.Next(minRoomSize, maxRoomSize + 1), 3, leaf.width - 2);
            int rh = Mathf.Clamp(rng.Next(minRoomSize, maxRoomSize + 1), 3, leaf.height - 2);
            int rx = leaf.x + rng.Next(1, leaf.width - rw - 1);
            int ry = leaf.y + rng.Next(1, leaf.height - rh - 1);

            var room = new RectInt(rx, ry, rw, rh);
            FillRect(room, CellType.RoomFloor);

            roomCenters.Add(new Vector2Int(room.xMin + room.width / 2,
                                           room.yMin + room.height / 2));

            carved++;
        }
    }
    #endregion

    /*==================================================================*/
    #region  Corridor (MST)
    /*==================================================================*/
    private void ConnectAllRooms()
    {
        if (roomCenters.Count < 2) return;

        var remaining = new HashSet<Vector2Int>(roomCenters);
        var tree = new List<(Vector2Int, Vector2Int)>();

        Vector2Int current = roomCenters[0];
        remaining.Remove(current);

        // Greedy MST
        while (remaining.Count > 0)
        {
            Vector2Int nearest = default; float best = float.MaxValue;
            foreach (var t in remaining)
            {
                float d = (current - t).sqrMagnitude;
                if (d < best) { best = d; nearest = t; }
            }
            tree.Add((current, nearest));
            current = nearest;
            remaining.Remove(current);
        }

        // 追加通路
        foreach (var a in roomCenters)
            foreach (var b in roomCenters)
                if (a != b && rng.NextDouble() < connectivity)
                    tree.Add((a, b));

        foreach (var e in tree) CarveCorridor(e.Item1, e.Item2);
    }

    private void CarveCorridor(Vector2Int a, Vector2Int b)
    {
        // 基本的な境界チェック
        if (!IsInside(a.x, a.y) || !IsInside(b.x, b.y))
        {
            return;
        }

        // 直線距離を計算
        int distance = Math.Abs(b.x - a.x) + Math.Abs(b.y - a.y);

        // まず確実な通路を作成（シンプルなL字型）
        bool forceSimple = distance > maxStraightLength * 3; // 極端に長い場合はシンプルに

        if (forceSimple)
        {
            // 非常に長い距離の場合、中間点を1つだけ作って分割
            Vector2Int mid = new Vector2Int(
                (a.x + b.x) / 2 + rng.Next(-2, 3),
                (a.y + b.y) / 2 + rng.Next(-2, 3)
            );
            // 中間点を境界内に修正
            mid.x = Mathf.Clamp(mid.x, 1, dungeonSize.x - 2);
            mid.y = Mathf.Clamp(mid.y, 1, dungeonSize.y - 2);

            CarveSimplePath(a, mid);
            CarveSimplePath(mid, b);
            return;
        }

        // 通常の距離では確実なL字型通路を作成
        CarveSimplePath(a, b);
    }

    private void CarveSimplePath(Vector2Int from, Vector2Int to)
    {
        // 基本的な境界チェック
        if (!IsInside(from.x, from.y) || !IsInside(to.x, to.y))
        {
            return;
        }

        // 同じ座標の場合は処理不要
        if (from == to)
        {
            return;
        }

        // 直線距離を計算
        int totalDistance = Math.Abs(to.x - from.x) + Math.Abs(to.y - from.y);

        // 短い距離または直線の場合
        if (totalDistance <= maxStraightLength || (from.x == to.x || from.y == to.y))
        {
            // 直線の場合で長すぎる場合は分割
            if ((from.x == to.x || from.y == to.y) && totalDistance > maxStraightLength)
            {
                // 中間点を作成
                Vector2Int mid;
                if (from.x == to.x)
                {
                    // 垂直線を分割
                    int midY = from.y + Math.Sign(to.y - from.y) * maxStraightLength;
                    mid = new Vector2Int(from.x, midY);
                }
                else
                {
                    // 水平線を分割
                    int midX = from.x + Math.Sign(to.x - from.x) * maxStraightLength;
                    mid = new Vector2Int(midX, from.y);
                }

                CarveLine(from, mid);
                CarveSimplePath(mid, to);
                return;
            }

            // 短い距離の場合は確率的に処理
            if (straightCorridors && (from.x == to.x || from.y == to.y) && rng.NextDouble() > bendProbability)
            {
                CarveLine(from, to);
            }
            else
            {
                // L字型通路
                bool horizFirst = rng.Next(2) == 0;
                Vector2Int corner = horizFirst ? new Vector2Int(to.x, from.y)
                                               : new Vector2Int(from.x, to.y);
                CarveLine(from, corner);
                CarveLine(corner, to);
            }
            return;
        }

        // 長い距離の場合、確実にL字型で分割
        bool useHorizontalFirst = rng.Next(2) == 0;
        Vector2Int midPoint = useHorizontalFirst ? new Vector2Int(to.x, from.y)
                                                 : new Vector2Int(from.x, to.y);

        // L字型の各区間をさらに分割
        CarveSimplePath(from, midPoint);
        CarveSimplePath(midPoint, to);
    }

    private void CarveLine(Vector2Int from, Vector2Int to)
    {
        Vector2Int step = new(
            to.x == from.x ? 0 : Math.Sign(to.x - from.x),
            to.y == from.y ? 0 : Math.Sign(to.y - from.y));

        Vector2Int p = from;
        while (p != to)
        {
            CarveCorridorCell(p, step);
            p += step;
        }
        CarveCorridorCell(to, step);
    }

    private void CarveCorridorCell(Vector2Int cell, Vector2Int dir)
    {
        // 境界チェックを追加
        if (!IsInside(cell.x, cell.y)) return;

        if (grid[cell.x, cell.y] != CellType.RoomFloor)
            grid[cell.x, cell.y] = CellType.PathFloor;

        if (corridorWidth < 2) return;

        if (dir.x != 0)
        {
            if (IsInside(cell.x, cell.y + 1) && !IsRoomFloor(cell.x, cell.y + 1))
                grid[cell.x, cell.y + 1] = CellType.PathFloor;
            if (IsInside(cell.x, cell.y - 1) && !IsRoomFloor(cell.x, cell.y - 1))
                grid[cell.x, cell.y - 1] = CellType.PathFloor;
        }
        else
        {
            if (IsInside(cell.x + 1, cell.y) && !IsRoomFloor(cell.x + 1, cell.y))
                grid[cell.x + 1, cell.y] = CellType.PathFloor;
            if (IsInside(cell.x - 1, cell.y) && !IsRoomFloor(cell.x - 1, cell.y))
                grid[cell.x - 1, cell.y] = CellType.PathFloor;
        }
    }
    #endregion

    /*==================================================================*/
    #region Instantiate (Floor & Edge‑Wall)
    /*==================================================================*/
    void InstantiateFloors()
    {
        for (int x = 0; x < dungeonSize.x; x++)
            for (int y = 0; y < dungeonSize.y; y++)
            {
                CellType t = grid[x, y];
                if (t == CellType.Empty) continue;

                Vector3 pos = new(x * cellSize, 0, y * cellSize);
                GameObject prefabToUse;
                float yOffset;
                string tag;
                Quaternion rotation;

                if (t == CellType.RoomFloor)
                {
                    prefabToUse = GetRandomPrefab(roomFloorPrefabs, roomFloorPrefab, "Room Floor");
                    yOffset = roomFloorYOffset;
                    tag = "Room";
                    rotation = GetRandomFloorRotation(rotateRoomFloors);
                }
                else // PathFloor
                {
                    prefabToUse = GetRandomPrefab(pathFloorPrefabs, pathFloorPrefab, "Path Floor");
                    yOffset = pathFloorYOffset;
                    tag = "Path";
                    rotation = GetRandomFloorRotation(rotatePathFloors);
                }

                var go = Instantiate(prefabToUse,
                                     pos + Vector3.up * yOffset,
                                     rotation, transform);
                go.tag = tag;
            }
    }

    private Quaternion GetRandomFloorRotation(bool allowRotation)
    {
        if (!enableFloorRotation || !allowRotation)
        {
            return Quaternion.identity; // 回転なし
        }

        // ランダムにY軸回転 (0, 90, 180, 270度)
        int randomRotation = rng.Next(4) * 90;
        return Quaternion.Euler(0, randomRotation, 0);
    }

    private Quaternion GetRandomCeilingRotation(bool allowRotation)
    {
        if (!enableCeilingRotation || !allowRotation)
        {
            return Quaternion.identity; // 回転なし
        }

        // ランダムにY軸回転 (0, 90, 180, 270度)
        int randomRotation = rng.Next(4) * 90;
        return Quaternion.Euler(0, randomRotation, 0);
    }

    void InstantiateCeilings()
    {
        if (!generateCeilings)
        {
            Debug.Log("Ceiling generation is disabled.");
            return;
        }

        for (int x = 0; x < dungeonSize.x; x++)
            for (int y = 0; y < dungeonSize.y; y++)
            {
                CellType t = grid[x, y];
                if (t == CellType.Empty) continue;

                Vector3 pos = new(x * cellSize, 0, y * cellSize);
                GameObject prefabToUse;
                float yOffset;
                Quaternion rotation;

                if (t == CellType.RoomFloor)
                {
                    prefabToUse = GetRandomPrefab(roomCeilingPrefabs, roomCeilingPrefab, "Room Ceiling");
                    yOffset = roomCeilingYOffset;
                    rotation = GetRandomCeilingRotation(rotateRoomCeilings);
                }
                else // PathFloor
                {
                    prefabToUse = GetRandomPrefab(pathCeilingPrefabs, pathCeilingPrefab, "Path Ceiling");
                    yOffset = pathCeilingYOffset;
                    rotation = GetRandomCeilingRotation(rotatePathCeilings);
                }

                // Prefabが設定されていない場合はスキップ
                if (prefabToUse == null)
                {
                    if (verboseDebugLogs)
                    {
                        string prefabType = (t == CellType.RoomFloor) ? "Room Ceiling" : "Path Ceiling";
                        Debug.LogWarning($"No {prefabType} prefab available at ({x}, {y}), skipping ceiling generation.");
                    }
                    continue;
                }

                var go = Instantiate(prefabToUse,
                                     pos + Vector3.up * yOffset,
                                     rotation, transform);
                go.tag = "Ceiling";
            }
    }

    private void InstantiateWalls()
    {
        for (int x = 0; x < dungeonSize.x; x++)
            for (int y = 0; y < dungeonSize.y; y++)
            {
                if (grid[x, y] == CellType.Empty) continue;

                Vector3 center = new(x * cellSize, 0f, y * cellSize);
                bool isRoom = grid[x, y] == CellType.RoomFloor;

                // 使用するプレハブと設定を決定
                GameObject wallPrefab = isRoom ?
                    GetRandomPrefab(roomWallPrefabs, roomWallPrefab, "Room Wall") :
                    GetRandomPrefab(pathWallPrefabs, pathWallPrefab, "Path Wall");
                float wallHalfT = isRoom ? roomWallHalfT : pathWallHalfT;
                float wallYOffset = isRoom ? roomWallYOffset : pathWallYOffset;
                float thicknessDiff = isRoom ? roomThicknessDiff : pathThicknessDiff;

                Quaternion rotZ = wallPrefab.transform.rotation;            // 壁面Z
                Quaternion rotX = Quaternion.Euler(0, 90, 0) * rotZ;        // 壁面X

                // 北辺
                if (!IsInside(x, y + 1) || grid[x, y + 1] == CellType.Empty)
                    PlaceWall(center, Vector3.forward, rotX, wallPrefab, wallHalfT, wallYOffset, thicknessDiff);
                // 東辺
                if (!IsInside(x + 1, y) || grid[x + 1, y] == CellType.Empty)
                    PlaceWall(center, Vector3.right, rotZ, wallPrefab, wallHalfT, wallYOffset, thicknessDiff);
                // 南辺
                if (!IsInside(x, y - 1) || grid[x, y - 1] == CellType.Empty)
                    PlaceWall(center, Vector3.back, rotX, wallPrefab, wallHalfT, wallYOffset, thicknessDiff);
                // 西辺
                if (!IsInside(x - 1, y) || grid[x - 1, y] == CellType.Empty)
                    PlaceWall(center, Vector3.left, rotZ, wallPrefab, wallHalfT, wallYOffset, thicknessDiff);
            }

        void PlaceWall(Vector3 c, Vector3 dir, Quaternion rot, GameObject prefab, float halfT, float yOffset, float thickDiff)
        {
            float sign = (dir == Vector3.forward || dir == Vector3.right) ? 1f : -1f;
            Vector3 offset = (dir.x != 0)
                ? new Vector3(sign * (halfCell + halfT + thickDiff), 0, 0)
                : new Vector3(0, 0, sign * (halfCell + halfT + thickDiff));

            Instantiate(prefab,
                        c + offset + Vector3.up * yOffset,
                        rot, transform);
        }
    }
    #endregion

    /*==================================================================*/
    #region Room Adjacent Path Processing
    /*==================================================================*/
    private void HandleRoomAdjacentPaths()
    {
        if (!changeRoomAdjacentPaths)
        {
            Debug.Log("Room adjacent path processing is disabled.");
            return;
        }

        // Isolated_Pathsリストをクリア
        isolatedPaths.Clear();

        int adjacentCount = 0;
        int isolatedCount = 0;

        for (int x = 0; x < dungeonSize.x; x++)
        {
            for (int y = 0; y < dungeonSize.y; y++)
            {
                // Pathセルのみを対象
                if (grid[x, y] != CellType.PathFloor) continue;

                // 部屋に隣接しているかチェック
                if (IsAdjacentToRoom(x, y))
                {
                    // 部屋に隣接している場合
                    if (ChangePathTagAtPosition(x, y, roomAdjacentPathTag))
                    {
                        adjacentCount++;
                    }
                }
                else
                {
                    // 部屋に隣接していない場合
                    isolatedPaths.Add(new Vector2Int(x, y)); // 座標を常に記録

                    if (removeOriginalPathTag)
                    {
                        // 元のタグを除去する設定の場合のみタグを変更
                        if (ChangePathTagAtPosition(x, y, "Isolated_Path"))
                        {
                            isolatedCount++;
                        }
                    }
                    // removeOriginalPathTag が false の場合、「Path」タグのまま（後でランダム選択の対象になる）
                }
            }
        }

        Debug.Log($"Room Adjacent Path Processing Complete:");
        Debug.Log($"  - Adjacent paths changed to '{roomAdjacentPathTag}': {adjacentCount}");
        if (removeOriginalPathTag)
        {
            Debug.Log($"  - Non-adjacent paths changed to 'Isolated_Path': {isolatedCount}");
        }
        else
        {
            Debug.Log($"  - Non-adjacent paths remain as 'Path': {isolatedPaths.Count}");
        }
        Debug.Log($"  - Non-adjacent paths recorded for random Eventable_Path selection: {isolatedPaths.Count}");
    }

    private bool IsAdjacentToRoom(int x, int y)
    {
        if (use8DirectionCheck)
        {
            // 8方向（上下左右＋斜め）をチェック
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // 自分自身はスキップ

                    int nx = x + dx;
                    int ny = y + dy;

                    // 境界内かチェック
                    if (IsInside(nx, ny) && grid[nx, ny] == CellType.RoomFloor)
                    {
                        return true; // 部屋に隣接している
                    }
                }
            }
        }
        else
        {
            // 4方向（上下左右のみ）をチェック
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                // 境界内かチェック
                if (IsInside(nx, ny) && grid[nx, ny] == CellType.RoomFloor)
                {
                    return true; // 部屋に隣接している
                }
            }
        }

        return false;
    }

    private void HandleRandomEventablePaths()
    {
        Debug.Log($"=== HandleRandomEventablePaths START ===");
        Debug.Log($"enableRandomEventablePaths: {enableRandomEventablePaths}");
        Debug.Log($"removeOriginalPathTag: {removeOriginalPathTag}");
        Debug.Log($"eventablePathCount: {eventablePathCount}");
        Debug.Log($"eventablePathRatio: {eventablePathRatio}");
        Debug.Log($"isolatedPaths.Count: {isolatedPaths.Count}");

        if (!enableRandomEventablePaths)
        {
            Debug.Log("Random Eventable Path processing is disabled.");
            return;
        }

        if (isolatedPaths.Count == 0)
        {
            Debug.Log("No non-adjacent Path found for random Eventable_Path conversion.");
            Debug.Log("This might be because:");
            Debug.Log("  1. No paths were marked as non-adjacent to rooms");
            Debug.Log("  2. All paths are adjacent to rooms");
            Debug.Log("  3. Room Adjacent Path processing was disabled");
            return;
        }

        // 変更する数を決定
        int targetCount;
        if (eventablePathCount == -1)
        {
            // 全て変更
            targetCount = isolatedPaths.Count;
        }
        else if (eventablePathCount == 0)
        {
            // 自動計算（割合ベース）
            targetCount = Mathf.RoundToInt(isolatedPaths.Count * eventablePathRatio);
        }
        else
        {
            // 指定された数
            targetCount = Mathf.Min(eventablePathCount, isolatedPaths.Count);
        }

        if (targetCount <= 0)
        {
            Debug.Log("Target count is 0. No Isolated_Path will be converted to Eventable_Path.");
            return;
        }

        // Isolated_Pathsをシャッフル
        List<Vector2Int> shuffledPaths = new List<Vector2Int>(isolatedPaths);
        for (int i = 0; i < shuffledPaths.Count; i++)
        {
            int randomIndex = rng.Next(i, shuffledPaths.Count);
            (shuffledPaths[i], shuffledPaths[randomIndex]) = (shuffledPaths[randomIndex], shuffledPaths[i]);
        }

        // 指定された数だけEventable_Pathに変更
        int convertedCount = 0;
        for (int i = 0; i < targetCount && i < shuffledPaths.Count; i++)
        {
            Vector2Int pos = shuffledPaths[i];
            Debug.Log($"Attempting to change Path at ({pos.x}, {pos.y}) to Eventable_Path...");

            bool success = ChangePathTagAtPosition(pos.x, pos.y, "Eventable_Path");
            if (success)
            {
                convertedCount++;
                Debug.Log($"✓ Successfully changed Path at ({pos.x}, {pos.y}) to Eventable_Path");
            }
            else
            {
                Debug.LogWarning($"✗ Failed to change Path at ({pos.x}, {pos.y}) to Eventable_Path");
            }
        }

        Debug.Log($"Random Eventable Path Processing Complete:");
        Debug.Log($"  - Available non-adjacent Path count: {isolatedPaths.Count}");
        Debug.Log($"  - Target conversion count: {targetCount}");
        Debug.Log($"  - Actually converted to 'Eventable_Path': {convertedCount}");
        if (removeOriginalPathTag)
        {
            Debug.Log($"  - Remaining 'Isolated_Path': {isolatedPaths.Count - convertedCount}");
        }
        else
        {
            Debug.Log($"  - Remaining 'Path': {isolatedPaths.Count - convertedCount}");
        }
    }
    #endregion

    /*==================================================================*/
    #region Eventable Path Detection
    /*==================================================================*/
    private void UpdateEventablePaths()
    {
        // ランダム選択機能が有効な場合、従来の自動検出をスキップ
        if (enableRandomEventablePaths)
        {
            Debug.Log("Traditional Eventable Path detection is skipped because Random Eventable Path is enabled.");
            return;
        }

        // 一時的にマークするためのグリッド
        var eventableGrid = new bool[dungeonSize.x, dungeonSize.y];
        var notEventableGrid = new bool[dungeonSize.x, dungeonSize.y];
        var eventableGroups = new List<(Vector2Int start, Vector2Int end, bool isHorizontal)>();

        Debug.Log("Starting traditional Eventable Path detection...");
        Debug.Log($"Settings: minLength={minConsecutiveLength}, maxLength={maxConsecutiveLength}, ignoreRoom={ignoreRoomAdjacency}");

        // グリッドの状態を確認
        AnalyzeGridState();

        // 水平方向の3～5連続をチェック
        CheckHorizontalEventablePaths(eventableGrid, eventableGroups);

        // 垂直方向の3～5連続をチェック
        CheckVerticalEventablePaths(eventableGrid, eventableGroups);

        // Eventable_Pathの両端に隣接するPathをNot_Eventable_Pathとしてマーク
        MarkEndpointsAsNotEventable(eventableGroups, notEventableGrid);

        // 実際のGameObjectのタグを変更
        ApplyTagChanges(eventableGrid, notEventableGrid);
    }

    private void AnalyzeGridState()
    {
        int roomCount = 0;
        int pathCount = 0;
        int emptyCount = 0;

        for (int x = 0; x < dungeonSize.x; x++)
        {
            for (int y = 0; y < dungeonSize.y; y++)
            {
                switch (grid[x, y])
                {
                    case CellType.RoomFloor: roomCount++; break;
                    case CellType.PathFloor: pathCount++; break;
                    case CellType.Empty: emptyCount++; break;
                }
            }
        }

        Debug.Log($"Grid analysis: {roomCount} rooms, {pathCount} paths, {emptyCount} empty");

        // 簡単な連続Path検出テスト
        int horizontalSequences = 0;
        int verticalSequences = 0;

        // 水平チェック
        for (int y = 0; y < dungeonSize.y; y++)
        {
            int currentLength = 0;
            for (int x = 0; x < dungeonSize.x; x++)
            {
                if (grid[x, y] == CellType.PathFloor)
                {
                    currentLength++;
                }
                else
                {
                    if (currentLength >= 2) horizontalSequences++;
                    currentLength = 0;
                }
            }
            if (currentLength >= 2) horizontalSequences++;
        }

        // 垂直チェック
        for (int x = 0; x < dungeonSize.x; x++)
        {
            int currentLength = 0;
            for (int y = 0; y < dungeonSize.y; y++)
            {
                if (grid[x, y] == CellType.PathFloor)
                {
                    currentLength++;
                }
                else
                {
                    if (currentLength >= 2) verticalSequences++;
                    currentLength = 0;
                }
            }
            if (currentLength >= 2) verticalSequences++;
        }

        Debug.Log($"Found sequences (length >= 2): {horizontalSequences} horizontal, {verticalSequences} vertical");
    }

    private void CheckHorizontalEventablePaths(bool[,] eventableGrid, List<(Vector2Int start, Vector2Int end, bool isHorizontal)> eventableGroups)
    {
        Debug.Log("Checking horizontal paths...");
        int foundGroups = 0;

        for (int y = 0; y < dungeonSize.y; y++)
        {
            int x = 0;
            while (x < dungeonSize.x)
            {
                // Pathの開始点を探す
                if (grid[x, y] != CellType.PathFloor)
                {
                    x++;
                    continue;
                }

                // 開始点から連続する長さを計算
                int startX = x;
                int consecutiveLength = 0;
                while (x < dungeonSize.x && grid[x, y] == CellType.PathFloor)
                {
                    consecutiveLength++;
                    x++;
                }

                Debug.Log($"Horizontal: ({startX},{y}) length={consecutiveLength}");

                // 条件を満たすかチェック
                if (consecutiveLength >= minConsecutiveLength && consecutiveLength <= maxConsecutiveLength)
                {
                    if (IsValidEventablePath(startX, y, consecutiveLength, true))
                    {
                        Debug.Log($"✓ Valid horizontal Eventable_Path: ({startX},{y}) length={consecutiveLength}");

                        // マークする
                        for (int i = 0; i < consecutiveLength; i++)
                        {
                            eventableGrid[startX + i, y] = true;
                        }

                        // グループ記録
                        eventableGroups.Add((new Vector2Int(startX, y), new Vector2Int(startX + consecutiveLength - 1, y), true));
                        foundGroups++;
                    }
                    else
                    {
                        Debug.Log($"✗ Horizontal path rejected: ({startX},{y}) - adjacent to Room");
                    }
                }
                else
                {
                    Debug.Log($"✗ Horizontal path rejected: ({startX},{y}) - length {consecutiveLength} not in range [{minConsecutiveLength}-{maxConsecutiveLength}]");
                }
            }
        }
        Debug.Log($"Found {foundGroups} horizontal Eventable_Path groups");
    }

    private void CheckVerticalEventablePaths(bool[,] eventableGrid, List<(Vector2Int start, Vector2Int end, bool isHorizontal)> eventableGroups)
    {
        Debug.Log("Checking vertical paths...");
        int foundGroups = 0;

        for (int x = 0; x < dungeonSize.x; x++)
        {
            int y = 0;
            while (y < dungeonSize.y)
            {
                // Pathの開始点を探す
                if (grid[x, y] != CellType.PathFloor || eventableGrid[x, y])
                {
                    y++;
                    continue;
                }

                // 開始点から連続する長さを計算
                int startY = y;
                int consecutiveLength = 0;
                while (y < dungeonSize.y && grid[x, y] == CellType.PathFloor && !eventableGrid[x, y])
                {
                    consecutiveLength++;
                    y++;
                }

                Debug.Log($"Vertical: ({x},{startY}) length={consecutiveLength}");

                // 条件を満たすかチェック
                if (consecutiveLength >= minConsecutiveLength && consecutiveLength <= maxConsecutiveLength)
                {
                    if (IsValidEventablePath(x, startY, consecutiveLength, false))
                    {
                        Debug.Log($"✓ Valid vertical Eventable_Path: ({x},{startY}) length={consecutiveLength}");

                        // マークする
                        for (int i = 0; i < consecutiveLength; i++)
                        {
                            eventableGrid[x, startY + i] = true;
                        }

                        // グループ記録
                        eventableGroups.Add((new Vector2Int(x, startY), new Vector2Int(x, startY + consecutiveLength - 1), false));
                        foundGroups++;
                    }
                    else
                    {
                        Debug.Log($"✗ Vertical path rejected: ({x},{startY}) - adjacent to Room");
                    }
                }
                else
                {
                    Debug.Log($"✗ Vertical path rejected: ({x},{startY}) - length {consecutiveLength} not in range [{minConsecutiveLength}-{maxConsecutiveLength}]");
                }
            }
        }
        Debug.Log($"Found {foundGroups} vertical Eventable_Path groups");
    }

    private bool IsValidEventablePath(int startX, int startY, int length, bool isHorizontal)
    {
        Debug.Log($"Validating path at ({startX},{startY}) length {length}, horizontal: {isHorizontal}");

        // デバッグ用：Room隣接チェックを無効化する場合
        if (ignoreRoomAdjacency)
        {
            Debug.Log($"Path at ({startX},{startY}) is valid (Room adjacency check ignored)");
            return true;
        }

        // 連続するPathセルとその隣接セルにRoomが含まれていないかチェック
        for (int i = 0; i < length; i++)
        {
            int x = isHorizontal ? startX + i : startX;
            int y = isHorizontal ? startY : startY + i;

            // 4方向の隣接セルをチェック
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in directions)
            {
                int nx = x + dir.x;
                int ny = y + dir.y;
                if (IsInside(nx, ny) && grid[nx, ny] == CellType.RoomFloor)
                {
                    Debug.Log($"Path rejected: cell ({x},{y}) is adjacent to Room at ({nx},{ny})");
                    return false; // Roomと隣接している
                }
            }
        }
        Debug.Log($"Path at ({startX},{startY}) is valid for Eventable_Path");
        return true;
    }

    private void MarkEndpointsAsNotEventable(List<(Vector2Int start, Vector2Int end, bool isHorizontal)> eventableGroups, bool[,] notEventableGrid)
    {
        Debug.Log($"Marking endpoints for {eventableGroups.Count} eventable groups");

        foreach (var group in eventableGroups)
        {
            Vector2Int start = group.start;
            Vector2Int end = group.end;
            bool isHorizontal = group.isHorizontal;

            // 開始点の前の隣接セルをチェック
            Vector2Int beforeStart = isHorizontal ? new Vector2Int(start.x - 1, start.y) : new Vector2Int(start.x, start.y - 1);
            if (IsInside(beforeStart.x, beforeStart.y) && grid[beforeStart.x, beforeStart.y] == CellType.PathFloor)
            {
                notEventableGrid[beforeStart.x, beforeStart.y] = true;
                Debug.Log($"Marked Not_Eventable_Path at start endpoint: ({beforeStart.x}, {beforeStart.y})");
            }

            // 終了点の後の隣接セルをチェック
            Vector2Int afterEnd = isHorizontal ? new Vector2Int(end.x + 1, end.y) : new Vector2Int(end.x, end.y + 1);
            if (IsInside(afterEnd.x, afterEnd.y) && grid[afterEnd.x, afterEnd.y] == CellType.PathFloor)
            {
                notEventableGrid[afterEnd.x, afterEnd.y] = true;
                Debug.Log($"Marked Not_Eventable_Path at end endpoint: ({afterEnd.x}, {afterEnd.y})");
            }
        }
    }

    private void ApplyTagChanges(bool[,] eventableGrid, bool[,] notEventableGrid)
    {
        int eventableCount = 0;
        int notEventableCount = 0;

        for (int x = 0; x < dungeonSize.x; x++)
        {
            for (int y = 0; y < dungeonSize.y; y++)
            {
                if (eventableGrid[x, y])
                {
                    if (ChangePathTagAtPosition(x, y, "Eventable_Path"))
                    {
                        eventableCount++;
                    }
                }
                else if (notEventableGrid[x, y])
                {
                    if (ChangePathTagAtPosition(x, y, "Not_Eventable_Path"))
                    {
                        notEventableCount++;
                    }
                }
            }
        }

        Debug.Log($"Created {eventableCount} Eventable_Path and {notEventableCount} Not_Eventable_Path tags");
    }

    private bool ChangePathTagAtPosition(int gridX, int gridY, string newTag)
    {
        Vector3 targetPos = new(gridX * cellSize, 0, gridY * cellSize);

        // このGameObjectの子オブジェクトから該当する座標のPathタグオブジェクトを検索
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            // Path, Isolated_Path, Room_Adjacent_Pathタグを対象に検索
            if (child.CompareTag("Path") || child.CompareTag("Isolated_Path") || child.CompareTag("Room_Adjacent_Path"))
            {
                Vector3 childPos = child.position;

                // XZ平面での距離のみを比較（Yは無視）
                float distance = Vector2.Distance(
                    new Vector2(childPos.x, childPos.z),
                    new Vector2(targetPos.x, targetPos.z)
                );

                if (distance < cellSize * 0.1f)  // セルサイズの10%以内
                {
                    string oldTag = child.tag;
                    child.tag = newTag;
                    Debug.Log($"Changed tag at ({gridX}, {gridY}) from '{oldTag}' to '{newTag}'");
                    return true; // 成功
                }
            }
        }

        Debug.LogWarning($"No Path object found at ({gridX}, {gridY}) to change to '{newTag}'");
        return false; // 失敗
    }
    #endregion

    /*==================================================================*/
    #region Helpers
    /*==================================================================*/
    private bool IsInside(int x, int y)
        => x >= 0 && y >= 0 && x < dungeonSize.x && y < dungeonSize.y;

    private void FillRect(RectInt r, CellType type)
    {
        for (int x = r.x; x < r.xMax; x++)
            for (int y = r.y; y < r.yMax; y++)
                grid[x, y] = type;
    }

    private bool IsFloor(int x, int y)
    => IsInside(x, y) && (grid[x, y] == CellType.RoomFloor ||
                          grid[x, y] == CellType.PathFloor);

    private bool IsRoomFloor(int x, int y)
=> IsInside(x, y) && grid[x, y] == CellType.RoomFloor;

    #endregion
}


