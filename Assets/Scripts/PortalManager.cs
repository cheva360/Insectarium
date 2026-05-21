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

    // Called from Update() — runs teleports first, then updates RT positions.
    // Keeping both in Update() ensures transforms are stable for the entire frame.
    private void Update()
    {
        // Pass 1 — check for crossings and execute any pending teleport.
        // Teleporting here (game logic time) means all scene objects, UI, and
        // shadow casters see the new position consistently for this frame's render.
        foreach (var portal in _portals)
            portal.CheckTeleportThisFrame();
    }

    // Render callback — only used to position and render the portal cameras
    // immediately before the player camera renders. No transforms are moved here.
    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != playerController.Instance?.playerCamera) return;

        foreach (var portal in _portals)
            portal.RenderPortalCamera();
    }
}