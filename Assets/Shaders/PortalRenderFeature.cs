using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PortalRenderFeature : ScriptableRendererFeature
{
    [Header("Portal Shaders")]
    public Shader maskShader;
    public Shader viewShader;
    public Shader sealShader;

    private PortalPass _pass;

    public override void Create()
    {
        _pass = new PortalPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (PortalManager.Instance == null) return;
        if (playerController.Instance == null) return;

        var cam = renderingData.cameraData.camera;
        if (cam != playerController.Instance.playerCamera) return;

        var portals = PortalManager.Instance.GetPortals();
        if (portals == null || portals.Count == 0) return;

        if (!_pass.EnsureMaterials(maskShader, viewShader, sealShader, portals)) return;

        _pass.Setup(portals);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Cleanup();
    }

    private sealed class PortalPass : ScriptableRenderPass
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Material _maskMat;
        private Material _viewMat;
        private Material _sealMat;
        private bool     _materialsReady;

        private IReadOnlyList<Portal> _portals;

        public bool EnsureMaterials(Shader mask, Shader view, Shader seal)
        {
            if (_materialsReady && _maskMat != null && _viewMat != null && _sealMat != null)
                return true;

            Cleanup();

            if (mask == null) { Debug.LogError("[PortalRenderFeature] Mask Shader not assigned.");  return false; }
            if (view == null) { Debug.LogError("[PortalRenderFeature] View Shader not assigned.");  return false; }
            if (seal == null) { Debug.LogError("[PortalRenderFeature] Seal Shader not assigned.");  return false; }

            _maskMat = new Material(mask) { hideFlags = HideFlags.HideAndDontSave };
            _sealMat = new Material(seal) { hideFlags = HideFlags.HideAndDontSave };

            _materialsReady = true;
            return true;
        }

        public bool EnsureMaterials(Shader mask, Shader view, Shader seal, IReadOnlyList<Portal> portals)
        {
            if (_materialsReady && _maskMat != null && _sealMat != null)
                return true;

            Cleanup();

            if (mask == null) { Debug.LogError("[PortalRenderFeature] Mask Shader not assigned.");  return false; }
            if (view == null) { Debug.LogError("[PortalRenderFeature] View Shader not assigned.");  return false; }
            if (seal == null) { Debug.LogError("[PortalRenderFeature] Seal Shader not assigned.");  return false; }

            _maskMat = new Material(mask) { hideFlags = HideFlags.HideAndDontSave };
            _sealMat = new Material(seal) { hideFlags = HideFlags.HideAndDontSave };

            // Give each portal its own view material with RT pre-assigned
            foreach (var portal in portals)
                portal?.SetViewShader(view);

            _materialsReady = true;
            return true;
        }

        public void Setup(IReadOnlyList<Portal> portals) => _portals = portals;

        public void Cleanup()
        {
            CoreUtils.Destroy(_maskMat);
            CoreUtils.Destroy(_viewMat);
            CoreUtils.Destroy(_sealMat);
            _maskMat = null; _viewMat = null; _sealMat = null;
            _materialsReady = false;
        }

        // RenderGraph path — used when compatibility mode is OFF (default in URP 17+)
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_portals == null || _portals.Count == 0) return;
            if (!_materialsReady) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddUnsafePass<PassData>("Portal Stencil Render", out var passData))
            {
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                passData.portals   = _portals;
                passData.maskMat   = _maskMat;
                passData.sealMat   = _sealMat;
                passData.mainTexId = MainTexId;

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = ctx.cmd;

                    foreach (var portal in data.portals)
                    {
                        if (portal == null || !portal.IsReady) continue;
                        if (!portal.ShouldRender()) continue;

                        Mesh      mesh   = portal.PortalMesh;
                        Matrix4x4 matrix = portal.transform.localToWorldMatrix;

                        if (mesh == null || portal.ViewMaterial == null) continue;

                        cmd.DrawMesh(mesh, matrix, data.maskMat, 0, 0);
                        cmd.DrawProcedural(Matrix4x4.identity, portal.ViewMaterial, 0,
                                           MeshTopology.Triangles, 3);
                        cmd.DrawMesh(mesh, matrix, data.sealMat, 0, 0);
                        cmd.DrawMesh(mesh, matrix, data.maskMat, 0, 1);
                    }
                });
            }
        }

        // Fallback Execute for compatibility mode — not normally called when
        // RenderGraph is active, but prevents a warning if someone enables it.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
    }

    // Data bag passed into the RenderGraph lambda — must be a class, not a struct,
    // so reference types can be stored and accessed inside SetRenderFunc.
    private class PassData
    {
        public IReadOnlyList<Portal> portals;
        public Material              maskMat;
        public Material              sealMat;
        public int                   mainTexId;
    }
}