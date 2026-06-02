using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class Portal : MonoBehaviour
{
    [Header("Portal Pair")]
    [Tooltip("The other portal this one is linked to.")]
    public Portal linkedPortal;

    [Header("Performance")]
    [Range(0.15f, 1f)]
    [SerializeField] private float renderScale = 0.5f;
    [SerializeField] private bool renderPortalShadows = false;
    [SerializeField] private LayerMask portalCullingMask = ~0;

    [Header("Door")]
    [SerializeField] private GameObject door;
    [SerializeField] private float doorOpenDuration = 0.8f;
    [SerializeField] private float portalShrinkDuration = 0.6f;

    private Camera        _portalCam;
    private RenderTexture _rt;
    private MeshRenderer  _meshRenderer;
    private MeshFilter    _meshFilter;
    private Collider      _portalCollider;
    private Transform     _playerTransform;
    private Camera        _playerCam;

    private float _prevSignedDist;
    private bool  _prevDistValid;
    private float _teleportCooldown;
    private const float CooldownDuration = 0.05f;
    private const float CooldownClearDistance = 0.2f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    // ── Public surface for PortalRenderFeature ────────────────────────────────
    public RenderTexture ExitRT     => _rt;
    public Mesh          PortalMesh => _meshFilter.sharedMesh;
    public bool          IsReady    => linkedPortal != null && _rt != null && _portalCam != null && ViewMaterial != null;
    public Material      ViewMaterial { get; private set; }

    // Cached per-frame straddle state — set by PortalManager.Update() after all
    // teleport checks, so it is identical for RenderPortalCamera() and the render
    // feature. Never recomputed mid-frame.
    public bool IsPlayerStraddling { get; private set; }

    internal void UpdateStraddleState()
    {
        if (_playerCam == null) { IsPlayerStraddling = false; return; }

        Vector3 camPos = _playerCam.transform.position;
        float dist = Vector3.Dot(transform.forward, camPos - transform.position);
        if (Mathf.Abs(dist) >= 0.3f) { IsPlayerStraddling = false; return; }

        Vector3 local = transform.InverseTransformPoint(camPos);
        IsPlayerStraddling = Mathf.Abs(local.x) <= 1.1f && Mathf.Abs(local.y) <= 1.1f;
    }

    internal void ClearStraddleState()
    {
        IsPlayerStraddling = false;
    }

    // Called by PortalRenderFeature once its view shader is ready.
    // Creates a per-portal material instance with the RT already baked in so
    // the render feature never needs SetGlobalTexture.
    public void SetViewShader(Shader viewShader)
    {
        if (ViewMaterial != null) CoreUtils.Destroy(ViewMaterial);
        ViewMaterial = new Material(viewShader) { hideFlags = HideFlags.HideAndDontSave };
        ViewMaterial.SetTexture(MainTexId, _rt);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _meshRenderer   = GetComponent<MeshRenderer>();
        _meshFilter     = GetComponent<MeshFilter>();
        _portalCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        var pc = playerController.Instance;
        _playerTransform = pc.transform;
        _playerCam       = pc.playerCamera;

        // Portal layer keeps portal cameras from seeing portal meshes,
        // preventing RT feedback loops.
        gameObject.layer = LayerMask.NameToLayer("Portal");

        // The render feature owns all portal drawing via stencil passes.
        // Disable the MeshRenderer so the quad doesn't render as geometry.
        _meshRenderer.enabled = false;

        CreatePortalCamera();
        CreateRenderTexture();

        if (_portalCollider != null)
        {
            // Collider starts disabled; Physics.IgnoreCollision still works on
            // disabled colliders, so we can pre-register the ignore safely.
            var cc  = _playerTransform.GetComponent<CharacterController>();
            var col = _playerTransform.GetComponent<Collider>();
            if (cc  != null) Physics.IgnoreCollision(_portalCollider, cc,  true);
            if (col != null) Physics.IgnoreCollision(_portalCollider, col, true);
        }

        PortalManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (_rt != null)       { _rt.Release(); Destroy(_rt); }
        if (_portalCam != null) Destroy(_portalCam.gameObject);
        PortalManager.Instance?.Unregister(this);
    }

    private void OnValidate()
    {
        if (_portalCam == null) return;

        _portalCam.cullingMask = portalCullingMask & ~LayerMask.GetMask("Portal");

        var portalURP = _portalCam.GetUniversalAdditionalCameraData();
        portalURP.renderShadows = renderPortalShadows;
    }

    // ── Teleport check — called by PortalManager.Update() ────────────────────
    internal void CheckTeleportThisFrame()
    {
        if (!_canTeleport || linkedPortal == null) return;

        float signedDist = SignedDist(transform, _playerCam.transform.position);
        bool insideBounds = IsInsidePortalBounds(_playerCam.transform.position);

        if (_teleportCooldown > 0f)
        {
            _teleportCooldown -= Time.deltaTime;

            if (Mathf.Abs(signedDist) > CooldownClearDistance || !insideBounds)
                _teleportCooldown = 0f;

            _prevSignedDist = signedDist;
            _prevDistValid  = true;
            return;
        }

        if (_prevDistValid)
        {
            const float teleportThreshold = 0.035f;
            bool crossedThreshold = _prevSignedDist <= -teleportThreshold &&
                                    signedDist >  -teleportThreshold;

            if (crossedThreshold && insideBounds)
            {
                TeleportPlayer();
                return;
            }
        }

        _prevSignedDist = signedDist;
        _prevDistValid  = true;
    }

    // ── Exit camera render ────────────────────────────────────────────────────
    public void RenderPortalCamera()
    {
        if (linkedPortal == null || _portalCam == null) return;

        EnsureRenderTextureMatchesScreen();

        _portalCam.transform.SetPositionAndRotation(
            TransformPointThroughPortal(transform, linkedPortal.transform, _playerCam.transform.position),
            TransformRotationThroughPortal(transform, linkedPortal.transform, _playerCam.transform.rotation)
        );

        _portalCam.projectionMatrix = _playerCam.projectionMatrix;
        _portalCam.nearClipPlane    = _playerCam.nearClipPlane;
        _portalCam.farClipPlane     = _playerCam.farClipPlane;

        ApplyObliqueNearClip();
        _portalCam.Render();
    }

    // ── Render texture ────────────────────────────────────────────────────────
    private void CreatePortalCamera()
    {
        var go = new GameObject($"PortalCam_{name}", typeof(Camera));
        go.transform.SetParent(transform, false);
        go.AddComponent<PortalCameraMarker>();

        _portalCam = go.GetComponent<Camera>();
        _portalCam.enabled             = false;
        _portalCam.clearFlags          = CameraClearFlags.SolidColor;
        _portalCam.backgroundColor     = Color.black;
        _portalCam.allowHDR            = false;
        _portalCam.allowMSAA           = false;
        _portalCam.useOcclusionCulling = true;

        // Use a dedicated culling mask for portal renders, but never allow the
        // Portal layer itself or the portal cameras will see portal surfaces and
        // create feedback / recursion artifacts.
        _portalCam.cullingMask = portalCullingMask & ~LayerMask.GetMask("Portal");

        var portalURP = _portalCam.GetUniversalAdditionalCameraData();
        portalURP.renderType           = CameraRenderType.Base;
        portalURP.renderPostProcessing = false;
        portalURP.renderShadows        = renderPortalShadows;
        portalURP.antialiasing         = AntialiasingMode.None;
    }

    private void CreateRenderTexture()
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * renderScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * renderScale));

        _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = $"PortalRT_{name}"
        };
        _rt.Create();
        _portalCam.targetTexture = _rt;
    }

    private void EnsureRenderTextureMatchesScreen()
    {
        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * renderScale));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * renderScale));
        if (_rt != null && _rt.width == w && _rt.height == h) return;

        if (_rt != null) { _portalCam.targetTexture = null; _rt.Release(); Destroy(_rt); }

        _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = $"PortalRT_{name}" };
        _rt.Create();
        _portalCam.targetTexture = _rt;

        // Keep the per-portal view material in sync with the new RT
        if (ViewMaterial != null)
            ViewMaterial.SetTexture(Shader.PropertyToID("_MainTex"), _rt);
    }

    // ── Teleport ──────────────────────────────────────────────────────────────
    private void TeleportPlayer()
    {
        var pc = playerController.Instance;
        if (pc == null) return;

        Vector3 newPos = TransformPointThroughPortal(transform, linkedPortal.transform, _playerTransform.position);
        Quaternion newRot = TransformRotationThroughPortal(transform, linkedPortal.transform, _playerTransform.rotation);

        const float exitOffset = -0.055f;
        newPos += linkedPortal.transform.forward * exitOffset;

        var cc = pc.GetComponent<CharacterController>();
        cc.enabled = false;
        _playerTransform.SetPositionAndRotation(newPos, newRot);
        cc.enabled = true;

        pc.OnPortalTeleport();

        linkedPortal.RenderPortalCamera();

        DisablePortalPair();
        RestoreExitPortalCollision();
        StartClosureSequence();
    }

    // Waits until the player has fully left the exit portal's bounds (with leeway)
    // before re-enabling its collider against the player.
    private const float ExitClearLeeway = 0.25f; // extra world-units of clearance

    private IEnumerator RestoreExitPortalCollisionWhenClear()
    {
        if (linkedPortal == null || linkedPortal._portalCollider == null || _playerTransform == null)
            yield break;

        // Poll every frame until the player is outside the exit portal bounds + leeway.
        while (true)
        {
            Vector3 local = linkedPortal.transform.InverseTransformPoint(_playerTransform.position);
            bool stillInside = Mathf.Abs(local.x) <= 1f + ExitClearLeeway
                            && Mathf.Abs(local.y) <= 1f + ExitClearLeeway
                            && Mathf.Abs(local.z) <= ExitClearLeeway;

            if (!stillInside) break;

            yield return null;
        }

        RestoreExitPortalCollision();
    }

    // ── Oblique near-clip ─────────────────────────────────────────────────────
    private void ApplyObliqueNearClip()
    {
        Transform exit = linkedPortal.transform;

        // The portal camera is always on the BACK side of the exit portal
        // (-exit.forward side), because TransformPointThroughPortal flips 180°.
        // The oblique clip plane must point TOWARD the camera (i.e. -exit.forward)
        // so that geometry between the camera and the exit surface is clipped,
        // and everything visible through the portal opening is kept.
        // Using +exit.forward does the opposite — clips the visible scene, leaving black.
        const float clipBias = 0.005f;

        // Normal = -exit.forward (facing the camera behind the exit portal).
        // Origin is inset slightly toward the camera to avoid clipping the portal
        // surface itself and to stay stable at the midpoint.
        Plane p = new Plane(-exit.forward, exit.position + exit.forward * clipBias);

        Vector4 clipPlane = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);

        Vector4 clipPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(_portalCam.worldToCameraMatrix)) * clipPlane;

        _portalCam.projectionMatrix = _playerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    // ── Math helpers ──────────────────────────────────────────────────────────
    private static float SignedDist(Transform portal, Vector3 point)
        => Vector3.Dot(portal.forward, point - portal.position);

    private bool IsInsidePortalBounds(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        return Mathf.Abs(local.x) <= 0.9f && Mathf.Abs(local.y) <= 0.9f;
    }

    public static Vector3 TransformPointThroughPortal(Transform inPortal, Transform outPortal, Vector3 point)
        => outPortal.TransformPoint(FlipMatrix.MultiplyPoint3x4(inPortal.InverseTransformPoint(point)));

    public static Quaternion TransformRotationThroughPortal(Transform inPortal, Transform outPortal, Quaternion rotation)
        => outPortal.rotation * FlipRotation * Quaternion.Inverse(inPortal.rotation) * rotation;

    private static readonly Quaternion FlipRotation = Quaternion.Euler(0f, 180f, 0f);
    private static readonly Matrix4x4  FlipMatrix   = Matrix4x4.TRS(Vector3.zero, FlipRotation, Vector3.one);

    public bool ShouldRender()
    {
        if (linkedPortal == null || _playerCam == null) return false;

        // Never cull while the player is right on the portal.
        float signedDist = SignedDist(transform, _playerCam.transform.position);
        if (Mathf.Abs(signedDist) <= 0.2f && IsInsidePortalBounds(_playerCam.transform.position))
            return true;

        Vector3 toPortal = transform.position - _playerCam.transform.position;

        // Skip portals behind the player.
        if (Vector3.Dot(_playerCam.transform.forward, toPortal) <= 0f)
            return false;

        // Skip portals outside the camera frustum.
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_playerCam);
        Bounds bounds = _meshFilter != null && _meshFilter.sharedMesh != null
            ? _meshFilter.sharedMesh.bounds
            : new Bounds(Vector3.zero, Vector3.one);
        bounds = TransformBounds(bounds, transform.localToWorldMatrix);

        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 localToWorld)
    {
        Vector3 center = localToWorld.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;

        Vector3 axisX = localToWorld.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, extents.z));

        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds(center, extents * 2f);
    }

    private bool _canTeleport = true;
    private bool _doorAnimationStarted;

    private void DisablePortalPair()
    {
        _canTeleport = false;
        _prevDistValid = false;
        _teleportCooldown = 0f;

        if (linkedPortal == null) return;

        linkedPortal._canTeleport = false;
        linkedPortal._prevDistValid = false;
        linkedPortal._teleportCooldown = 0f;
    }

    private void StartClosureSequence()
    {
        if (_doorAnimationStarted) return;
        _doorAnimationStarted = true;
        StartCoroutine(AnimateClosureSequence());
    }

    private IEnumerator AnimateClosureSequence()
    {
        if (door != null)
            yield return AnimateDoorOpen();

        if (linkedPortal != null)
            yield return ShrinkExitPortalFromTopToBottom();
    }

    private IEnumerator AnimateDoorOpen()
    {
        Transform doorTransform = door.transform;
        Vector3 euler = doorTransform.localEulerAngles;

        float startY = euler.y;
        float endY = startY - 160f;
        float elapsed = 0f;

        while (elapsed < doorOpenDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / doorOpenDuration);
            float easedT = EaseInCubic(t);

            euler.y = Mathf.LerpAngle(startY, endY, easedT);
            doorTransform.localEulerAngles = euler;

            yield return null;
        }

        euler.y = endY;
        doorTransform.localEulerAngles = euler;
    }

    private IEnumerator ShrinkExitPortalFromTopToBottom()
    {
        Transform exitTransform = linkedPortal.transform;

        Vector3 startScale = exitTransform.localScale;
        Vector3 startPosition = exitTransform.localPosition;

        float elapsed = 0f;

        while (elapsed < portalShrinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / portalShrinkDuration);
            float easedT = EaseOutCubic(t);
            float newYScale = Mathf.Lerp(startScale.y, 0f, easedT);

            Vector3 scale = startScale;
            scale.y = newYScale;
            exitTransform.localScale = scale;

            Vector3 position = startPosition;
            position.y = startPosition.y - ((startScale.y - newYScale) * 0.5f);
            exitTransform.localPosition = position;

            yield return null;
        }

        Vector3 finalScale = startScale;
        finalScale.y = 0f;
        exitTransform.localScale = finalScale;

        Vector3 finalPosition = startPosition;
        finalPosition.y = startPosition.y - (startScale.y * 0.5f);
        exitTransform.localPosition = finalPosition;
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private static float EaseOutCubic(float t)
    {
        float x = 1f - t;
        return 1f - (x * x * x);
    }

    private void RestoreExitPortalCollision()
    {
        if (linkedPortal == null || linkedPortal._portalCollider == null || _playerTransform == null)
            return;

        // Enable the collider now that the player has passed through.
        linkedPortal._portalCollider.enabled = true;

        var cc = _playerTransform.GetComponent<CharacterController>();
        var col = _playerTransform.GetComponent<Collider>();

        if (cc != null)
            Physics.IgnoreCollision(linkedPortal._portalCollider, cc, false);

        if (col != null)
            Physics.IgnoreCollision(linkedPortal._portalCollider, col, false);
    }
}
