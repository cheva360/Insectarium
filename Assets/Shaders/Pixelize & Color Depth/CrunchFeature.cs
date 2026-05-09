using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class CrunchRenderFeature : ScriptableRendererFeature
{
    class CrunchPass : ScriptableRenderPass
    {
        PixelationVolumeComponent pixelation;
        private int width;
        private int height;
        
        static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
        static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
        static readonly int ColorPrecision = Shader.PropertyToID("_ColorPrecision");

        public void Setup(ref PixelationSettings settings, RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;

            width = settings.finalWidth;
            height = settings.finalHeight;
            
            requiresIntermediateTexture = true;
            
            // material.SetFloat(WidthPixelation, settings.widthPixelation);
            // material.SetFloat(HeightPixelation, settings.heightPixelation);
            // material.SetFloat(ColorPrecision, settings.colorPrecision);
        }

        private class PassData
        {
            public TextureHandle texToWriteTo;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("crunchRenderFeature requires intermediate texture");
            }

            var source = resourceData.activeColorTexture;

            var colorDesc = renderGraph.GetTextureDesc(source);
            //TextureDesc color_desc = resourceData.cameraDepthTexture.GetDescriptor(renderGraph);
            colorDesc.clearBuffer = false;
            colorDesc.width = width;
            colorDesc.height = height;
            colorDesc.format = GraphicsFormat.R5G5B5A1_UNormPack16;
            TextureHandle colorDestination = renderGraph.CreateTexture(colorDesc);

            var depthSource = resourceData.activeDepthTexture;
            var depthDesc = renderGraph.GetTextureDesc(depthSource);
            depthDesc.clearBuffer = false;
            depthDesc.width = width;
            depthDesc.height = height;
            TextureHandle depthDestination = renderGraph.CreateTexture(depthDesc);
            
            resourceData.cameraColor = colorDestination;
            resourceData.cameraDepth = depthDestination;
            //resourceData.cameraDepth = 
            // builder.SetRenderAttachment(destination,);     
            //using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelize + color", out var passData))
            //{
            //    
            //    
            //    builder.SetRenderFunc(
            //       static (PassData passData, RasterGraphContext context) => ExecutePass(passData, context) 
            //    );
            //}
        }

        static void ExecutePass(PassData passData, RasterGraphContext context)
        {
            
        }
    }

    [Serializable]
    public class PixelationSettings
    {
        public int finalWidth = 512;
        public int finalHeight = 512;
        // public float colorPrecision = 32.0f;
    }

    [SerializeField]
    PixelationSettings settings;
    CrunchPass pixelPass;

    public RenderPassEvent pixelizePassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    public override void Create()
    {
        pixelPass ??= new CrunchPass();
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        pixelPass.Setup(ref settings, pixelizePassEvent);

        // renderingData.cameraData.camera.depthTextureMode =
        //    renderingData.cameraData.camera.depthTextureMode | DepthTextureMode.Depth;
        
        renderer.EnqueuePass(pixelPass);
    }
}



