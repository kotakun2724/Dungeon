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
    [Header("Prefabs")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject wallPrefab;

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

    [Header("Tile / Scale")]
    [SerializeField] private float cellSize = 4.55f;     // 1 マス = 4.55 m
    [SerializeField] private bool autoOffsets = true;     // 厚み自動補正

    [Header("Debug / Seed")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 0;

    /*──────────────────────────── Internal ───────────────────────────*/
    private enum CellType { Empty, RoomFloor, PathFloor }              // 壁セルを廃止

    private CellType[,] grid;
    private readonly List<RectInt> rooms = new();
    private readonly List<Vector2Int> roomCenters = new();
    private System.Random rng;

    // オフセット & スナップ用
    private float halfCell, floorHalfT, wallHalfT, thicknessDiff;
    private float floorYOffset, wallYOffset;

    /*==================================================================*/
    #region Public API
    /*==================================================================*/
    public void Generate()
    {
        CalcOffsetsAndThickness();
        Clear();

        rng = randomSeed ? new System.Random() : new System.Random(seed);

        SplitArea(new RectInt(Vector2Int.zero, dungeonSize));
        CarveRooms();
        ConnectAllRooms();
        InstantiateFloors();
        InstantiateWalls();        // ← 境界走査で壁生成
    }

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
    }
    #endregion

    /*==================================================================*/
    #region  Offset & Thickness
    /*==================================================================*/
    private void CalcOffsetsAndThickness()
    {
        halfCell = cellSize * .5f;
        floorHalfT = floorPrefab.transform.localScale.y * .5f;
        wallHalfT = wallPrefab.transform.localScale.z * .5f;  // 壁 Cube は X=90° 回転前提

        thicknessDiff = floorHalfT - wallHalfT;   // 正なら床の方が厚い

        if (!autoOffsets) { floorYOffset = wallYOffset = 0f; return; }

        // Pivot = 中央 -> 床: −半厚, 壁: +半厚 (下面を y=0)
        floorYOffset = -floorHalfT;
        wallYOffset = wallHalfT;
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
        if (straightCorridors && (a.x == b.x || a.y == b.y))
        {
            CarveLine(a, b);
            return;
        }

        bool horizFirst = rng.Next(2) == 0;
        Vector2Int c = horizFirst ? new Vector2Int(b.x, a.y)
                                  : new Vector2Int(a.x, b.y);
        CarveLine(a, c);
        CarveLine(c, b);
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
                var go = Instantiate(floorPrefab,
                                     pos + Vector3.up * floorYOffset,
                                     Quaternion.identity, transform);

                /* タグ付け */
                if (t == CellType.RoomFloor) go.tag = "Room";
                else go.tag = "Path";
            }
    }


    private void InstantiateWalls()
    {
        Quaternion rotZ = wallPrefab.transform.rotation;            // 壁面Z
        Quaternion rotX = Quaternion.Euler(0, 90, 0) * rotZ;        // 壁面X

        for (int x = 0; x < dungeonSize.x; x++)
            for (int y = 0; y < dungeonSize.y; y++)
            {
                if (grid[x, y] == CellType.Empty) continue;

                Vector3 center = new(x * cellSize, 0f, y * cellSize);

                // 北辺
                if (!IsInside(x, y + 1) || grid[x, y + 1] == CellType.Empty)
                    PlaceWall(center, Vector3.forward, rotX);
                // 東辺
                if (!IsInside(x + 1, y) || grid[x + 1, y] == CellType.Empty)
                    PlaceWall(center, Vector3.right, rotZ);
                // 南辺
                if (!IsInside(x, y - 1) || grid[x, y - 1] == CellType.Empty)
                    PlaceWall(center, Vector3.back, rotX);
                // 西辺
                if (!IsInside(x - 1, y) || grid[x - 1, y] == CellType.Empty)
                    PlaceWall(center, Vector3.left, rotZ);
            }

        void PlaceWall(Vector3 c, Vector3 dir, Quaternion rot)
        {
            float sign = (dir == Vector3.forward || dir == Vector3.right) ? 1f : -1f;
            Vector3 offset = (dir.x != 0)
                ? new Vector3(sign * (halfCell + wallHalfT + thicknessDiff), 0, 0)
                : new Vector3(0, 0, sign * (halfCell + wallHalfT + thicknessDiff));

            Instantiate(wallPrefab,
                        c + offset + Vector3.up * wallYOffset,
                        rot, transform);
        }
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
