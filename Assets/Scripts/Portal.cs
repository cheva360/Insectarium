using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(MeshRenderer))]
public class Portal : MonoBehaviour
{
    [Header("Portal Pair")]
    [Tooltip("The other portal this one is linked to.")]
    public Portal linkedPortal;

    private Camera        _portalCam;
    private RenderTexture _renderTexture;
    private MeshRenderer  _meshRenderer;
    private Material      _portalMaterial;
    private Collider      _portalCollider;

    private Transform _playerTransform;
    private Camera    _playerCam;

    private float _prevSignedDist;
    private bool  _prevDistValid;

    private float _teleportCooldown;
    private const float CooldownDuration = 0.3f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        _meshRenderer   = GetComponent<MeshRenderer>();
        _portalCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        var pc = playerController.Instance;
        _playerTransform = pc.transform;
        _playerCam       = pc.playerCamera;

        // Put this portal on the Portal layer so portal cameras can exclude it
        // from their culling mask — prevents the back-face feedback artifact
        // without any mesh hiding or flickering.
        gameObject.layer = LayerMask.NameToLayer("Portal");

        CreatePortalCamera();
        CreateRenderTexture();
        AssignMaterial();

        if (_portalCollider != null)
        {
            var cc  = _playerTransform.GetComponent<CharacterController>();
            var col = _playerTransform.GetComponent<Collider>();
            if (cc  != null) Physics.IgnoreCollision(_portalCollider, cc,  true);
            if (col != null) Physics.IgnoreCollision(_portalCollider, col, true);
        }

        PortalManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); }
        if (_portalMaterial != null) Destroy(_portalMaterial);
        if (_portalCam      != null) Destroy(_portalCam.gameObject);
        PortalManager.Instance?.Unregister(this);
    }

    // ── Teleport check — called by PortalManager.Update() ────────────────────
    // Runs in game logic time so the teleport is fully committed before any
    // rendering occurs this frame. This prevents UI/scene flicker.
    internal void CheckTeleportThisFrame()
    {
        if (linkedPortal == null) return;

        if (_teleportCooldown > 0f)
        {
            _teleportCooldown -= Time.deltaTime;
            _prevSignedDist = SignedDist(transform, _playerCam.transform.position);
            _prevDistValid  = true;
            return;
        }

        float signedDist = SignedDist(transform, _playerCam.transform.position);

        if (_prevDistValid)
        {
            const float teleportThreshold = 0.1f;
            bool crossedThreshold = _prevSignedDist <= -teleportThreshold && signedDist > -teleportThreshold;
            bool crossedZero      = _prevSignedDist < 0f && signedDist >= 0f;

            if ((crossedThreshold || crossedZero) && IsInsidePortalBounds(_playerCam.transform.position))
            {
                TeleportPlayer();
                return;
            }
        }

        _prevSignedDist = signedDist;
        _prevDistValid  = true;
    }

    // ── Render — called by PortalManager.OnBeginCameraRendering() ─────────────
    public void RenderPortalCamera()
    {
        if (linkedPortal == null || _portalCam == null) return;

        EnsureRenderTextureMatchesScreen();

        _portalCam.transform.SetPositionAndRotation(
            TransformPointThroughPortal(linkedPortal.transform, transform, _playerCam.transform.position),
            TransformRotationThroughPortal(linkedPortal.transform, transform, _playerCam.transform.rotation)
        );

        _portalCam.projectionMatrix = _playerCam.projectionMatrix;
        _portalCam.nearClipPlane    = _playerCam.nearClipPlane;
        _portalCam.farClipPlane     = _playerCam.farClipPlane;

        ApplyObliqueNearClip();
        _portalCam.Render();
    }

    // ── Render texture ────────────────────────────────────────────────────────
    private void EnsureRenderTextureMatchesScreen()
    {
        int w = Screen.width;
        int h = Screen.height;

        if (_renderTexture != null &&
            _renderTexture.width  == w &&
            _renderTexture.height == h)
            return;

        if (_renderTexture != null)
        {
            _portalCam.targetTexture = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        _renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = $"PortalRT_{name}"
        };
        _renderTexture.Create();
        _portalCam.targetTexture = _renderTexture;
        _portalMaterial.SetTexture(MainTexId, _renderTexture);
    }

    // ── Teleport ──────────────────────────────────────────────────────────────
    private void TeleportPlayer()
    {
        var pc = playerController.Instance;
        if (pc == null) return;

        Vector3    newPos = TransformPointThroughPortal(transform, linkedPortal.transform, _playerTransform.position);
        Quaternion newRot = TransformRotationThroughPortal(transform, linkedPortal.transform, _playerTransform.rotation);

        const float exitOffset = 0.15f;
        newPos -= linkedPortal.transform.forward * exitOffset;

        var cc = pc.GetComponent<CharacterController>();
        cc.enabled = false;
        _playerTransform.SetPositionAndRotation(newPos, newRot);
        cc.enabled = true;

        pc.OnPortalTeleport();

        linkedPortal.RenderPortalCamera();

        _prevDistValid    = false;
        _teleportCooldown = CooldownDuration;

        linkedPortal._prevDistValid    = false;
        linkedPortal._teleportCooldown = CooldownDuration;
    }

    internal IEnumerator HideMeshForOneFrame()
    {
        _meshRenderer.enabled = false;
        yield return null;
        _meshRenderer.enabled = true;
    }

    // ── Setup helpers ─────────────────────────────────────────────────────────
    private void CreatePortalCamera()
    {
        var go = new GameObject($"PortalCam_{name}", typeof(Camera));
        go.transform.SetParent(transform, false);

        go.AddComponent<PortalCameraMarker>();

        _portalCam = go.GetComponent<Camera>();
        _portalCam.enabled         = false;
        _portalCam.clearFlags      = CameraClearFlags.SolidColor;
        _portalCam.backgroundColor = Color.black;

        // Exclude the Portal layer from the portal camera's culling mask.
        // This means portal camera A never sees portal mesh B (the linked portal),
        // eliminating the back-face feedback loop entirely — no mesh hiding needed.
        _portalCam.cullingMask = _playerCam != null
            ? _playerCam.cullingMask & ~LayerMask.GetMask("Portal")
            : Camera.main.cullingMask & ~LayerMask.GetMask("Portal");

        var playerURP = _playerCam != null ? _playerCam.GetUniversalAdditionalCameraData() : null;
        var portalURP = _portalCam.GetUniversalAdditionalCameraData();
        portalURP.renderType           = CameraRenderType.Base;
        portalURP.renderPostProcessing = false;
        if (playerURP != null)
        {
            portalURP.renderShadows = playerURP.renderShadows;
            portalURP.antialiasing  = AntialiasingMode.None;
        }
    }

    private void CreateRenderTexture()
    {
        _renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
        {
            name = $"PortalRT_{name}"
        };
        _renderTexture.Create();
        _portalCam.targetTexture = _renderTexture;
    }

    private void AssignMaterial()
    {
        var shader = Shader.Find("Custom/Portal");
        if (shader == null)
        {
            Debug.LogError("[Portal] Shader 'Custom/Portal' not found.");
            return;
        }
        _portalMaterial = new Material(shader);
        _portalMaterial.SetTexture(MainTexId, _renderTexture);
        _meshRenderer.material = _portalMaterial;
    }

    // ── Oblique near-clip ─────────────────────────────────────────────────────
    private void ApplyObliqueNearClip()
    {
        Transform clipPlane = linkedPortal.transform;
        int side = System.Math.Sign(
            Vector3.Dot(clipPlane.forward, clipPlane.position - _portalCam.transform.position));

        Vector3 camSpacePos    = _portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = _portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * side;
        float   camSpaceDist   = -Vector3.Dot(camSpacePos, camSpaceNormal) + 0.01f;

        _portalCam.projectionMatrix = _portalCam.CalculateObliqueMatrix(
            new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDist));
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
}
