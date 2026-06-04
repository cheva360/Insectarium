#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public sealed class PolyShapeNavMeshObstacleTool : EditorWindow
{
    [SerializeField] private GameObject sourceObject;
    [SerializeField] private float obstacleThickness = 0.15f;
    [SerializeField] private float obstacleInset = 0.08f;
    [SerializeField] private float obstacleHeightPadding = 0.05f;
    [SerializeField] private float bottomEdgeTolerance = 0.01f;
    [SerializeField] private bool clearExistingFirst = true;

    [MenuItem("Tools/PolyShape NavMesh Obstacles")]
    public static void OpenWindow()
    {
        GetWindow<PolyShapeNavMeshObstacleTool>("PolyShape Obstacles");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Mesh Object", sourceObject, typeof(GameObject), true);

        if (GUILayout.Button("Use Selected Object"))
            sourceObject = Selection.activeGameObject;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Obstacle Settings", EditorStyles.boldLabel);
        obstacleThickness = EditorGUILayout.FloatField("Thickness", obstacleThickness);
        obstacleInset = EditorGUILayout.FloatField("Inset Into Wall", obstacleInset);
        obstacleHeightPadding = EditorGUILayout.FloatField("Height Padding", obstacleHeightPadding);
        bottomEdgeTolerance = EditorGUILayout.FloatField("Bottom Edge Tolerance", bottomEdgeTolerance);
        clearExistingFirst = EditorGUILayout.Toggle("Clear Existing First", clearExistingFirst);

        EditorGUILayout.Space();

        GUI.enabled = sourceObject != null;
        if (GUILayout.Button("Generate Obstacles"))
            GenerateObstacles();

        if (GUILayout.Button("Clear Generated Obstacles"))
            ClearGeneratedObstacles();
        GUI.enabled = true;
    }

    private void GenerateObstacles()
    {
        if (sourceObject == null)
            return;

        MeshFilter meshFilter = sourceObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("PolyShape Obstacles", "The selected object needs a MeshFilter with a valid mesh.", "OK");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        if (vertices == null || triangles == null || triangles.Length < 3)
            return;

        Transform sourceTransform = meshFilter.transform;
        List<Segment> bottomSegments = GetBottomWallSegments(mesh, bottomEdgeTolerance);
        if (bottomSegments.Count == 0)
        {
            EditorUtility.DisplayDialog("PolyShape Obstacles", "No bottom wall perimeter edges were found on the selected mesh.", "OK");
            return;
        }

        GameObject container = GetOrCreateContainer();
        if (clearExistingFirst)
            ClearContainerChildren(container);

        float localHeight = Mathf.Max(mesh.bounds.size.y + obstacleHeightPadding, 0.01f);
        Vector3 meshCenterWorld = sourceTransform.TransformPoint(mesh.bounds.center);

        for (int i = 0; i < bottomSegments.Count; i++)
        {
            Vector3 worldA = sourceTransform.TransformPoint(bottomSegments[i].A);
            Vector3 worldB = sourceTransform.TransformPoint(bottomSegments[i].B);
            CreateObstacle(container.transform, sourceTransform.up, meshCenterWorld, worldA, worldB, localHeight, obstacleThickness, obstacleInset);
        }

        Selection.activeGameObject = container;
        EditorGUIUtility.PingObject(container);
    }

    private void ClearGeneratedObstacles()
    {
        if (sourceObject == null)
            return;

        Transform existing = sourceObject.transform.Find("GeneratedNavMeshObstacles");
        if (existing == null)
            return;

        Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private GameObject GetOrCreateContainer()
    {
        Transform existing = sourceObject.transform.Find("GeneratedNavMeshObstacles");
        if (existing != null)
            return existing.gameObject;

        GameObject container = new GameObject("GeneratedNavMeshObstacles");
        Undo.RegisterCreatedObjectUndo(container, "Create NavMesh Obstacle Container");
        container.transform.SetParent(sourceObject.transform, false);
        return container;
    }

    private static void ClearContainerChildren(GameObject container)
    {
        for (int i = container.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(container.transform.GetChild(i).gameObject);
    }

    private static void CreateObstacle(
        Transform parent,
        Vector3 upAxis,
        Vector3 meshCenterWorld,
        Vector3 worldA,
        Vector3 worldB,
        float height,
        float thickness,
        float inset)
    {
        Vector3 segment = worldB - worldA;
        float length = segment.magnitude;
        if (length <= 0.0001f)
            return;

        Vector3 forward = segment / length;
        Vector3 side = Vector3.Cross(upAxis.normalized, forward).normalized;
        if (side.sqrMagnitude <= 0.0001f)
            side = Vector3.right;

        Vector3 midpoint = (worldA + worldB) * 0.5f;
        Vector3 towardCenter = meshCenterWorld - midpoint;
        if (Vector3.Dot(side, towardCenter) < 0f)
            side = -side;

        Quaternion rotation = Quaternion.LookRotation(forward, upAxis.normalized);
        Vector3 position = midpoint + upAxis.normalized * (height * 0.5f) + side * inset;

        GameObject obstacleObject = new GameObject("NavMeshObstacleSegment");
        Undo.RegisterCreatedObjectUndo(obstacleObject, "Create NavMesh Obstacle");
        obstacleObject.transform.SetParent(parent, true);
        obstacleObject.transform.SetPositionAndRotation(position, rotation);
        obstacleObject.transform.localScale = Vector3.one;

        NavMeshObstacle obstacle = Undo.AddComponent<NavMeshObstacle>(obstacleObject);
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(Mathf.Max(thickness, 0.01f), Mathf.Max(height, 0.01f), length);
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;
    }

    private static List<Segment> GetBottomWallSegments(Mesh mesh, float tolerance)
    {
        List<Segment> segments = new List<Segment>();
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3[] faceNormals = BuildFaceNormals(mesh);

        float minY = mesh.bounds.min.y;
        Dictionary<Edge, List<int>> edgeMap = new Dictionary<Edge, List<int>>();

        int triangleCount = triangles.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int start = i * 3;
            AddEdge(edgeMap, triangles[start], triangles[start + 1], i);
            AddEdge(edgeMap, triangles[start + 1], triangles[start + 2], i);
            AddEdge(edgeMap, triangles[start + 2], triangles[start], i);
        }

        foreach (KeyValuePair<Edge, List<int>> pair in edgeMap)
        {
            Vector3 a = vertices[pair.Key.A];
            Vector3 b = vertices[pair.Key.B];

            if (Mathf.Abs(a.y - minY) > tolerance || Mathf.Abs(b.y - minY) > tolerance)
                continue;

            bool touchesWallSide = false;
            for (int i = 0; i < pair.Value.Count; i++)
            {
                Vector3 normal = faceNormals[pair.Value[i]];
                if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.5f)
                {
                    touchesWallSide = true;
                    break;
                }
            }

            if (!touchesWallSide)
                continue;

            segments.Add(new Segment(a, b));
        }

        return segments;
    }

    private static Vector3[] BuildFaceNormals(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        int triangleCount = triangles.Length / 3;
        Vector3[] normals = new Vector3[triangleCount];

        for (int i = 0; i < triangleCount; i++)
        {
            int start = i * 3;
            Vector3 a = vertices[triangles[start]];
            Vector3 b = vertices[triangles[start + 1]];
            Vector3 c = vertices[triangles[start + 2]];
            normals[i] = Vector3.Cross(b - a, c - a).normalized;
        }

        return normals;
    }

    private static void AddEdge(Dictionary<Edge, List<int>> edgeMap, int a, int b, int triangleIndex)
    {
        Edge edge = new Edge(a, b);
        if (!edgeMap.TryGetValue(edge, out List<int> connectedTriangles))
        {
            connectedTriangles = new List<int>();
            edgeMap.Add(edge, connectedTriangles);
        }

        connectedTriangles.Add(triangleIndex);
    }

    private readonly struct Segment
    {
        public readonly Vector3 A;
        public readonly Vector3 B;

        public Segment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }
    }

    private readonly struct Edge
    {
        public readonly int A;
        public readonly int B;

        public Edge(int a, int b)
        {
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }
    }
}
#endif