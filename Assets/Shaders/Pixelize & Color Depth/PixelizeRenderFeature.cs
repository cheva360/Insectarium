using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class PixelizeRenderFeature : ScriptableRendererFeature
{
    class PixelationPass : ScriptableRenderPass
    {
        PixelationVolumeComponent pixelation;
        Material material;
        RenderTargetIdentifier currentTarget;

        static readonly int Pixelation = Shader.PropertyToID("_Pixelation");
        static readonly int ColorPrecision = Shader.PropertyToID("_ColorPrecision");

        public void Setup(ref PixelationSettings settings, ref Material pixelizeMaterial)
        {
            renderPassEvent = settings.pixelizePassEvent;

            material = pixelizeMaterial;

            requiresIntermediateTexture = true;

            material.SetInteger(Pixelation, settings.pixelation);
            material.SetInteger(ColorPrecision, settings.colorPrecision);
        }
        private class PassData { }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var stack = VolumeManager.instance.stack;
            pixelation = stack.GetComponent<PixelationVolumeComponent>();
            if (!pixelation.IsActive())
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("pixelizeRenderFeature requires intermediate texture");
            }

            var source = resourceData.activeColorTexture;

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, material, 0);
            renderGraph.AddBlitPass(para, passName: "Pixelation Pass");

            resourceData.cameraColor = destination;
        }
    }

    class DitherPass : ScriptableRenderPass
    {
        Material material;


        static readonly int Steps = Shader.PropertyToID("_Steps");
        static readonly int RenderScale = Shader.PropertyToID("_RenderScale");

        public void Setup(ref DitherSettings settings, ref Material ditherMaterial)
        {
            renderPassEvent = settings.ditherPassEvent;

            material = ditherMaterial;

            requiresIntermediateTexture = true;

            material.SetInteger(Steps, settings.colorPrecision);
            material.SetFloat(RenderScale, settings.renderScale);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var stack = VolumeManager.instance.stack;
            var pixelation = stack.GetComponent<PixelationVolumeComponent>();
            if (!pixelation.IsActive())
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("ditherRenderFeature requires intermediate texture");
            }

            var source = resourceData.activeColorTexture;

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, material, 0);
            renderGraph.AddBlitPass(para, passName: "Dither Pass");

            resourceData.cameraColor = destination;
        }
    }

    [Serializable]
    public class PixelationSettings
    {
        public bool pixelize = true;
        public int pixelation = 512;
        public int colorPrecision = 32;

        public RenderPassEvent pixelizePassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }
    [Serializable]
    public class DitherSettings
    {
        public bool dither = true;
        public int colorPrecision = 16;
        public float renderScale = 1.0f;

        public RenderPassEvent ditherPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    [SerializeField]
    PixelationSettings pixelSettings;
    [SerializeField]
    DitherSettings ditherSettings;

    PixelationPass pixelPass;
    Material pixelationMaterial;

    private DitherPass ditherPass;
    private Material ditherMaterial;

    [SerializeField]
    private Shader pixelShader;
    [SerializeField]
    private Shader ditherShader;


    public override void Create()
    {
        pixelPass ??= new PixelationPass();
        ditherPass ??= new DitherPass();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Skip portal cameras was breaking it
        if (renderingData.cameraData.camera.GetComponent<PortalCameraMarker>() != null)
            return;

        if (pixelationMaterial == null || ditherMaterial == null)
        {
            if (pixelShader == null || ditherShader == null)
            {
                Debug.LogError("Shaders not assigned in PixelizeRenderFeature");
                return;
            }

            pixelationMaterial = CoreUtils.CreateEngineMaterial(pixelShader);
            ditherMaterial = CoreUtils.CreateEngineMaterial(ditherShader);
            if (pixelationMaterial == null || ditherMaterial == null)
            {
                Debug.LogWarning("Not all required materials could be created. Outlines will not render.");
                return;
            }
        }

        pixelPass.Setup(ref pixelSettings, ref pixelationMaterial);
        ditherPass.Setup(ref ditherSettings, ref ditherMaterial);

        if (pixelSettings.pixelize)
            renderer.EnqueuePass(pixelPass);
        if (ditherSettings.dither)
            renderer.EnqueuePass(ditherPass);
    }
}





