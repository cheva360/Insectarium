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
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            material = pixelizeMaterial;
            
            requiresIntermediateTexture = true;
            
            material.SetFloat(WidthPixelation, settings.widthPixelation);
            material.SetFloat(HeightPixelation, settings.heightPixelation);
            material.SetFloat(ColorPrecision, settings.colorPrecision);
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData{}

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
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

    [Serializable]
    public class PixelationSettings
    {
        public float widthPixelation = 512;
        public float heightPixelation = 512;
        public float colorPrecision = 32.0f;
    }

    [SerializeField]
    PixelationSettings settings;
    PixelationPass pixelPass;
    Material pixelationMaterial;

    public override void Create()
    {
        pixelPass ??= new PixelationPass();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        if (pixelationMaterial == null)
        {
            var shader = Shader.Find("Custom/Pixelation");
            if (shader == null)
            {
                Debug.LogError("Shader not found");
                return;
            }
            
            pixelationMaterial = CoreUtils.CreateEngineMaterial(shader);
            if (pixelationMaterial ==null)
            {
                Debug.LogWarning("Not all required materials could be created. Outlines will not render.");
                return;
            }
        }
        
        pixelPass.Setup(ref settings, ref pixelationMaterial);

        // renderingData.cameraData.camera.depthTextureMode =
        //    renderingData.cameraData.camera.depthTextureMode | DepthTextureMode.Depth;
        
        renderer.EnqueuePass(pixelPass);
    }
}



