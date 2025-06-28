using System.Collections.Generic;
using UnityEngine;

public class MST
{
    public static List<Delaunay2D.Edge> Kruskal(List<Vector2> points, List<Delaunay2D.Edge> edges)
    {
        // クラスカル法によるMST構築
        var parent = new int[points.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { return parent[x] == x ? x : parent[x] = Find(parent[x]); }
        void Union(int x, int y) { parent[Find(x)] = Find(y); }

        // エッジを距離順にソート
        var sorted = new List<Delaunay2D.Edge>(edges);
        sorted.Sort((e1, e2) =>
            Vector2.Distance(points[e1.a], points[e1.b]).CompareTo(
            Vector2.Distance(points[e2.a], points[e2.b])));

        var mst = new List<Delaunay2D.Edge>();
        foreach (var e in sorted)
        {
            if (Find(e.a) != Find(e.b))
            {
                mst.Add(e);
                Union(e.a, e.b);
            }
        }
        return mst;
    }
}