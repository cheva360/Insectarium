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
        
        static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
        static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
        static readonly int ColorPrecision = Shader.PropertyToID("_ColorPrecision");

        public void Setup(ref PixelationSettings settings, ref Material pixelizeMaterial)
        {
            renderPassEvent = settings.pixelizePassEvent;

            material = pixelizeMaterial;
            
            requiresIntermediateTexture = true;
            
            material.SetFloat(WidthPixelation, settings.widthPixelation);
            material.SetFloat(HeightPixelation, settings.heightPixelation);
            material.SetFloat(ColorPrecision, settings.colorPrecision);
        }
        private class PassData{}

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
            
            material.SetInteger(Steps, settings.steps);
            material.SetFloat(RenderScale, settings.render_scale);
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
        public float widthPixelation = 512;
        public float heightPixelation = 512;
        public float colorPrecision = 32.0f;
        
        public RenderPassEvent pixelizePassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }
    [Serializable]
    public class DitherSettings
    {
        public bool dither = true;
        public int steps = 16;
        public float render_scale = 1.0f;
        
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


    public override void Create()
    {
        pixelPass ??= new PixelationPass();
        ditherPass ??= new DitherPass();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        if (pixelationMaterial == null || ditherMaterial == null)
        {
            var pixel_shader = Shader.Find("Custom/Pixelation");
            if (pixel_shader == null)
            {
                Debug.LogError("Pixelation shader not found");
                return;
            }

            var dither_shader = Shader.Find("Custom/Dithering");
            if (dither_shader == null)
            {
                Debug.LogError("Dither shader not found");
                return;
            }
            
            pixelationMaterial = CoreUtils.CreateEngineMaterial(pixel_shader);
            ditherMaterial = CoreUtils.CreateEngineMaterial(dither_shader);
            if (pixelationMaterial ==null || ditherMaterial == null)
            {
                Debug.LogWarning("Not all required materials could be created. Outlines will not render.");
                return;
            }
        }
        
        pixelPass.Setup(ref pixelSettings, ref pixelationMaterial);
        ditherPass.Setup(ref ditherSettings, ref ditherMaterial);

        // renderingData.cameraData.camera.depthTextureMode =
        //    renderingData.cameraData.camera.depthTextureMode | DepthTextureMode.Depth;
        
        
        if (pixelSettings.pixelize)
            renderer.EnqueuePass(pixelPass);
        if (ditherSettings.dither)
            renderer.EnqueuePass(ditherPass);
        
    }
}



