using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NavMeshFacePatchTool : EditorWindow
{
    private const string OutputFolder = "Assets/Generated/NavMeshPatches";

    [SerializeField] private bool toolEnabled = true;
    [SerializeField] private GameObject sourceObject;
    [SerializeField] private float maxAngleDelta = 5f;
    [SerializeField] private bool useFloodFill = false;
    [SerializeField] private bool limitFloodByDistance = true;
    [SerializeField] private float maxFloodDistance = 1.5f;
    [SerializeField] private bool multiSelectMode = false;
    [SerializeField] private int patchLayer = 0;
    [SerializeField] private bool buildNavMeshImmediately = true;

    private MeshCollider sourceMeshCollider;
    private Mesh sourceMesh;
    private Renderer sourceRenderer;
    private int hoveredTriangleIndex = -1;
    private readonly List<int> pendingSelectedTriangles = new List<int>();

    [MenuItem("Tools/NavMesh Face Patch Tool")]
    public static void OpenWindow()
    {
        GetWindow<NavMeshFacePatchTool>("NavMesh Face Patch");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        TryAssignFromSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
        if (sourceObject == null)
        {
            TryAssignFromSelection();
        }

        Repaint();
    }

    private void TryAssignFromSelection()
    {
        sourceObject = Selection.activeGameObject;
        CacheSource();
        pendingSelectedTriangles.Clear();
    }

    private void CacheSource()
    {
        hoveredTriangleIndex = -1;
        sourceMeshCollider = null;
        sourceMesh = null;
        sourceRenderer = null;

        if (sourceObject == null)
        {
            return;
        }

        sourceMeshCollider = sourceObject.GetComponent<MeshCollider>();
        sourceRenderer = sourceObject.GetComponent<Renderer>();

        if (sourceMeshCollider != null)
        {
            sourceMesh = sourceMeshCollider.sharedMesh;
        }
    }

    private void OnGUI()
    {
        toolEnabled = EditorGUILayout.Toggle("Tool Enabled", toolEnabled);

        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            CacheSource();
            pendingSelectedTriangles.Clear();
        }

        if (GUILayout.Button("Use Current Selection"))
        {
            TryAssignFromSelection();
            pendingSelectedTriangles.Clear();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Flood Fill", EditorStyles.boldLabel);
        useFloodFill = EditorGUILayout.Toggle("Use Flood Fill", useFloodFill);
        maxAngleDelta = EditorGUILayout.Slider("Max Angle Delta", maxAngleDelta, 0f, 45f);
        limitFloodByDistance = EditorGUILayout.Toggle("Limit By Distance", limitFloodByDistance);

        if (limitFloodByDistance)
        {
            maxFloodDistance = EditorGUILayout.FloatField("Max Flood Distance", maxFloodDistance);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
        multiSelectMode = EditorGUILayout.Toggle("Multi Select Mode", multiSelectMode);

        if (multiSelectMode)
        {
            EditorGUILayout.LabelField($"Queued Faces: {pendingSelectedTriangles.Count}");

            if (GUILayout.Button("Clear Queued Faces"))
            {
                pendingSelectedTriangles.Clear();
            }

            GUI.enabled = pendingSelectedTriangles.Count > 0;
            if (GUILayout.Button("Create Patch From Queued Faces"))
            {
                CreatePatchFromTriangles(pendingSelectedTriangles);
                pendingSelectedTriangles.Clear();
            }
            GUI.enabled = true;
        }

        patchLayer = EditorGUILayout.LayerField("Patch Layer", patchLayer);
        buildNavMeshImmediately = EditorGUILayout.Toggle("Build Immediately", buildNavMeshImmediately);

        EditorGUILayout.Space();

        if (!toolEnabled)
        {
            EditorGUILayout.HelpBox("Enable the tool to use Scene view selection.", MessageType.Info);
            return;
        }

        if (sourceObject == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a MeshCollider, then Shift+Left Click a face in the Scene view.", MessageType.Info);
            return;
        }

        if (sourceMeshCollider == null || sourceMesh == null)
        {
            EditorGUILayout.HelpBox("The source object needs a MeshCollider with a valid shared mesh.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("Shift+Left Click a triangle in the Scene view. With multi select enabled, clicks queue faces and build one seamless patch.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!toolEnabled)
        {
            hoveredTriangleIndex = -1;
            return;
        }

        CacheSource();

        if (sourceObject == null || sourceMeshCollider == null || sourceMesh == null)
        {
            return;
        }

        Event currentEvent = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

        if (!sourceMeshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            hoveredTriangleIndex = -1;
            return;
        }

        hoveredTriangleIndex = hit.triangleIndex;
        DrawTriangleOutline(sourceMesh, sourceObject.transform.localToWorldMatrix, hoveredTriangleIndex);

        if (multiSelectMode)
        {
            DrawQueuedTriangleOutlines(sourceMesh, sourceObject.transform.localToWorldMatrix, pendingSelectedTriangles);
        }

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && currentEvent.shift)
        {
            List<int> clickedTriangles = useFloodFill
                ? FloodFillTriangles(
                    hit.triangleIndex,
                    sourceMesh.triangles,
                    sourceMesh.vertices,
                    BuildFaceNormals(sourceMesh),
                    BuildTriangleAdjacency(sourceMesh.triangles),
                    sourceObject.transform.InverseTransformPoint(hit.point))
                : new List<int> { hit.triangleIndex };

            if (multiSelectMode)
            {
                AddTrianglesToPendingSelection(clickedTriangles);
            }
            else
            {
                CreatePatchFromTriangles(clickedTriangles);
            }

            currentEvent.Use();
        }
    }

    private void DrawQueuedTriangleOutlines(Mesh mesh, Matrix4x4 localToWorldMatrix, List<int> triangleIndices)
    {
        Handles.zTest = CompareFunction.LessEqual;
        Handles.color = Color.yellow;

        for (int i = 0; i < triangleIndices.Count; i++)
        {
            int triangleIndex = triangleIndices[i];
            int[] triangles = mesh.triangles;
            int start = triangleIndex * 3;

            if (start < 0 || start + 2 >= triangles.Length)
            {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3 a = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start]]);
            Vector3 b = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start + 1]]);
            Vector3 c = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start + 2]]);

            Handles.DrawAAPolyLine(3f, a, b, c, a);
        }
    }

    private void AddTrianglesToPendingSelection(List<int> trianglesToAdd)
    {
        for (int i = 0; i < trianglesToAdd.Count; i++)
        {
            int triangleIndex = trianglesToAdd[i];
            if (!pendingSelectedTriangles.Contains(triangleIndex))
            {
                pendingSelectedTriangles.Add(triangleIndex);
            }
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void CreatePatchFromTriangles(List<int> selectedTriangles)
    {
        if (selectedTriangles == null || selectedTriangles.Count == 0)
        {
            return;
        }

        int[] sourceTriangles = sourceMesh.triangles;
        Vector3[] sourceVertices = sourceMesh.vertices;
        Vector3[] faceNormals = BuildFaceNormals(sourceMesh);

        GetPatchTransformWorld(
            sourceTriangles,
            sourceVertices,
            faceNormals,
            selectedTriangles,
            out Vector3 patchWorldCenter,
            out Quaternion patchWorldRotation);

        Mesh patchMesh = BuildPatchMesh(
            sourceMesh,
            sourceTriangles,
            selectedTriangles,
            sourceObject.transform,
            patchWorldCenter,
            patchWorldRotation);

        if (patchMesh == null)
        {
            return;
        }

        EnsureFolder(OutputFolder);

        string meshAssetPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{sourceObject.name}_NavMeshPatch.asset");
        AssetDatabase.CreateAsset(patchMesh, meshAssetPath);
        AssetDatabase.SaveAssets();

        GameObject container = new GameObject($"{sourceObject.name}_NavMeshPatch");
        Undo.RegisterCreatedObjectUndo(container, "Create NavMesh Face Patch");
        container.transform.SetPositionAndRotation(patchWorldCenter, patchWorldRotation);
        container.transform.SetParent(sourceObject.transform.parent, true);
        container.layer = patchLayer;

        GameObject patchObject = new GameObject("PatchMesh");
        Undo.RegisterCreatedObjectUndo(patchObject, "Create NavMesh Face Patch");
        patchObject.transform.SetParent(container.transform, false);
        patchObject.transform.localPosition = Vector3.zero;
        patchObject.transform.localRotation = Quaternion.identity;
        patchObject.transform.localScale = Vector3.one;
        patchObject.layer = patchLayer;

        MeshFilter meshFilter = Undo.AddComponent<MeshFilter>(patchObject);
        meshFilter.sharedMesh = patchMesh;

        MeshRenderer meshRenderer = Undo.AddComponent<MeshRenderer>(patchObject);
        if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
        {
            meshRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        NavMeshSurface surface = Undo.AddComponent<NavMeshSurface>(container);
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = 1 << patchLayer;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.04f;
        surface.overrideTileSize = true;
        surface.tileSize = 64;

        if (buildNavMeshImmediately)
        {
            surface.BuildNavMesh();
        }

        Selection.activeGameObject = container;
        EditorGUIUtility.PingObject(container);
    }

    private void GetPatchTransformWorld(
        int[] sourceTriangles,
        Vector3[] sourceVertices,
        Vector3[] faceNormals,
        List<int> selectedTriangles,
        out Vector3 worldCenter,
        out Quaternion worldRotation)
    {
        HashSet<int> uniqueVertexIndices = new HashSet<int>();
        List<Vector3> worldVertices = new List<Vector3>();
        worldCenter = Vector3.zero;
        Vector3 worldNormal = Vector3.zero;

        for (int triangleListIndex = 0; triangleListIndex < selectedTriangles.Count; triangleListIndex++)
        {
            int triangleIndex = selectedTriangles[triangleListIndex];
            worldNormal += sourceObject.transform.TransformDirection(faceNormals[triangleIndex]);

            int start = triangleIndex * 3;
            for (int i = 0; i < 3; i++)
            {
                int vertexIndex = sourceTriangles[start + i];
                if (uniqueVertexIndices.Add(vertexIndex))
                {
                    Vector3 worldVertex = sourceObject.transform.TransformPoint(sourceVertices[vertexIndex]);
                    worldVertices.Add(worldVertex);
                    worldCenter += worldVertex;
                }
            }
        }

        if (worldVertices.Count > 0)
        {
            worldCenter /= worldVertices.Count;
        }
        else
        {
            worldCenter = sourceObject.transform.position;
        }

        if (worldNormal.sqrMagnitude <= 0.0001f)
        {
            worldNormal = sourceObject.transform.up;
        }
        else
        {
            worldNormal.Normalize();
        }

        GetStablePlaneBasis(worldNormal, out Vector3 basisRight, out Vector3 basisForward);

        float xx = 0f;
        float xz = 0f;
        float zz = 0f;

        for (int i = 0; i < worldVertices.Count; i++)
        {
            Vector3 offset = Vector3.ProjectOnPlane(worldVertices[i] - worldCenter, worldNormal);
            float x = Vector3.Dot(offset, basisRight);
            float z = Vector3.Dot(offset, basisForward);

            xx += x * x;
            xz += x * z;
            zz += z * z;
        }

        float angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
        Vector3 principalAxis = (basisRight * Mathf.Cos(angle)) + (basisForward * Mathf.Sin(angle));

        if (principalAxis.sqrMagnitude <= 0.0001f)
        {
            principalAxis = basisForward;
        }

        Vector3 referenceAxis = Vector3.ProjectOnPlane(sourceObject.transform.up, worldNormal);
        if (referenceAxis.sqrMagnitude <= 0.0001f)
        {
            referenceAxis = Vector3.ProjectOnPlane(sourceObject.transform.right, worldNormal);
        }

        if (referenceAxis.sqrMagnitude > 0.0001f && Vector3.Dot(principalAxis, referenceAxis) < 0f)
        {
            principalAxis = -principalAxis;
        }

        principalAxis.Normalize();

        Vector3 worldRight = principalAxis;
        Vector3 worldForward = Vector3.Cross(worldRight, worldNormal).normalized;

        if (worldForward.sqrMagnitude <= 0.0001f)
        {
            worldForward = basisForward;
        }

        worldRotation = Quaternion.LookRotation(worldForward, worldNormal);
    }

    private static void GetStablePlaneBasis(Vector3 planeNormal, out Vector3 basisRight, out Vector3 basisForward)
    {
        Vector3 seedAxis = Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up)) < 0.999f
            ? Vector3.up
            : Vector3.right;

        basisRight = Vector3.Cross(seedAxis, planeNormal).normalized;

        if (basisRight.sqrMagnitude <= 0.0001f)
        {
            seedAxis = Vector3.forward;
            basisRight = Vector3.Cross(seedAxis, planeNormal).normalized;
        }

        basisForward = Vector3.Cross(planeNormal, basisRight).normalized;
    }

    private static Mesh BuildPatchMesh(
        Mesh source,
        int[] sourceTriangles,
        List<int> selectedTriangles,
        Transform sourceTransform,
        Vector3 patchWorldCenter,
        Quaternion patchWorldRotation)
    {
        Vector3[] sourceVertices = source.vertices;
        Vector2[] sourceUvs = source.uv;

        Matrix4x4 worldToPatch = Matrix4x4.TRS(patchWorldCenter, patchWorldRotation, Vector3.one).inverse;

        Dictionary<int, int> vertexMap = new Dictionary<int, int>();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        foreach (int triangleIndex in selectedTriangles)
        {
            int start = triangleIndex * 3;

            for (int i = 0; i < 3; i++)
            {
                int sourceVertexIndex = sourceTriangles[start + i];

                if (!vertexMap.TryGetValue(sourceVertexIndex, out int newVertexIndex))
                {
                    newVertexIndex = vertices.Count;
                    vertexMap.Add(sourceVertexIndex, newVertexIndex);

                    Vector3 worldVertex = sourceTransform.TransformPoint(sourceVertices[sourceVertexIndex]);
                    Vector3 localVertex = worldToPatch.MultiplyPoint3x4(worldVertex);

                    vertices.Add(new Vector3(localVertex.x, 0f, localVertex.z));
                    normals.Add(Vector3.up);

                    if (sourceUvs != null && sourceUvs.Length == sourceVertices.Length)
                    {
                        uvs.Add(sourceUvs[sourceVertexIndex]);
                    }
                }

                triangles.Add(newVertexIndex);
            }
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            return null;
        }

        Mesh mesh = new Mesh
        {
            name = "NavMeshFacePatch"
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);

        if (uvs.Count == vertices.Count)
        {
            mesh.SetUVs(0, uvs);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private void DrawTriangleOutline(Mesh mesh, Matrix4x4 localToWorldMatrix, int triangleIndex)
    {
        int[] triangles = mesh.triangles;
        int start = triangleIndex * 3;

        if (start < 0 || start + 2 >= triangles.Length)
        {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        Vector3 a = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start]]);
        Vector3 b = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start + 1]]);
        Vector3 c = localToWorldMatrix.MultiplyPoint3x4(vertices[triangles[start + 2]]);

        Handles.zTest = CompareFunction.LessEqual;
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(4f, a, b, c, a);
    }

    private List<int> FloodFillTriangles(
        int startTriangleIndex,
        int[] triangles,
        Vector3[] vertices,
        Vector3[] faceNormals,
        List<int>[] neighbors,
        Vector3 localHitPoint)
    {
        int triangleCount = triangles.Length / 3;
        if (startTriangleIndex < 0 || startTriangleIndex >= triangleCount)
        {
            return new List<int>();
        }

        float minDot = Mathf.Cos(maxAngleDelta * Mathf.Deg2Rad);
        float maxDistanceSqr = maxFloodDistance * maxFloodDistance;
        Vector3 startNormal = faceNormals[startTriangleIndex];

        Queue<int> queue = new Queue<int>();
        bool[] visited = new bool[triangleCount];
        List<int> result = new List<int>();

        queue.Enqueue(startTriangleIndex);
        visited[startTriangleIndex] = true;

        while (queue.Count > 0)
        {
            int triangle = queue.Dequeue();

            Vector3 triangleCenter = GetTriangleCenter(triangles, vertices, triangle);
            if (limitFloodByDistance && (triangleCenter - localHitPoint).sqrMagnitude > maxDistanceSqr)
            {
                continue;
            }

            result.Add(triangle);

            foreach (int neighbor in neighbors[triangle])
            {
                if (visited[neighbor])
                {
                    continue;
                }

                if (Vector3.Dot(startNormal, faceNormals[neighbor]) < minDot)
                {
                    continue;
                }

                visited[neighbor] = true;
                queue.Enqueue(neighbor);
            }
        }

        return result;
    }

    private static Vector3 GetTriangleCenter(int[] triangles, Vector3[] vertices, int triangleIndex)
    {
        int start = triangleIndex * 3;
        Vector3 a = vertices[triangles[start]];
        Vector3 b = vertices[triangles[start + 1]];
        Vector3 c = vertices[triangles[start + 2]];
        return (a + b + c) / 3f;
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

    private static List<int>[] BuildTriangleAdjacency(int[] triangles)
    {
        int triangleCount = triangles.Length / 3;
        List<int>[] neighbors = new List<int>[triangleCount];

        for (int i = 0; i < triangleCount; i++)
        {
            neighbors[i] = new List<int>();
        }

        Dictionary<Edge, List<int>> edgeMap = new Dictionary<Edge, List<int>>();

        for (int i = 0; i < triangleCount; i++)
        {
            int start = i * 3;
            AddEdge(edgeMap, triangles[start], triangles[start + 1], i);
            AddEdge(edgeMap, triangles[start + 1], triangles[start + 2], i);
            AddEdge(edgeMap, triangles[start + 2], triangles[start], i);
        }

        foreach (KeyValuePair<Edge, List<int>> pair in edgeMap)
        {
            List<int> connected = pair.Value;
            for (int i = 0; i < connected.Count; i++)
            {
                for (int j = i + 1; j < connected.Count; j++)
                {
                    int a = connected[i];
                    int b = connected[j];

                    if (!neighbors[a].Contains(b))
                    {
                        neighbors[a].Add(b);
                    }

                    if (!neighbors[b].Contains(a))
                    {
                        neighbors[b].Add(a);
                    }
                }
            }
        }

        return neighbors;
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