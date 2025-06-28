using System.Collections.Generic;
using UnityEngine;

public class Delaunay2D
{
    public struct Edge { public int a, b; public Edge(int a, int b) { this.a = a; this.b = b; } }
    public struct Triangle { public int a, b, c; public Triangle(int a, int b, int c) { this.a = a; this.b = b; this.c = c; } }

    public static List<Triangle> Triangulate(List<Vector2> points)
    {
        // Bowyer-Watsonアルゴリズムによる簡易Delaunay三角分割
        var triangles = new List<Triangle>();
        if (points.Count < 3) return triangles;
        // 1. 大きなスーパー三角形を作る
        float minX = points[0].x, maxX = points[0].x, minY = points[0].y, maxY = points[0].y;
        foreach (var p in points)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }
        float dx = maxX - minX, dy = maxY - minY, deltaMax = Mathf.Max(dx, dy) * 10f;
        Vector2 p1 = new Vector2(minX - 1, minY - 1);
        Vector2 p2 = new Vector2(minX - 1, maxY + deltaMax);
        Vector2 p3 = new Vector2(maxX + deltaMax, minY - 1);
        var super = new List<Vector2>(points) { p1, p2, p3 };
        triangles.Add(new Triangle(super.Count - 3, super.Count - 2, super.Count - 1));
        // 2. 各点を追加
        for (int i = 0; i < points.Count; i++)
        {
            var edges = new List<Edge>();
            var badTriangles = new List<Triangle>();
            foreach (var t in triangles)
            {
                if (InCircle(super[t.a], super[t.b], super[t.c], points[i]))
                {
                    badTriangles.Add(t);
                    edges.Add(new Edge(t.a, t.b));
                    edges.Add(new Edge(t.b, t.c));
                    edges.Add(new Edge(t.c, t.a));
                }
            }
            // 重複エッジを除去
            var uniqueEdges = new List<Edge>();
            foreach (var e in edges)
            {
                bool found = false;
                foreach (var e2 in uniqueEdges)
                {
                    if ((e.a == e2.a && e.b == e2.b) || (e.a == e2.b && e.b == e2.a)) { found = true; break; }
                }
                if (!found) uniqueEdges.Add(e);
            }
            foreach (var t in badTriangles) triangles.Remove(t);
            foreach (var e in uniqueEdges) triangles.Add(new Triangle(e.a, e.b, i));
        }
        // 3. スーパー三角形に関わる三角形を除去
        triangles.RemoveAll(t => t.a >= points.Count || t.b >= points.Count || t.c >= points.Count);
        return triangles;
    }

    static bool InCircle(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
    {
        float ax = a.x - p.x, ay = a.y - p.y;
        float bx = b.x - p.x, by = b.y - p.y;
        float cx = c.x - p.x, cy = c.y - p.y;
        float det = (ax * ax + ay * ay) * (bx * cy - cx * by)
                 - (bx * bx + by * by) * (ax * cy - cx * ay)
                 + (cx * cx + cy * cy) * (ax * by - bx * ay);
        return det > 0f;
    }

    public static List<Edge> GetEdges(List<Triangle> triangles)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var t in triangles)
        {
            AddEdge(edges, t.a, t.b);
            AddEdge(edges, t.b, t.c);
            AddEdge(edges, t.c, t.a);
        }
        var result = new List<Edge>();
        foreach (var e in edges) result.Add(new Edge(e.Item1, e.Item2));
        return result;
    }
    static void AddEdge(HashSet<(int, int)> set, int a, int b)
    {
        if (a < b) set.Add((a, b));
        else set.Add((b, a));
    }
}