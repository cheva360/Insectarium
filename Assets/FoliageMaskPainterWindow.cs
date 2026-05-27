using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class FoliageMaskPainterWindow : EditorWindow
{
    private const string UseFoliagePropertyName = "_UseFoliage";
    private const string TriplanarSharpnessPropertyName = "_TriplanarSharpness";
    private const string BoundsMinPropertyName = "_FoliageMaskBoundsMin";
    private const string BoundsSizePropertyName = "_FoliageMaskBoundsSize";

    private static readonly string[] MaskPropertyNames =
    {
        "_FoliageMaskPosX",
        "_FoliageMaskNegX",
        "_FoliageMaskPosY",
        "_FoliageMaskNegY",
        "_FoliageMaskPosZ",
        "_FoliageMaskNegZ"
    };

    private static readonly string[] MaskDisplayNames =
    {
        "Mask +X",
        "Mask -X",
        "Mask +Y",
        "Mask -Y",
        "Mask +Z",
        "Mask -Z"
    };

    private static readonly string[] MaskAssetSuffixes =
    {
        "PosX",
        "NegX",
        "PosY",
        "NegY",
        "PosZ",
        "NegZ"
    };

    private enum BrushMode
    {
        Add,
        Erase
    }

    private enum MaskResolution
    {
        R8 = 8,
        R16 = 16,
        R32 = 32,
        R64 = 64,
        R128 = 128,
        R256 = 256,
        R512 = 512,
        R1024 = 1024,
        R2048 = 2048,
        R4096 = 4096
    }

    private Renderer targetRenderer;
    private int materialIndex;
    private BrushMode brushMode = BrushMode.Add;
    private bool paintingEnabled = true;
    private bool autoSaveOnStrokeEnd;

    private float brushSizePixels = 32f;
    private float brushStrength = 1f;
    private float brushSoftness = 0.5f;
    private MaskResolution maskResolution = MaskResolution.R128;

    private readonly Texture2D[] sourceMaskTextures = new Texture2D[6];
    private readonly Texture2D[] workingTextures = new Texture2D[6];
    private readonly Color32[][] workingPixels = new Color32[6][];
    private readonly string[] sourceMaskPaths = new string[6];

    private bool strokeDirty;
    private bool isPainting;

    private Vector3 lastPaintLocalPoint;
    private bool hasLastPaintPoint;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Foliage Mask Painter")]
    public static void OpenWindow()
    {
        GetWindow<FoliageMaskPainterWindow>("Foliage Mask Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryAssignFromSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (strokeDirty)
        {
            SaveWorkingTexturesIfNeeded();
        }
        else
        {
            CancelStrokePreview();
        }
    }

    private void OnSelectionChange()
    {
        TryAssignFromSelection();
        Repaint();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        targetRenderer = (Renderer)EditorGUILayout.ObjectField("Renderer", targetRenderer, typeof(Renderer), true);

        if (GUILayout.Button("Use Selected Object"))
        {
            TryAssignFromSelection();
        }

        if (targetRenderer == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a Renderer.", MessageType.Info);
            return;
        }

        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            EditorGUILayout.HelpBox("The selected renderer has no materials.", MessageType.Warning);
            return;
        }

        materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
        materialIndex = EditorGUILayout.Popup("Material", materialIndex, GetMaterialNames(materials));

        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null)
        {
            EditorGUILayout.HelpBox("No valid material selected.", MessageType.Warning);
            return;
        }

        if (!SupportsTriplanarMasks(targetMaterial))
        {
            EditorGUILayout.HelpBox("The selected material does not support triplanar foliage masks.", MessageType.Error);
            return;
        }

        RefreshMaskReferences(targetMaterial);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mask Assets", EditorStyles.boldLabel);

        maskResolution = (MaskResolution)EditorGUILayout.EnumPopup("Mask Resolution", maskResolution);

        if (GUILayout.Button("Prepare Unique Material + Masks For This Renderer"))
        {
            SaveWorkingTexturesIfNeeded();
            PrepareUniqueMaterialAndMasks();
            targetMaterial = GetTargetMaterial();
            RefreshMaskReferences(targetMaterial);
        }

        if (!HasAllMasks())
        {
            EditorGUILayout.HelpBox("One or more foliage masks are missing.", MessageType.Warning);

            if (GUILayout.Button("Create And Assign Missing Masks"))
            {
                SaveWorkingTexturesIfNeeded();
                CreateAndAssignMissingMasks(targetMaterial);
                RefreshMaskReferences(targetMaterial);
            }
        }

        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < MaskPropertyNames.Length; i++)
            {
                EditorGUILayout.ObjectField(MaskDisplayNames[i], sourceMaskTextures[i], typeof(Texture2D), false);
            }
        }

        using (new EditorGUI.DisabledScope(!HasAllMasks()))
        {
            if (GUILayout.Button("Resize Masks To Resolution"))
            {
                SaveWorkingTexturesIfNeeded();
                ResizeMasksToResolution(targetMaterial);
                RefreshMaskReferences(targetMaterial);
            }

            if (GUILayout.Button("Erase All Masks"))
            {
                if (EditorUtility.DisplayDialog("Erase All Foliage Masks", "This will clear all foliage paint for the selected renderer material.", "Erase All", "Cancel"))
                {
                    SaveWorkingTexturesIfNeeded();
                    EraseAllMasks(targetMaterial);
                    RefreshMaskReferences(targetMaterial);
                }
            }
        }

        if (!(GetTargetCollider() is MeshCollider))
        {
            EditorGUILayout.HelpBox("A MeshCollider is required for Scene painting.", MessageType.Warning);

            if (GUILayout.Button("Add / Replace With MeshCollider"))
            {
                EnsureMeshCollider();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        paintingEnabled = EditorGUILayout.Toggle("Painting Enabled", paintingEnabled);
        autoSaveOnStrokeEnd = EditorGUILayout.Toggle("Auto Save On Stroke End", autoSaveOnStrokeEnd);
        brushMode = (BrushMode)EditorGUILayout.EnumPopup("Mode", brushMode);
        brushSizePixels = EditorGUILayout.Slider("Size (pixels)", brushSizePixels, 0.01f, 20f);
        brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0.01f, 1f);
        brushSoftness = EditorGUILayout.Slider("Softness", brushSoftness, 0f, 1f);

        if (strokeDirty)
        {
            EditorGUILayout.HelpBox("There are unsaved mask changes.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!HasLiveWorkingTextures() && !strokeDirty))
        {
            if (GUILayout.Button("Save Masks Now"))
            {
                SaveWorkingTexturesIfNeeded();
                RefreshMaskReferences(targetMaterial);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Scene controls:\n" +
            "- Left Mouse: paint\n" +
            "- Shift + Left Mouse: erase\n" +
            "- Lower mask resolution gives a chunkier, vertex-paint-like look\n" +
            "- Disable auto save to avoid the lag spike on mouse release\n" +
            "- Use the unique material button so painting only affects this renderer",
            MessageType.None);

        if (targetMaterial.HasProperty(UseFoliagePropertyName) && targetMaterial.GetFloat(UseFoliagePropertyName) < 0.5f)
        {
            if (GUILayout.Button("Enable Foliage On Material"))
            {
                Undo.RecordObject(targetMaterial, "Enable Foliage");
                targetMaterial.SetFloat(UseFoliagePropertyName, 1f);
                EditorUtility.SetDirty(targetMaterial);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!paintingEnabled || targetRenderer == null)
        {
            return;
        }

        Material targetMaterial = GetTargetMaterial();
        MeshCollider meshCollider = GetTargetCollider() as MeshCollider;
        if (targetMaterial == null || meshCollider == null || !SupportsTriplanarMasks(targetMaterial))
        {
            return;
        }

        RefreshMaskReferences(targetMaterial);
        if (!HasAllMasks())
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.alt)
        {
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        bool hitTarget = meshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity);

        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (hitTarget)
        {
            Handles.color = GetBrushPreviewColor(IsErase(currentEvent));
            Handles.DrawWireDisc(hit.point, hit.normal, GetWorldBrushSize());
            HandleUtility.AddDefaultControl(controlId);
            SceneView.RepaintAll();
        }

        switch (currentEvent.type)
        {
            case EventType.MouseDown:
                if (hitTarget && currentEvent.button == 0)
                {
                    GUIUtility.hotControl = controlId;
                    BeginStroke(targetMaterial);
                    PaintAtHit(hit, IsErase(currentEvent), targetMaterial);
                    lastPaintLocalPoint = targetRenderer.transform.InverseTransformPoint(hit.point);
                    hasLastPaintPoint = true;
                    currentEvent.Use();
                }
                break;

            case EventType.MouseDrag:
                if (isPainting && GUIUtility.hotControl == controlId && currentEvent.button == 0)
                {
                    if (hitTarget)
                    {
                        PaintInterpolatedStroke(hit, IsErase(currentEvent), targetMaterial);
                    }

                    currentEvent.Use();
                }
                break;

            case EventType.MouseUp:
                if (isPainting && GUIUtility.hotControl == controlId && currentEvent.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    isPainting = false;

                    if (autoSaveOnStrokeEnd)
                    {
                        SaveWorkingTexturesIfNeeded();
                    }

                    hasLastPaintPoint = false;
                    currentEvent.Use();
                }
                break;
        }
    }

    private void TryAssignFromSelection()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            return;
        }

        Renderer selectedRenderer = selected.GetComponent<Renderer>();
        if (selectedRenderer == null)
        {
            selectedRenderer = selected.GetComponentInChildren<Renderer>();
        }

        if (selectedRenderer != null)
        {
            targetRenderer = selectedRenderer;
            materialIndex = FindFirstCompatibleMaterialIndex();
        }
    }

    private int FindFirstCompatibleMaterialIndex()
    {
        if (targetRenderer == null)
        {
            return 0;
        }

        Material[] materials = targetRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (SupportsTriplanarMasks(materials[i]))
            {
                return i;
            }
        }

        return 0;
    }

    private Material GetTargetMaterial()
    {
        if (targetRenderer == null)
        {
            return null;
        }

        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            return null;
        }

        materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
        return materials[materialIndex];
    }

    private Collider GetTargetCollider()
    {
        return targetRenderer != null ? targetRenderer.GetComponent<Collider>() : null;
    }

    private MeshFilter GetTargetMeshFilter()
    {
        return targetRenderer != null ? targetRenderer.GetComponent<MeshFilter>() : null;
    }

    private bool SupportsTriplanarMasks(Material material)
    {
        if (material == null)
        {
            return false;
        }

        for (int i = 0; i < MaskPropertyNames.Length; i++)
        {
            if (!material.HasProperty(MaskPropertyNames[i]))
            {
                return false;
            }
        }

        return material.HasProperty(BoundsMinPropertyName) && material.HasProperty(BoundsSizePropertyName);
    }

    private void RefreshMaskReferences(Material material)
    {
        for (int i = 0; i < MaskPropertyNames.Length; i++)
        {
            sourceMaskTextures[i] = material != null ? material.GetTexture(MaskPropertyNames[i]) as Texture2D : null;
        }
    }

    private bool HasAllMasks()
    {
        for (int i = 0; i < sourceMaskTextures.Length; i++)
        {
            if (sourceMaskTextures[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasLiveWorkingTextures()
    {
        for (int i = 0; i < workingTextures.Length; i++)
        {
            if (workingTextures[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureMeshCollider()
    {
        MeshFilter meshFilter = GetTargetMeshFilter();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Foliage Mask Painter", "A MeshFilter with a valid mesh is required.", "OK");
            return;
        }

        Collider existingCollider = GetTargetCollider();
        if (existingCollider != null && !(existingCollider is MeshCollider))
        {
            Undo.DestroyObjectImmediate(existingCollider);
        }

        MeshCollider meshCollider = targetRenderer.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = Undo.AddComponent<MeshCollider>(targetRenderer.gameObject);
        }

        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        EditorUtility.SetDirty(meshCollider);
    }

    private void PrepareUniqueMaterialAndMasks()
    {
        Material sourceMaterial = GetTargetMaterial();
        MeshFilter meshFilter = GetTargetMeshFilter();

        if (sourceMaterial == null || meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        string sourceMaterialPath = AssetDatabase.GetAssetPath(sourceMaterial);
        string directory = string.IsNullOrEmpty(sourceMaterialPath)
            ? "Assets"
            : Path.GetDirectoryName(sourceMaterialPath).Replace("\\", "/");

        if (string.IsNullOrEmpty(directory))
        {
            directory = "Assets";
        }

        string materialPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + sourceMaterial.name + "_" + targetRenderer.name + "_Paint.mat");
        Material uniqueMaterial = new Material(sourceMaterial);
        AssetDatabase.CreateAsset(uniqueMaterial, materialPath);

        Material[] materials = targetRenderer.sharedMaterials;
        materials[materialIndex] = uniqueMaterial;

        Undo.RecordObject(targetRenderer, "Assign Unique Paint Material");
        targetRenderer.sharedMaterials = materials;
        EditorUtility.SetDirty(targetRenderer);

        CreateAndAssignMissingMasks(uniqueMaterial);
        ApplyObjectSpaceSettings(uniqueMaterial);
        EnsureMeshCollider();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void CreateAndAssignMissingMasks(Material targetMaterial)
    {
        if (targetMaterial == null)
        {
            return;
        }

        for (int i = 0; i < MaskPropertyNames.Length; i++)
        {
            if (targetMaterial.GetTexture(MaskPropertyNames[i]) == null)
            {
                Texture2D mask = CreateMaskAsset(targetMaterial, i);
                Undo.RecordObject(targetMaterial, "Assign Foliage Mask");
                targetMaterial.SetTexture(MaskPropertyNames[i], mask);
            }
        }

        if (targetMaterial.HasProperty(UseFoliagePropertyName))
        {
            targetMaterial.SetFloat(UseFoliagePropertyName, 1f);
        }

        EditorUtility.SetDirty(targetMaterial);
        ApplyObjectSpaceSettings(targetMaterial);
        RefreshMaskReferences(targetMaterial);
    }

    private Texture2D CreateMaskAsset(Material targetMaterial, int axisIndex)
    {
        string materialPath = AssetDatabase.GetAssetPath(targetMaterial);
        string directory = string.IsNullOrEmpty(materialPath)
            ? "Assets"
            : Path.GetDirectoryName(materialPath).Replace("\\", "/");

        if (string.IsNullOrEmpty(directory))
        {
            directory = "Assets";
        }

        string axisSuffix = MaskAssetSuffixes[axisIndex];
        string maskPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + targetRenderer.name + "_" + targetMaterial.name + "_FoliageMask" + axisSuffix + ".png");

        int size = Mathf.Max(1, (int)maskResolution);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        Color32[] pixels = new Color32[size * size];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        File.WriteAllBytes(maskPath, texture.EncodeToPNG());
        DestroyImmediate(texture);

        AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
        EnsureMaskImporterSettings(maskPath);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
    }

    private void ResizeMasksToResolution(Material targetMaterial)
    {
        if (targetMaterial == null)
        {
            return;
        }

        int size = Mathf.Max(1, (int)maskResolution);

        for (int i = 0; i < MaskPropertyNames.Length; i++)
        {
            Texture2D current = targetMaterial.GetTexture(MaskPropertyNames[i]) as Texture2D;
            if (current == null)
            {
                continue;
            }

            Texture2D resized = ResizeMaskAsset(current, size);
            if (resized != null)
            {
                Undo.RecordObject(targetMaterial, "Resize Foliage Masks");
                targetMaterial.SetTexture(MaskPropertyNames[i], resized);
            }
        }

        EditorUtility.SetDirty(targetMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private Texture2D ResizeMaskAsset(Texture2D source, int newSize)
    {
        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path) || !EnsureMaskImporterSettings(path))
        {
            return null;
        }

        Texture2D readableSource = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (readableSource == null)
        {
            return null;
        }

        int sourceWidth = readableSource.width;
        int sourceHeight = readableSource.height;
        Color32[] sourcePixels = readableSource.GetPixels32();

        Texture2D resized = new Texture2D(newSize, newSize, TextureFormat.RGBA32, false, true);
        Color32[] resizedPixels = new Color32[newSize * newSize];

        for (int y = 0; y < newSize; y++)
        {
            int srcY = Mathf.Clamp(Mathf.FloorToInt((float)y / newSize * sourceHeight), 0, sourceHeight - 1);
            for (int x = 0; x < newSize; x++)
            {
                int srcX = Mathf.Clamp(Mathf.FloorToInt((float)x / newSize * sourceWidth), 0, sourceWidth - 1);
                resizedPixels[y * newSize + x] = sourcePixels[srcY * sourceWidth + srcX];
            }
        }

        resized.SetPixels32(resizedPixels);
        resized.Apply(false, false);

        File.WriteAllBytes(path, resized.EncodeToPNG());
        DestroyImmediate(resized);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        EnsureMaskImporterSettings(path);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private void EraseAllMasks(Material targetMaterial)
    {
        if (targetMaterial == null)
        {
            return;
        }

        RefreshMaskReferences(targetMaterial);

        for (int i = 0; i < sourceMaskTextures.Length; i++)
        {
            Texture2D mask = sourceMaskTextures[i];
            if (mask == null)
            {
                continue;
            }

            FillMaskAsset(mask, 0);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshMaskReferences(targetMaterial);
    }

    private void FillMaskAsset(Texture2D texture, byte value)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path) || !EnsureMaskImporterSettings(path))
        {
            return;
        }

        Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (readableTexture == null)
        {
            return;
        }

        int width = readableTexture.width;
        int height = readableTexture.height;
        Color32[] pixels = new Color32[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(value, value, value, 255);
        }

        Texture2D filled = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        filled.SetPixels32(pixels);
        filled.Apply(false, false);

        File.WriteAllBytes(path, filled.EncodeToPNG());
        DestroyImmediate(filled);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        EnsureMaskImporterSettings(path);
    }

    private void ApplyObjectSpaceSettings(Material targetMaterial)
    {
        MeshFilter meshFilter = GetTargetMeshFilter();
        if (targetMaterial == null || meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Bounds bounds = meshFilter.sharedMesh.bounds;

        Undo.RecordObject(targetMaterial, "Update Foliage Mask Bounds");
        targetMaterial.SetVector(BoundsMinPropertyName, bounds.min);
        targetMaterial.SetVector(BoundsSizePropertyName, bounds.size);
        EditorUtility.SetDirty(targetMaterial);
    }

    private void BeginStroke(Material targetMaterial)
    {
        if (isPainting)
        {
            return;
        }

        ApplyObjectSpaceSettings(targetMaterial);

        if (HasLiveWorkingTextures())
        {
            for (int i = 0; i < MaskPropertyNames.Length; i++)
            {
                if (workingTextures[i] != null)
                {
                    targetMaterial.SetTexture(MaskPropertyNames[i], workingTextures[i]);
                }
            }

            isPainting = true;
            return;
        }

        RefreshMaskReferences(targetMaterial);

        if (!HasAllMasks())
        {
            return;
        }

        for (int i = 0; i < MaskPropertyNames.Length; i++)
        {
            sourceMaskPaths[i] = AssetDatabase.GetAssetPath(sourceMaskTextures[i]);
            if (!EnsureMaskImporterSettings(sourceMaskPaths[i]))
            {
                return;
            }

            Texture2D reloadedSource = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceMaskPaths[i]);
            sourceMaskTextures[i] = reloadedSource;

            workingTextures[i] = new Texture2D(reloadedSource.width, reloadedSource.height, TextureFormat.RGBA32, false, true);
            workingTextures[i].name = reloadedSource.name + "_WorkingCopy";
            workingTextures[i].SetPixels32(reloadedSource.GetPixels32());
            workingTextures[i].Apply(false, false);

            workingPixels[i] = workingTextures[i].GetPixels32();
            targetMaterial.SetTexture(MaskPropertyNames[i], workingTextures[i]);
        }

        isPainting = true;
    }

    private void PaintAtHit(RaycastHit hit, bool erase, Material targetMaterial)
    {
        if (!isPainting)
        {
            return;
        }

        MeshFilter meshFilter = GetTargetMeshFilter();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Vector3 localPoint = targetRenderer.transform.InverseTransformPoint(hit.point);
        Vector3 localNormal = targetRenderer.transform.InverseTransformDirection(hit.normal).normalized;
        Bounds bounds = meshFilter.sharedMesh.bounds;
        float sharpness = GetSharpness(targetMaterial);
        Vector3 weights = GetTriplanarWeights(localNormal, sharpness);

        int maskIndexX = GetSignedMaskIndex(0, localNormal.x);
        int maskIndexY = GetSignedMaskIndex(1, localNormal.y);
        int maskIndexZ = GetSignedMaskIndex(2, localNormal.z);

        PaintProjectedAxis(maskIndexX, 0, localPoint, localNormal, bounds, erase, weights.x);
        PaintProjectedAxis(maskIndexY, 1, localPoint, localNormal, bounds, erase, weights.y);
        PaintProjectedAxis(maskIndexZ, 2, localPoint, localNormal, bounds, erase, weights.z);
    }

    private void PaintInterpolatedStroke(RaycastHit hit, bool erase, Material targetMaterial)
    {
        Vector3 currentLocalPoint = targetRenderer.transform.InverseTransformPoint(hit.point);

        if (!hasLastPaintPoint)
        {
            PaintAtHit(hit, erase, targetMaterial);
            lastPaintLocalPoint = currentLocalPoint;
            hasLastPaintPoint = true;
            return;
        }

        MeshFilter meshFilter = GetTargetMeshFilter();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            PaintAtHit(hit, erase, targetMaterial);
            lastPaintLocalPoint = currentLocalPoint;
            return;
        }

        Bounds bounds = meshFilter.sharedMesh.bounds;
        float brushRadiusLocal = GetBrushRadiusLocal(bounds);
        float spacing = Mathf.Max(brushRadiusLocal * 0.35f, 0.0001f);

        float distance = Vector3.Distance(lastPaintLocalPoint, currentLocalPoint);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 point = Vector3.Lerp(lastPaintLocalPoint, currentLocalPoint, t);

            RaycastHit interpolatedHit = hit;
            interpolatedHit.point = targetRenderer.transform.TransformPoint(point);

            PaintAtHit(interpolatedHit, erase, targetMaterial);
        }

        lastPaintLocalPoint = currentLocalPoint;
    }

    private void PaintProjectedAxis(int maskIndex, int axisIndex, Vector3 localPoint, Vector3 localNormal, Bounds bounds, bool erase, float axisWeight)
    {
        GetProjectedUvAndPlaneSize(axisIndex, localPoint, localNormal, bounds, out Vector2 uv, out Vector2 planeSize);

        Texture2D maskTexture = workingTextures[maskIndex] != null ? workingTextures[maskIndex] : sourceMaskTextures[maskIndex];
        if (maskTexture == null)
        {
            return;
        }

        float texelWidth = planeSize.x / Mathf.Max(maskTexture.width, 1);
        float texelHeight = planeSize.y / Mathf.Max(maskTexture.height, 1);
        float brushRadiusLocal = Mathf.Max(0.0001f, brushSizePixels * Mathf.Min(texelWidth, texelHeight));

        PaintIntoMask(maskIndex, uv, planeSize, erase, axisWeight, brushRadiusLocal);
    }

    private void PaintIntoMask(int maskIndex, Vector2 centerUv, Vector2 planeSize, bool erase, float axisWeight, float brushRadiusLocal)
    {
        if (workingTextures[maskIndex] == null || workingPixels[maskIndex] == null || axisWeight <= 0.0001f)
        {
            return;
        }

        int width = workingTextures[maskIndex].width;
        int height = workingTextures[maskIndex].height;

        float planeWidth = Mathf.Max(planeSize.x, 0.0001f);
        float planeHeight = Mathf.Max(planeSize.y, 0.0001f);

        int centerX = Mathf.RoundToInt(Mathf.Clamp01(centerUv.x) * (width - 1));
        int centerY = Mathf.RoundToInt(Mathf.Clamp01(centerUv.y) * (height - 1));

        int radiusX = Mathf.Max(1, Mathf.CeilToInt((brushRadiusLocal / planeWidth) * width) + 1);
        int radiusY = Mathf.Max(1, Mathf.CeilToInt((brushRadiusLocal / planeHeight) * height) + 1);

        int minX = Mathf.Max(0, centerX - radiusX);
        int maxX = Mathf.Min(width - 1, centerX + radiusX);
        int minY = Mathf.Max(0, centerY - radiusY);
        int maxY = Mathf.Min(height - 1, centerY + radiusY);

        float innerRadius = brushRadiusLocal * (1f - brushSoftness);
        byte targetValue = erase ? (byte)0 : (byte)255;

        for (int y = minY; y <= maxY; y++)
        {
            int rowOffset = y * width;
            float pixelMinV = (y / (float)height - centerUv.y) * planeHeight;
            float pixelSizeV = planeHeight / height;

            for (int x = minX; x <= maxX; x++)
            {
                float pixelMinU = (x / (float)width - centerUv.x) * planeWidth;
                float pixelSizeU = planeWidth / width;

                float coverage = GetBrushCoverage(pixelMinU, pixelMinV, pixelSizeU, pixelSizeV, innerRadius, brushRadiusLocal);
                if (coverage <= 0.0001f)
                {
                    continue;
                }

                float blend = brushStrength * coverage * axisWeight;
                int pixelIndex = rowOffset + x;

                Color32 pixel = workingPixels[maskIndex][pixelIndex];
                byte next = (byte)Mathf.RoundToInt(Mathf.Lerp(pixel.r, targetValue, blend));

                pixel.r = next;
                pixel.g = next;
                pixel.b = next;
                pixel.a = 255;
                workingPixels[maskIndex][pixelIndex] = pixel;
            }
        }

        workingTextures[maskIndex].SetPixels32(workingPixels[maskIndex]);
        workingTextures[maskIndex].Apply(false, false);
        strokeDirty = true;
    }

    private static float GetBrushCoverage(float pixelMinU, float pixelMinV, float pixelSizeU, float pixelSizeV, float innerRadius, float outerRadius)
    {
        const int sampleCount = 4;
        float total = 0f;

        for (int sy = 0; sy < sampleCount; sy++)
        {
            float fy = (sy + 0.5f) / sampleCount;
            float y = pixelMinV + pixelSizeV * fy;

            for (int sx = 0; sx < sampleCount; sx++)
            {
                float fx = (sx + 0.5f) / sampleCount;
                float x = pixelMinU + pixelSizeU * fx;

                float distance = Mathf.Sqrt(x * x + y * y);
                if (distance >= outerRadius)
                {
                    continue;
                }

                if (distance <= innerRadius || outerRadius <= innerRadius)
                {
                    total += 1f;
                }
                else
                {
                    float t = Mathf.InverseLerp(innerRadius, outerRadius, distance);
                    total += 1f - t;
                }
            }
        }

        return total / (sampleCount * sampleCount);
    }

    private static float DistanceToPixelCell(float minX, float maxX, float minY, float maxY)
    {
        float nearestX = 0f;
        if (0f < minX) nearestX = minX;
        else if (0f > maxX) nearestX = maxX;

        float nearestY = 0f;
        if (0f < minY) nearestY = minY;
        else if (0f > maxY) nearestY = maxY;

        return Mathf.Sqrt(nearestX * nearestX + nearestY * nearestY);
    }

    private void GetProjectedUvAndPlaneSize(int axisIndex, Vector3 localPoint, Vector3 localNormal, Bounds bounds, out Vector2 uv, out Vector2 planeSize)
    {
        Vector3 min = bounds.min;
        Vector3 size = bounds.size;

        size.x = Mathf.Max(size.x, 0.0001f);
        size.y = Mathf.Max(size.y, 0.0001f);
        size.z = Mathf.Max(size.z, 0.0001f);

        if (axisIndex == 0)
        {
            uv = new Vector2(
                Mathf.InverseLerp(min.z, min.z + size.z, localPoint.z),
                Mathf.InverseLerp(min.y, min.y + size.y, localPoint.y));

            planeSize = new Vector2(size.z, size.y);
            return;
        }

        if (axisIndex == 1)
        {
            uv = new Vector2(
                Mathf.InverseLerp(min.x, min.x + size.x, localPoint.x),
                Mathf.InverseLerp(min.z, min.z + size.z, localPoint.z));

            planeSize = new Vector2(size.x, size.z);
            return;
        }

        uv = new Vector2(
            Mathf.InverseLerp(min.x, min.x + size.x, localPoint.x),
            Mathf.InverseLerp(min.y, min.y + size.y, localPoint.y));

        planeSize = new Vector2(size.x, size.y);
    }

    private Vector3 GetTriplanarWeights(Vector3 localNormal, float sharpness)
    {
        Vector3 n = new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z));
        n.x = Mathf.Pow(Mathf.Max(n.x, 0.0001f), sharpness);
        n.y = Mathf.Pow(Mathf.Max(n.y, 0.0001f), sharpness);
        n.z = Mathf.Pow(Mathf.Max(n.z, 0.0001f), sharpness);

        float sum = n.x + n.y + n.z;
        if (sum <= 0.0001f)
        {
            return new Vector3(0.3333f, 0.3333f, 0.3333f);
        }

        return n / sum;
    }

    private float GetSharpness(Material material)
    {
        if (material != null && material.HasProperty(TriplanarSharpnessPropertyName))
        {
            return Mathf.Max(1f, material.GetFloat(TriplanarSharpnessPropertyName));
        }

        return 2f;
    }

    private float GetBrushRadiusLocal(Bounds bounds)
    {
        return Mathf.Max(0.0001f, brushSizePixels);
    }

    private float GetReferenceMaskResolution()
    {
        if (sourceMaskTextures[0] != null)
        {
            return sourceMaskTextures[0].width;
        }

        return (int)maskResolution;
    }

    private void SaveWorkingTexturesIfNeeded()
    {
        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial == null)
        {
            CancelStrokePreview();
            return;
        }

        if (!HasLiveWorkingTextures())
        {
            return;
        }

        if (!strokeDirty)
        {
            RestoreSourceTextureReferences(targetMaterial);
            ClearWorkingState();
            return;
        }

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < workingTextures.Length; i++)
            {
                if (workingTextures[i] == null || string.IsNullOrEmpty(sourceMaskPaths[i]))
                {
                    continue;
                }

                File.WriteAllBytes(sourceMaskPaths[i], workingTextures[i].EncodeToPNG());
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        for (int i = 0; i < workingTextures.Length; i++)
        {
            if (string.IsNullOrEmpty(sourceMaskPaths[i]))
            {
                continue;
            }

            Texture2D reloadedSource = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceMaskPaths[i]);
            sourceMaskTextures[i] = reloadedSource;
            targetMaterial.SetTexture(MaskPropertyNames[i], reloadedSource);
        }

        EditorUtility.SetDirty(targetMaterial);
        ClearWorkingState();
    }

    private void CancelStrokePreview()
    {
        Material targetMaterial = GetTargetMaterial();
        if (targetMaterial != null)
        {
            RestoreSourceTextureReferences(targetMaterial);
        }

        ClearWorkingState();
    }

    private void RestoreSourceTextureReferences(Material targetMaterial)
    {
        if (targetMaterial == null)
        {
            return;
        }

        for (int i = 0; i < sourceMaskTextures.Length; i++)
        {
            if (sourceMaskTextures[i] != null)
            {
                targetMaterial.SetTexture(MaskPropertyNames[i], sourceMaskTextures[i]);
            }
        }

        EditorUtility.SetDirty(targetMaterial);
    }

    private void ClearWorkingState()
    {
        for (int i = 0; i < workingTextures.Length; i++)
        {
            if (workingTextures[i] != null)
            {
                DestroyImmediate(workingTextures[i]);
            }

            workingTextures[i] = null;
            workingPixels[i] = null;
            sourceMaskPaths[i] = null;
        }

        strokeDirty = false;
        isPainting = false;
    }

    private bool EnsureMaskImporterSettings(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return false;
        }

        bool changed = false;

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return true;
    }

    private float GetWorldBrushSize()
    {
        MeshFilter meshFilter = GetTargetMeshFilter();
        if (meshFilter == null || meshFilter.sharedMesh == null || targetRenderer == null)
        {
            return 0.1f;
        }

        float referenceResolution = GetReferenceMaskResolution();
        float maxLocalDimension = Mathf.Max(
            meshFilter.sharedMesh.bounds.size.x,
            Mathf.Max(meshFilter.sharedMesh.bounds.size.y, meshFilter.sharedMesh.bounds.size.z));

        float localTexelSize = maxLocalDimension / Mathf.Max(referenceResolution, 1f);
        float localRadius = Mathf.Max(0.0001f, brushSizePixels * 0.5f * localTexelSize);

        Vector3 scale = targetRenderer.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

        return Mathf.Max(0.02f, localRadius * maxScale);
    }

    private bool IsErase(Event currentEvent)
    {
        return currentEvent.shift || brushMode == BrushMode.Erase;
    }

    private Color GetBrushPreviewColor(bool erase)
    {
        return erase
            ? new Color(1f, 0.25f, 0.25f, 0.9f)
            : new Color(0.2f, 1f, 0.2f, 0.9f);
    }

    private static string[] GetMaterialNames(Material[] materials)
    {
        string[] names = new string[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            names[i] = materials[i] != null ? materials[i].name : "<None>";
        }

        return names;
    }

    // Add this helper near the other painter helpers:
    private static int GetSignedMaskIndex(int axisIndex, float normalComponent)
    {
        return axisIndex * 2 + (normalComponent < 0f ? 1 : 0);
    }
}