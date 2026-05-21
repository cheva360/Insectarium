using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    private readonly List<Portal> _portals = new();

    private void Awake()
    {
        Instance = this;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    public void Register(Portal portal)
    {
        if (!_portals.Contains(portal))
            _portals.Add(portal);
    }

    public void Unregister(Portal portal)
    {
        _portals.Remove(portal);
    }

    // Called by PortalRenderFeature to iterate portals this frame.
    public IReadOnlyList<Portal> GetPortals() => _portals;

    // Called from Update() — runs teleports first, then updates RT positions.
    // Keeping both in Update() ensures transforms are stable for the entire frame.
    private void Update()
    {
        foreach (var portal in _portals)
            portal.CheckTeleportThisFrame();
    }

    // Renders all exit cameras to their RTs immediately before the player
    // camera renders, so the RTs are current when the render feature blits them.
    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != playerController.Instance?.playerCamera) return;

        foreach (var portal in _portals)
        {
            if (!portal.ShouldRender())
                continue;

            portal.RenderPortalCamera();
        }
    }
}