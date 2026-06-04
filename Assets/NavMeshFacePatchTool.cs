#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
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
    [SerializeField] private bool autoLinkNearbyPatches = true;
    [SerializeField] private bool restrictLinksToCurrentSource = true;
    [SerializeField] private float maxAutoLinkDistance = 0.5f;
    [SerializeField] private float autoLinkWidth = 0.08f;
    [SerializeField] private float autoLinkHeight = 0.05f;

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
        if (toolEnabled)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }
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

        if (sourceMeshCollider != null && sourceMeshCollider.sharedMesh != null)
        {
            sourceMesh = sourceMeshCollider.sharedMesh;
            return;
        }

        MeshFilter meshFilter = sourceObject.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            sourceMesh = meshFilter.sharedMesh;
        }
    }

    private void OnGUI()
    {
        bool newToolEnabled = EditorGUILayout.Toggle("Tool Enabled", toolEnabled);
        if (newToolEnabled != toolEnabled)
        {
            toolEnabled = newToolEnabled;
            if (toolEnabled)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;
            }
            else
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                if (GUIUtility.hotControl != 0)
                    GUIUtility.hotControl = 0;
            }
        }

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
        EditorGUILayout.LabelField("Linking", EditorStyles.boldLabel);
        autoLinkNearbyPatches = EditorGUILayout.Toggle("Auto Link Nearby Patches", autoLinkNearbyPatches);
        restrictLinksToCurrentSource = EditorGUILayout.Toggle("Restrict To Current Source", restrictLinksToCurrentSource);
        maxAutoLinkDistance = EditorGUILayout.FloatField("Max Link Distance", maxAutoLinkDistance);
        autoLinkWidth = EditorGUILayout.FloatField("Link Width", autoLinkWidth);
        autoLinkHeight = EditorGUILayout.FloatField("Link Height (Separation)", autoLinkHeight);

        if (GUILayout.Button("Rebuild All Patch Links"))
        {
            RebuildAllPatchLinks();
        }

        if (!toolEnabled)
        {
            EditorGUILayout.HelpBox("Enable the tool to use Scene view selection.", MessageType.Info);
            return;
        }

        if (sourceObject == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a valid mesh, then Shift+Left Click a face in the Scene view.", MessageType.Info);
            return;
        }

        if (sourceMeshCollider == null || sourceMesh == null)
        {
            EditorGUILayout.HelpBox("The source object needs a valid mesh from either MeshCollider.sharedMesh or MeshFilter.sharedMesh.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("Shift+Left Click a triangle in the Scene view. With multi select enabled, clicks queue faces and build one seamless patch.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!toolEnabled)
        {
            hoveredTriangleIndex = -1;
            // Release any lingering hotControl so scene drag is never stuck.
            if (GUIUtility.hotControl != 0)
                GUIUtility.hotControl = 0;
            return;
        }

        CacheSource();

        if (sourceObject == null || sourceMeshCollider == null || sourceMesh == null)
            return;

        Event currentEvent = Event.current;

        // Only intercept input when Shift is held — otherwise let the scene
        // view handle all mouse events (drag to pan/orbit etc.) normally.
        if (!currentEvent.shift)
        {
            hoveredTriangleIndex = -1;
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

        if (!sourceMeshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            hoveredTriangleIndex = -1;
            return;
        }

        hoveredTriangleIndex = hit.triangleIndex;
        DrawTriangleOutline(sourceMesh, sourceObject.transform.localToWorldMatrix, hoveredTriangleIndex);

        if (multiSelectMode)
            DrawQueuedTriangleOutlines(sourceMesh, sourceObject.transform.localToWorldMatrix, pendingSelectedTriangles);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
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
                AddTrianglesToPendingSelection(clickedTriangles);
            else
                CreatePatchFromTriangles(clickedTriangles);

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

        List<int> filteredTriangles = FilterTrianglesInsideBlockingGeometry(
            selectedTriangles,
            sourceTriangles,
            sourceVertices,
            faceNormals);

        if (filteredTriangles.Count == 0)
            return;

        GetPatchTransformWorld(
            sourceTriangles,
            sourceVertices,
            faceNormals,
            filteredTriangles,
            out Vector3 patchWorldCenter,
            out Quaternion patchWorldRotation);

        Mesh patchMesh = BuildPatchMesh(
            sourceMesh,
            sourceTriangles,
            filteredTriangles,
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
        container.transform.SetParent(sourceObject.transform, true);
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

        if (autoLinkNearbyPatches)
        {
            RebuildAllPatchLinks();
        }
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

        for (int triangleListIndex = 0; triangleListIndex < selectedTriangles.Count; triangleListIndex++)
        {
            int triangleIndex = selectedTriangles[triangleListIndex];
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

        int anchorTriangleIndex = selectedTriangles[0];
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < selectedTriangles.Count; i++)
        {
            int triangleIndex = selectedTriangles[i];
            int start = triangleIndex * 3;

            Vector3 a = sourceObject.transform.TransformPoint(sourceVertices[sourceTriangles[start]]);
            Vector3 b = sourceObject.transform.TransformPoint(sourceVertices[sourceTriangles[start + 1]]);
            Vector3 c = sourceObject.transform.TransformPoint(sourceVertices[sourceTriangles[start + 2]]);
            Vector3 triangleCenter = (a + b + c) / 3f;

            float distanceSqr = (triangleCenter - worldCenter).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                anchorTriangleIndex = triangleIndex;
            }
        }

        Vector3 worldUp = sourceObject.transform.TransformDirection(faceNormals[anchorTriangleIndex]).normalized;
        if (worldUp.sqrMagnitude <= 0.0001f)
        {
            worldUp = sourceObject.transform.up;
        }

        Vector3 worldForward = Vector3.ProjectOnPlane(sourceObject.transform.forward, worldUp);
        if (worldForward.sqrMagnitude <= 0.0001f)
        {
            worldForward = Vector3.ProjectOnPlane(sourceObject.transform.right, worldUp);
        }

        if (worldForward.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallbackAxis = Mathf.Abs(Vector3.Dot(worldUp, Vector3.up)) < 0.999f
                ? Vector3.up
                : Vector3.right;

            worldForward = Vector3.Cross(fallbackAxis, worldUp);
        }

        worldForward.Normalize();
        worldRotation = Quaternion.LookRotation(worldForward, worldUp);
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
        Vector3[] sourceNormals = source.normals;
        Vector2[] sourceUv = source.uv;

        Matrix4x4 worldToPatch = Matrix4x4.TRS(patchWorldCenter, patchWorldRotation, Vector3.one).inverse;
        Quaternion patchWorldRotationInverse = Quaternion.Inverse(patchWorldRotation);

        Dictionary<int, int> vertexMap = new Dictionary<int, int>();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        bool hasSourceNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
        bool hasSourceUvs = sourceUv != null && sourceUv.Length == sourceVertices.Length;

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
                    vertices.Add(localVertex);

                    if (hasSourceNormals)
                    {
                        Vector3 worldNormal = sourceTransform.TransformDirection(sourceNormals[sourceVertexIndex]).normalized;
                        Vector3 localNormal = (patchWorldRotationInverse * worldNormal).normalized;
                        normals.Add(localNormal);
                    }

                    if (hasSourceUvs)
                    {
                        uvs.Add(sourceUv[sourceVertexIndex]);
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

        if (hasSourceNormals && normals.Count == vertices.Count)
        {
            mesh.SetNormals(normals);
        }
        else
        {
            mesh.RecalculateNormals();
        }

        if (hasSourceUvs && uvs.Count == vertices.Count)
        {
            mesh.SetUVs(0, uvs);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Dictionary<int, Vector2> BuildProjectedPatchPositions(
        int[] sourceTriangles,
        List<int> selectedTriangles,
        Vector3[] sourceVertices,
        Transform sourceTransform,
        Matrix4x4 worldToPatch)
    {
        Dictionary<int, Vector2> projectedPositions = new Dictionary<int, Vector2>();

        for (int i = 0; i < selectedTriangles.Count; i++)
        {
            int start = selectedTriangles[i] * 3;

            for (int j = 0; j < 3; j++)
            {
                int sourceVertexIndex = sourceTriangles[start + j];
                if (projectedPositions.ContainsKey(sourceVertexIndex))
                {
                    continue;
                }

                Vector3 worldVertex = sourceTransform.TransformPoint(sourceVertices[sourceVertexIndex]);
                Vector3 localVertex = worldToPatch.MultiplyPoint3x4(worldVertex);
                projectedPositions.Add(sourceVertexIndex, new Vector2(localVertex.x, localVertex.z));
            }
        }

        return projectedPositions;
    }

    private static Dictionary<int, Vector2> BuildUnwrappedPatchPositions(
        int[] sourceTriangles,
        List<int> selectedTriangles,
        Vector3[] sourceVertices,
        Transform sourceTransform,
        Dictionary<int, Vector2> projectedPositions)
    {
        Dictionary<int, Vector2> unwrappedPositions = new Dictionary<int, Vector2>();

        if (selectedTriangles.Count == 0)
        {
            return unwrappedPositions;
        }

        GetTriangleVertexIndices(sourceTriangles, selectedTriangles[0], out int rootA, out int rootB, out int rootC);
        unwrappedPositions[rootA] = projectedPositions[rootA];
        unwrappedPositions[rootB] = projectedPositions[rootB];
        unwrappedPositions[rootC] = projectedPositions[rootC];

        HashSet<int> resolvedTriangles = new HashSet<int> { selectedTriangles[0] };

        bool madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;

            for (int i = 0; i < selectedTriangles.Count; i++)
            {
                int triangleIndex = selectedTriangles[i];
                if (resolvedTriangles.Contains(triangleIndex))
                {
                    continue;
                }

                GetTriangleVertexIndices(sourceTriangles, triangleIndex, out int a, out int b, out int c);

                bool hasA = unwrappedPositions.ContainsKey(a);
                bool hasB = unwrappedPositions.ContainsKey(b);
                bool hasC = unwrappedPositions.ContainsKey(c);

                int placedCount = (hasA ? 1 : 0) + (hasB ? 1 : 0) + (hasC ? 1 : 0);

                if (placedCount == 3)
                {
                    resolvedTriangles.Add(triangleIndex);
                    madeProgress = true;
                    continue;
                }

                if (placedCount != 2)
                {
                    continue;
                }

                int sharedA;
                int sharedB;
                int missing;

                if (!hasA)
                {
                    sharedA = b;
                    sharedB = c;
                    missing = a;
                }
                else if (!hasB)
                {
                    sharedA = c;
                    sharedB = a;
                    missing = b;
                }
                else
                {
                    sharedA = a;
                    sharedB = b;
                    missing = c;
                }

                if (!TryPlaceTriangleVertex(
                    sharedA,
                    sharedB,
                    missing,
                    sourceVertices,
                    sourceTransform,
                    unwrappedPositions,
                    projectedPositions,
                    out Vector2 placedPosition))
                {
                    continue;
                }

                unwrappedPositions[missing] = placedPosition;
                resolvedTriangles.Add(triangleIndex);
                madeProgress = true;
            }
        }

        foreach (KeyValuePair<int, Vector2> pair in projectedPositions)
        {
            if (!unwrappedPositions.ContainsKey(pair.Key))
            {
                unwrappedPositions.Add(pair.Key, pair.Value);
            }
        }

        return unwrappedPositions;
    }

    private static bool TryPlaceTriangleVertex(
        int sharedA,
        int sharedB,
        int missing,
        Vector3[] sourceVertices,
        Transform sourceTransform,
        Dictionary<int, Vector2> unwrappedPositions,
        Dictionary<int, Vector2> projectedPositions,
        out Vector2 placedPosition)
    {
        placedPosition = Vector2.zero;

        if (!unwrappedPositions.TryGetValue(sharedA, out Vector2 pointA) ||
            !unwrappedPositions.TryGetValue(sharedB, out Vector2 pointB))
        {
            return false;
        }

        Vector3 worldA = sourceTransform.TransformPoint(sourceVertices[sharedA]);
        Vector3 worldB = sourceTransform.TransformPoint(sourceVertices[sharedB]);
        Vector3 worldMissing = sourceTransform.TransformPoint(sourceVertices[missing]);

        float lengthAM = Vector3.Distance(worldA, worldMissing);
        float lengthBM = Vector3.Distance(worldB, worldMissing);

        Vector2 edge = pointB - pointA;
        float edgeLength = edge.magnitude;

        if (edgeLength <= 0.0001f)
        {
            return false;
        }

        float minReach = Mathf.Abs(lengthAM - lengthBM);
        float maxReach = lengthAM + lengthBM;

        if (edgeLength < minReach || edgeLength > maxReach)
        {
            return false;
        }

        Vector2 edgeDir = edge / edgeLength;
        float alongEdge = ((lengthAM * lengthAM) - (lengthBM * lengthBM) + (edgeLength * edgeLength)) / (2f * edgeLength);
        float heightSqr = Mathf.Max(0f, (lengthAM * lengthAM) - (alongEdge * alongEdge));
        float height = Mathf.Sqrt(heightSqr);

        Vector2 edgePerp = new Vector2(-edgeDir.y, edgeDir.x);
        Vector2 basePoint = pointA + (edgeDir * alongEdge);

        Vector2 candidateA = basePoint + (edgePerp * height);
        Vector2 candidateB = basePoint - (edgePerp * height);

        Vector2 projectedGuess = projectedPositions[missing];
        placedPosition = (candidateA - projectedGuess).sqrMagnitude <= (candidateB - projectedGuess).sqrMagnitude
            ? candidateA
            : candidateB;

        return true;
    }

    private static void GetTriangleVertexIndices(int[] sourceTriangles, int triangleIndex, out int a, out int b, out int c)
    {
        int start = triangleIndex * 3;
        a = sourceTriangles[start];
        b = sourceTriangles[start + 1];
        c = sourceTriangles[start + 2];
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

                if (IsFloodEdgeBlocked(triangles, vertices, triangle, neighbor))
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

    private static void AddBoundaryEdge(Dictionary<Edge, int> edgeUseCounts, int a, int b)
    {
        Edge edge = new Edge(a, b);

        if (edgeUseCounts.TryGetValue(edge, out int count))
        {
            edgeUseCounts[edge] = count + 1;
        }
        else
        {
            edgeUseCounts.Add(edge, 1);
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

    private static bool TryGetClosestPointsBetweenPatches(
        MeshFilter sourcePatchFilter,
        MeshFilter targetPatchFilter,
        out Vector3 sourceWorldPoint,
        out Vector3 targetWorldPoint,
        out float closestDistanceSqr)
    {
        sourceWorldPoint = Vector3.zero;
        targetWorldPoint = Vector3.zero;
        closestDistanceSqr = float.MaxValue;

        List<BoundarySegment> sourceSegments = GetBoundarySegments(sourcePatchFilter);
        List<BoundarySegment> targetSegments = GetBoundarySegments(targetPatchFilter);

        if (sourceSegments.Count == 0 || targetSegments.Count == 0)
            return false;

        bool found = false;

        for (int i = 0; i < sourceSegments.Count; i++)
        {
            BoundarySegment sourceSegment = sourceSegments[i];

            for (int j = 0; j < targetSegments.Count; j++)
            {
                BoundarySegment targetSegment = targetSegments[j];

                GetClosestPointsOnSegments(
                    sourceSegment.A, sourceSegment.B,
                    targetSegment.A, targetSegment.B,
                    out Vector3 sourcePoint,
                    out Vector3 targetPoint);

                float distanceSqr = (sourcePoint - targetPoint).sqrMagnitude;
                if (distanceSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distanceSqr;
                sourceWorldPoint = sourcePoint;
                targetWorldPoint = targetPoint;
                found = true;
            }
        }

        if (!found)
            return false;

        // When patches touch at a seam the two closest points are at the same
        // world location, which produces a zero-length link. Push each endpoint
        // slightly toward its own mesh centroid so both points land on their
        // respective navmesh surfaces with a real gap between them.
        const float insetDistance = 0.08f;

        Vector3 sourceCentroid = sourcePatchFilter.transform.TransformPoint(
            sourcePatchFilter.sharedMesh.bounds.center);
        Vector3 targetCentroid = targetPatchFilter.transform.TransformPoint(
            targetPatchFilter.sharedMesh.bounds.center);

        Vector3 sourceDir = sourceCentroid - sourceWorldPoint;
        float sourceLen = sourceDir.magnitude;
        if (sourceLen > 0.001f)
            sourceWorldPoint += (sourceDir / sourceLen) * Mathf.Min(insetDistance, sourceLen * 0.4f);

        Vector3 targetDir = targetCentroid - targetWorldPoint;
        float targetLen = targetDir.magnitude;
        if (targetLen > 0.001f)
            targetWorldPoint += (targetDir / targetLen) * Mathf.Min(insetDistance, targetLen * 0.4f);

        return true;
    }

    // Returns the closest boundary segment pair between two patches, ranked by
    // actual closest point on segment to segment (handles mismatched edge sizes).
    private static bool TryGetSeamBetweenPatches(
        MeshFilter sourcePatchFilter,
        MeshFilter targetPatchFilter,
        out BoundarySegment sourceSeam,
        out BoundarySegment targetSeam,
        out Vector3 sourceContact,
        out Vector3 targetContact,
        out float closestDistanceSqr)
    {
        sourceSeam = default;
        targetSeam = default;
        sourceContact = Vector3.zero;
        targetContact = Vector3.zero;
        closestDistanceSqr = float.MaxValue;

        List<BoundarySegment> sourceSegments = GetBoundarySegments(sourcePatchFilter);
        List<BoundarySegment> targetSegments = GetBoundarySegments(targetPatchFilter);

        if (sourceSegments.Count == 0 || targetSegments.Count == 0)
            return false;

        bool found = false;

        for (int i = 0; i < sourceSegments.Count; i++)
        {
            BoundarySegment src = sourceSegments[i];

            for (int j = 0; j < targetSegments.Count; j++)
            {
                BoundarySegment tgt = targetSegments[j];

                GetClosestPointsOnSegments(src.A, src.B, tgt.A, tgt.B,
                    out Vector3 srcPt, out Vector3 tgtPt);

                float distSqr = (srcPt - tgtPt).sqrMagnitude;

                if (distSqr >= closestDistanceSqr)
                    continue;

                closestDistanceSqr = distSqr;
                sourceSeam = src;
                targetSeam = tgt;
                sourceContact = srcPt;
                targetContact = tgtPt;
                found = true;
            }
        }

        return found;
    }

    // Compute the overlap width between two seam segments along their shared axis.
    // Only meaningful when the edges are roughly parallel; returns 0 otherwise.
    private static float GetSeamOverlapWidth(BoundarySegment src, BoundarySegment tgt)
    {
        Vector3 srcDir = src.B - src.A;
        float srcLen = srcDir.magnitude;
        if (srcLen < 0.0001f) return 0f;
        Vector3 srcAxis = srcDir / srcLen;

        Vector3 tgtDir = tgt.B - tgt.A;
        float tgtLen = tgtDir.magnitude;
        if (tgtLen < 0.0001f) return 0f;

        // Only compute overlap when edges are roughly parallel.
        if (Mathf.Abs(Vector3.Dot(srcAxis, tgtDir / tgtLen)) < 0.85f)
            return 0f;

        float tA = Vector3.Dot(tgt.A - src.A, srcAxis);
        float tB = Vector3.Dot(tgt.B - src.A, srcAxis);
        if (tA > tB) { float tmp = tA; tA = tB; tB = tmp; }

        float overlapMin = Mathf.Max(0f, tA);
        float overlapMax = Mathf.Min(srcLen, tB);
        return Mathf.Max(0f, overlapMax - overlapMin);
    }

    private static List<BoundarySegment> GetBoundarySegments(MeshFilter meshFilter)
    {
        List<BoundarySegment> segments = new List<BoundarySegment>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
            return segments;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        if (vertices == null || triangles == null || triangles.Length < 3)
            return segments;

        Dictionary<Edge, int> edgeUseCounts = new Dictionary<Edge, int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            AddBoundaryEdge(edgeUseCounts, triangles[i], triangles[i + 1]);
            AddBoundaryEdge(edgeUseCounts, triangles[i + 1], triangles[i + 2]);
            AddBoundaryEdge(edgeUseCounts, triangles[i + 2], triangles[i]);
        }

        foreach (KeyValuePair<Edge, int> pair in edgeUseCounts)
        {
            if (pair.Value != 1)
                continue;

            Vector3 a = meshFilter.transform.TransformPoint(vertices[pair.Key.A]);
            Vector3 b = meshFilter.transform.TransformPoint(vertices[pair.Key.B]);
            segments.Add(new BoundarySegment(a, b));
        }

        return segments;
    }

    private static void GetClosestPointsOnSegments(
        Vector3 p1,
        Vector3 q1,
        Vector3 p2,
        Vector3 q2,
        out Vector3 c1,
        out Vector3 c2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;
        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);

        float s;
        float t;

        if (a <= Mathf.Epsilon && e <= Mathf.Epsilon)
        {
            c1 = p1;
            c2 = p2;
            return;
        }

        if (a <= Mathf.Epsilon)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);

            if (e <= Mathf.Epsilon)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;

                s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;

                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }

    private readonly struct BoundarySegment
    {
        public readonly Vector3 A;
        public readonly Vector3 B;

        public BoundarySegment(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }
    }

    private void RebuildAllPatchLinks()
    {
        GameObject linkContainer = GameObject.Find("NavMeshPatchLinks");
        if (linkContainer != null)
        {
            NavMeshLink[] old = linkContainer.GetComponentsInChildren<NavMeshLink>(true);
            for (int i = old.Length - 1; i >= 0; i--)
            {
                if (old[i] != null)
                    Undo.DestroyObjectImmediate(old[i].gameObject);
            }
        }

        linkContainer = GetOrCreateLinkContainer();
        float maxDistanceSqr = maxAutoLinkDistance * maxAutoLinkDistance;

        NavMeshSurface[] allSurfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);

        for (int i = 0; i < allSurfaces.Length; i++)
        {
            NavMeshSurface sourceSurface = allSurfaces[i];
            if (sourceSurface == null) continue;

            MeshFilter sourceFilter = sourceSurface.GetComponentInChildren<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

            for (int j = i + 1; j < allSurfaces.Length; j++)
            {
                NavMeshSurface targetSurface = allSurfaces[j];
                if (targetSurface == null) continue;

                if (sourceSurface.agentTypeID != targetSurface.agentTypeID)
                    continue;

                MeshFilter targetFilter = targetSurface.GetComponentInChildren<MeshFilter>();
                if (targetFilter == null || targetFilter.sharedMesh == null) continue;

                if (!TryGetSeamBetweenPatches(
                    sourceFilter, targetFilter,
                    out BoundarySegment sourceSeam,
                    out BoundarySegment targetSeam,
                    out Vector3 sourceContact,
                    out Vector3 targetContact,
                    out float closestDistanceSqr))
                    continue;

                Vector3 sourcePairNormal = GetMeshAverageNormal(sourceFilter);
                Vector3 targetPairNormal = GetMeshAverageNormal(targetFilter);
                bool sourcePairIsWall = Mathf.Abs(Vector3.Dot(sourcePairNormal, Vector3.up)) < 0.5f;
                bool targetPairIsWall = Mathf.Abs(Vector3.Dot(targetPairNormal, Vector3.up)) < 0.5f;
                bool isWallToFloorPair = sourcePairIsWall ^ targetPairIsWall;

                if (closestDistanceSqr > maxDistanceSqr && !isWallToFloorPair)
                    continue;

                CreatePatchLink(linkContainer, sourceFilter, targetFilter,
                    sourceSeam, targetSeam, sourceContact, targetContact,
                    sourceSurface.agentTypeID);
            }
        }

        Debug.Log($"[NavMeshFacePatchTool] Rebuilt patch links. Container: {linkContainer.name}");
    }

    private static GameObject GetOrCreateLinkContainer()
    {
        const string linkContainerName = "NavMeshPatchLinks";
        GameObject existing = GameObject.Find(linkContainerName);

        if (existing != null)
            return existing;

        GameObject container = new GameObject(linkContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Create NavMesh Patch Link Container");
        return container;
    }

    private void CreatePatchLink(
        GameObject linkParent,
        MeshFilter sourceFilter,
        MeshFilter targetFilter,
        BoundarySegment sourceSeam,
        BoundarySegment targetSeam,
        Vector3 sourceContact,
        Vector3 targetContact,
        int agentTypeId)
    {
        Vector3 sourceMid = sourceContact;
        Vector3 targetMid = targetContact;

        const float inset = 0.35f;

        Vector3 srcCentroid = sourceFilter.transform.TransformPoint(sourceFilter.sharedMesh.bounds.center);
        Vector3 tgtCentroid = targetFilter.transform.TransformPoint(targetFilter.sharedMesh.bounds.center);

        Vector3 srcInsetDir = srcCentroid - sourceMid;
        if (srcInsetDir.magnitude > 0.001f)
            sourceMid += srcInsetDir.normalized * Mathf.Min(inset, srcInsetDir.magnitude * 0.4f);

        Vector3 tgtInsetDir = tgtCentroid - targetMid;
        if (tgtInsetDir.magnitude > 0.001f)
            targetMid += tgtInsetDir.normalized * Mathf.Min(inset, tgtInsetDir.magnitude * 0.4f);

        float initialSnapRadius = Mathf.Max(maxAutoLinkDistance, 1.0f);
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(sourceMid, out NavMeshHit srcHit, initialSnapRadius, filter))
        {
            Debug.LogWarning($"[NavMeshFacePatchTool] Could not snap link start to navmesh near {sourceMid}. Link skipped.");
            return;
        }
        sourceMid = srcHit.position;

        if (!NavMesh.SamplePosition(targetMid, out NavMeshHit tgtHit, initialSnapRadius, filter))
        {
            Debug.LogWarning($"[NavMeshFacePatchTool] Could not snap link end to navmesh near {targetMid}. Link skipped.");
            return;
        }
        targetMid = tgtHit.position;

        Vector3 linkDir = targetMid - sourceMid;
        if (linkDir.sqrMagnitude < 0.000001f)
            return;
        linkDir = linkDir.normalized;

        // Width: horizontal span of the shared seam only
        Vector3 seamA_flat = new Vector3(sourceSeam.A.x, 0f, sourceSeam.A.z);
        Vector3 seamB_flat = new Vector3(sourceSeam.B.x, 0f, sourceSeam.B.z);
        float seamWidth = Mathf.Max(Vector3.Distance(seamA_flat, seamB_flat), autoLinkWidth);

        // Classify surfaces.
        Vector3 sourceNormal = GetMeshAverageNormal(sourceFilter);
        Vector3 targetNormal = GetMeshAverageNormal(targetFilter);
        bool sourceIsWall = Mathf.Abs(Vector3.Dot(sourceNormal, Vector3.up)) < 0.5f;
        bool targetIsWall = Mathf.Abs(Vector3.Dot(targetNormal, Vector3.up)) < 0.5f;

        Quaternion linkRotation;
        Vector3 linkCenter;

        if (sourceIsWall && targetIsWall)
        {
            float srcWorldMinY = sourceFilter.transform.TransformPoint(sourceFilter.sharedMesh.bounds.min).y;
            float srcWorldMaxY = sourceFilter.transform.TransformPoint(sourceFilter.sharedMesh.bounds.max).y;
            float tgtWorldMinY = targetFilter.transform.TransformPoint(targetFilter.sharedMesh.bounds.min).y;
            float tgtWorldMaxY = targetFilter.transform.TransformPoint(targetFilter.sharedMesh.bounds.max).y;
            float sharedMinY = Mathf.Min(srcWorldMinY, tgtWorldMinY);
            float sharedMaxY = Mathf.Max(srcWorldMaxY, tgtWorldMaxY);
            float sharedMidY = (sharedMinY + sharedMaxY) * 0.5f;

            seamWidth = sharedMaxY - sharedMinY;

            float wallSnapRadius = (sharedMaxY - sharedMinY) * 0.5f + 1.0f;

            Vector3 srcSearchOrigin = new Vector3(sourceMid.x, sharedMidY, sourceMid.z);
            if (NavMesh.SamplePosition(srcSearchOrigin, out NavMeshHit srcWallHit, wallSnapRadius, filter))
                sourceMid = srcWallHit.position;
            else
                sourceMid = srcSearchOrigin;

            Vector3 tgtSearchOrigin = new Vector3(targetMid.x, sharedMidY, targetMid.z);
            if (NavMesh.SamplePosition(tgtSearchOrigin, out NavMeshHit tgtWallHit, wallSnapRadius, filter))
                targetMid = tgtWallHit.position;
            else
                targetMid = tgtSearchOrigin;

            Vector3 flatDir = new Vector3(targetMid.x - sourceMid.x, 0f, targetMid.z - sourceMid.z);
            if (flatDir.sqrMagnitude < 0.0001f)
                flatDir = Vector3.forward;
            flatDir = flatDir.normalized;

            linkRotation = Quaternion.LookRotation(flatDir, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            linkCenter = (sourceMid + targetMid) * 0.5f;
        }
        else if (sourceIsWall || targetIsWall)
        {
            MeshFilter wallFilter = sourceIsWall ? sourceFilter : targetFilter;
            BoundarySegment wallSeam = sourceIsWall ? sourceSeam : targetSeam;

            float wallWorldY1 = wallFilter.transform.TransformPoint(wallFilter.sharedMesh.bounds.min).y;
            float wallWorldY2 = wallFilter.transform.TransformPoint(wallFilter.sharedMesh.bounds.max).y;
            float wallMinY = Mathf.Min(wallWorldY1, wallWorldY2);
            float wallMaxY = Mathf.Max(wallWorldY1, wallWorldY2);
            float wallHeight = wallMaxY - wallMinY;

            // Width: horizontal span of the wall seam only
            seamWidth = Mathf.Max(
                Vector3.Distance(
                    new Vector3(wallSeam.A.x, 0f, wallSeam.A.z),
                    new Vector3(wallSeam.B.x, 0f, wallSeam.B.z)),
                autoLinkWidth);

            float wallFloorSnapRadius = wallHeight * 0.5f + 1.0f;

            Vector3 wallSeamMid = (wallSeam.A + wallSeam.B) * 0.5f;
            wallSeamMid.y = wallMinY;

            Vector3 wallCentroid = wallFilter.transform.TransformPoint(wallFilter.sharedMesh.bounds.center);
            Vector3 wallInsetDir = wallCentroid - wallSeamMid;
            const float wallSurfaceInset = 0.12f;

            Vector3 wallPoint = wallSeamMid;
            if (wallInsetDir.magnitude > 0.001f)
                wallPoint += wallInsetDir.normalized * Mathf.Min(wallSurfaceInset, wallInsetDir.magnitude * 0.25f);

            Vector3 wallOutward = sourceIsWall ? sourceNormal : targetNormal;
            wallOutward = new Vector3(wallOutward.x, 0f, wallOutward.z);
            if (wallOutward.sqrMagnitude < 0.0001f)
                wallOutward = Vector3.forward;
            wallOutward = wallOutward.normalized;

            const float floorSurfaceOffset = 0.2f;
            Vector3 floorSearchOrigin = wallPoint + wallOutward * floorSurfaceOffset;
            floorSearchOrigin.y = wallMinY;

            if (NavMesh.SamplePosition(floorSearchOrigin, out NavMeshHit floorHit, wallFloorSnapRadius, filter))
            {
                if (sourceIsWall)
                {
                    sourceMid = wallPoint;
                    targetMid = floorHit.position;
                }
                else
                {
                    sourceMid = floorHit.position;
                    targetMid = wallPoint;
                }
            }
            else
            {
                return;
            }

            Vector3 wallToFloorDir = sourceIsWall ? targetMid - sourceMid : sourceMid - targetMid;
            Vector3 flatDir = new Vector3(wallToFloorDir.x, 0f, wallToFloorDir.z);
            if (flatDir.sqrMagnitude < 0.0001f)
                flatDir = wallOutward;
            flatDir = flatDir.normalized;

            linkRotation = Quaternion.LookRotation(flatDir, Vector3.up) * Quaternion.Euler(0f, -90f, 45f);
            // Center: XZ from the seam midpoint (actual junction), Y at floor level
            linkCenter = new Vector3(wallSeamMid.x, wallMinY, wallSeamMid.z);
        }
        else
        {
            // Floor-to-floor: face along linkDir, world up.
            Vector3 upHint = Mathf.Abs(Vector3.Dot(linkDir, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            linkRotation = Quaternion.LookRotation(linkDir, upHint);
            linkCenter = (sourceMid + targetMid) * 0.5f;
        }

        GameObject linkObject = new GameObject("AutoNavMeshLink");
        Undo.RegisterCreatedObjectUndo(linkObject, "Create NavMesh Patch Link");
        linkObject.transform.SetParent(linkParent.transform, true);
        linkObject.transform.position = linkCenter;
        linkObject.transform.rotation = linkRotation;
        linkObject.transform.localScale = Vector3.one;

        GameObject startObj = new GameObject("LinkStart");
        Undo.RegisterCreatedObjectUndo(startObj, "Create NavMesh Link Start");
        startObj.transform.SetParent(linkObject.transform, true);
        startObj.transform.position = sourceMid;

        GameObject endObj = new GameObject("LinkEnd");
        Undo.RegisterCreatedObjectUndo(endObj, "Create NavMesh Link End");
        endObj.transform.SetParent(linkObject.transform, true);
        endObj.transform.position = targetMid;

        NavMeshLink link = Undo.AddComponent<NavMeshLink>(linkObject);
        link.startTransform = startObj.transform;
        link.endTransform = endObj.transform;
        link.width = seamWidth;
        link.bidirectional = true;
        link.autoUpdate = true;
        link.area = 0;
        link.costModifier = -1f;
        link.agentTypeID = agentTypeId;
    }

    private static Vector3 GetMeshAverageNormal(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] normals = mesh.normals;

        if (normals == null || normals.Length == 0)
            return meshFilter.transform.up;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < normals.Length; i++)
            sum += normals[i];

        return meshFilter.transform.TransformDirection(sum / normals.Length).normalized;
    }

    private bool IsSurfaceFromCurrentSource(NavMeshSurface surface)
    {
        if (surface == null || sourceObject == null)
            return false;

        return surface.transform.parent == sourceObject.transform;
    }

    private bool IsTriangleCenterBlocked(Vector3 localPoint, Vector3 normal)
    {
        Vector3 worldPoint = sourceObject.transform.TransformPoint(localPoint);
        Vector3 worldNormal = sourceObject.transform.TransformDirection(normal).normalized;

        const float surfaceOffset = 0.02f;
        const float checkDistance = 0.08f;

        Vector3 castStart = worldPoint + worldNormal * surfaceOffset;
        Vector3 castDirection = -worldNormal;

        if (Physics.Raycast(castStart, castDirection, out RaycastHit hit, checkDistance))
            return hit.collider != null && hit.collider.gameObject != sourceObject;

        return false;
    }

    private bool IsFloodEdgeBlocked(
        int[] triangles,
        Vector3[] vertices,
        int fromTriangle,
        int toTriangle)
    {
        int fromStart = fromTriangle * 3;
        int toStart = toTriangle * 3;

        int sharedA = -1;
        int sharedB = -1;

        for (int i = 0; i < 3; i++)
        {
            int fromIndex = triangles[fromStart + i];
            for (int j = 0; j < 3; j++)
            {
                int toIndex = triangles[toStart + j];
                if (fromIndex != toIndex)
                    continue;

                if (sharedA == -1)
                    sharedA = fromIndex;
                else if (sharedB == -1)
                    sharedB = fromIndex;
            }
        }

        if (sharedA == -1 || sharedB == -1)
            return false;

        Vector3 worldA = sourceObject.transform.TransformPoint(vertices[sharedA]);
        Vector3 worldB = sourceObject.transform.TransformPoint(vertices[sharedB]);
        Vector3 edgeMid = (worldA + worldB) * 0.5f;

        Vector3 fromCenter = sourceObject.transform.TransformPoint(GetTriangleCenter(triangles, vertices, fromTriangle));
        Vector3 toCenter = sourceObject.transform.TransformPoint(GetTriangleCenter(triangles, vertices, toTriangle));
        Vector3 across = toCenter - fromCenter;
        if (across.sqrMagnitude < 0.0001f)
            return false;

        across.Normalize();

        const float sideOffset = 0.03f;
        const float castHeight = 0.2f;
        float castDistance = Vector3.Distance(fromCenter, toCenter);

        Vector3 castStart = edgeMid - across * sideOffset + Vector3.up * castHeight;
        Quaternion castRotation = Quaternion.LookRotation(across, Vector3.up);
        Vector3 halfExtents = new Vector3(0.02f, castHeight, 0.02f);

        if (Physics.BoxCast(castStart, halfExtents, across, out RaycastHit hit, castRotation, castDistance))
            return hit.collider != null && hit.collider.gameObject != sourceObject;

        return false;
    }

    private List<int> FilterTrianglesInsideBlockingGeometry(
        List<int> selectedTriangles,
        int[] triangles,
        Vector3[] vertices,
        Vector3[] faceNormals)
    {
        List<int> filtered = new List<int>(selectedTriangles.Count);
        int triangleCount = triangles.Length / 3;

        for (int i = 0; i < selectedTriangles.Count; i++)
        {
            int triangleIndex = selectedTriangles[i];
            if (triangleIndex < 0 || triangleIndex >= triangleCount)
                continue;

            if (!IsTriangleInsideBlockingGeometry(triangleIndex, triangles, vertices, faceNormals))
                filtered.Add(triangleIndex);
        }

        return filtered;
    }

    private bool IsTriangleInsideBlockingGeometry(
        int triangleIndex,
        int[] triangles,
        Vector3[] vertices,
        Vector3[] faceNormals)
    {
        int triangleCount = triangles.Length / 3;
        if (triangleIndex < 0 || triangleIndex >= triangleCount)
            return true;

        int start = triangleIndex * 3;
        if (start < 0 || start + 2 >= triangles.Length)
            return true;

        int indexA = triangles[start];
        int indexB = triangles[start + 1];
        int indexC = triangles[start + 2];

        if (indexA < 0 || indexA >= vertices.Length ||
            indexB < 0 || indexB >= vertices.Length ||
            indexC < 0 || indexC >= vertices.Length ||
            faceNormals == null || triangleIndex >= faceNormals.Length)
            return true;

        Vector3 a = sourceObject.transform.TransformPoint(vertices[indexA]);
        Vector3 b = sourceObject.transform.TransformPoint(vertices[indexB]);
        Vector3 c = sourceObject.transform.TransformPoint(vertices[indexC]);

        Vector3 center = (a + b + c) / 3f;
        Vector3 normal = sourceObject.transform.TransformDirection(faceNormals[triangleIndex]).normalized;
        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;

        const float sampleInset = 0.02f;
        const float halfHeight = 0.1f;
        Vector3 halfExtents = new Vector3(sampleInset, halfHeight, sampleInset);

        Vector3[] samples =
        {
            center,
            Vector3.Lerp(a, center, 0.5f),
            Vector3.Lerp(b, center, 0.5f),
            Vector3.Lerp(c, center, 0.5f)
        };

        for (int i = 0; i < samples.Length; i++)
        {
            Collider[] overlaps = Physics.OverlapBox(
                samples[i],
                halfExtents,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int j = 0; j < overlaps.Length; j++)
            {
                Collider overlap = overlaps[j];
                if (overlap == null || overlap.gameObject == sourceObject)
                    continue;

                Vector3 closest = overlap.ClosestPoint(samples[i]);
                if ((closest - samples[i]).sqrMagnitude < 0.0001f)
                    return true;
            }
        }

        return false;
    }
}
#endif