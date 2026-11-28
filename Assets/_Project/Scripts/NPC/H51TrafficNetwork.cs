using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51TrafficNetwork : MonoBehaviour
    {
        [Serializable]
        public struct Edge
        {
            public int a;
            public int b;
            public float cost;

            public Edge(int from, int to, float edgeCost)
            {
                a = from;
                b = to;
                cost = edgeCost;
            }
        }

        [SerializeField] Vector3[] nodes = Array.Empty<Vector3>();
        [SerializeField] Edge[] edges = Array.Empty<Edge>();

        public int NodeCount => nodes != null ? nodes.Length : 0;
        public IReadOnlyList<Vector3> Nodes => nodes;

        public void Configure(List<Vector3> configuredNodes, List<Edge> configuredEdges)
        {
            nodes = configuredNodes != null ? configuredNodes.ToArray() : Array.Empty<Vector3>();
            edges = configuredEdges != null ? configuredEdges.ToArray() : Array.Empty<Edge>();
        }

        public int FindNearestNode(Vector3 point)
        {
            if (nodes == null || nodes.Length == 0) return -1;

            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 delta = nodes[i] - point;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }
            return best;
        }

        public Vector3 GetNode(int index)
        {
            return nodes != null && index >= 0 && index < nodes.Length
                ? nodes[index]
                : transform.position;
        }

        public bool TryBuildPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            if (result == null) return false;
            result.Clear();

            int start = FindNearestNode(from);
            int goal = FindNearestNode(to);
            if (start < 0 || goal < 0) return false;

            if (start == goal)
            {
                result.Add(nodes[start]);
                return true;
            }

            int count = nodes.Length;
            float[] g = new float[count];
            float[] f = new float[count];
            int[] previous = new int[count];
            bool[] closed = new bool[count];
            List<int> open = new List<int>(Mathf.Min(count, 128));

            for (int i = 0; i < count; i++)
            {
                g[i] = float.PositiveInfinity;
                f[i] = float.PositiveInfinity;
                previous[i] = -1;
            }

            g[start] = 0f;
            f[start] = Heuristic(start, goal);
            open.Add(start);

            while (open.Count > 0)
            {
                int current = open[0];
                int currentSlot = 0;
                float bestF = f[current];
                for (int i = 1; i < open.Count; i++)
                {
                    int candidate = open[i];
                    if (f[candidate] >= bestF) continue;
                    bestF = f[candidate];
                    current = candidate;
                    currentSlot = i;
                }

                open.RemoveAt(currentSlot);
                if (current == goal)
                {
                    Reconstruct(previous, goal, result);
                    return result.Count > 0;
                }

                closed[current] = true;

                for (int e = 0; e < edges.Length; e++)
                {
                    Edge edge = edges[e];
                    int neighbor = -1;
                    if (edge.a == current) neighbor = edge.b;
                    else if (edge.b == current) neighbor = edge.a;
                    if (neighbor < 0 || neighbor >= count || closed[neighbor]) continue;

                    float step = edge.cost > .01f
                        ? edge.cost
                        : Vector3.Distance(nodes[current], nodes[neighbor]);
                    float tentative = g[current] + step;
                    if (tentative >= g[neighbor]) continue;

                    previous[neighbor] = current;
                    g[neighbor] = tentative;
                    f[neighbor] = tentative + Heuristic(neighbor, goal);
                    if (!open.Contains(neighbor)) open.Add(neighbor);
                }
            }

            return false;
        }

        float Heuristic(int a, int b)
        {
            Vector3 delta = nodes[a] - nodes[b];
            delta.y = 0f;
            return delta.magnitude;
        }

        void Reconstruct(int[] previous, int goal, List<Vector3> result)
        {
            List<int> reversed = new List<int>();
            int current = goal;
            int guard = 0;
            while (current >= 0 && guard++ < previous.Length + 2)
            {
                reversed.Add(current);
                current = previous[current];
            }

            for (int i = reversed.Count - 1; i >= 0; i--)
                result.Add(nodes[reversed[i]]);
        }
    }
}
