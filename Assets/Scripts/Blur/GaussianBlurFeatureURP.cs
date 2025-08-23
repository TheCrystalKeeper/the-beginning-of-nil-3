// Assets/Scripts/URP/GaussianBlurFeatureURP.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GaussianBlurFeatureURP : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        // Assign a material that uses "Hidden/GaussianBlurSeparableURP"
        public Material blurMaterial;
        [Range(1, 12)] public int radius = 6;
        [Range(0.5f, 10f)] public float sigma = 3f;

        [Header("Optional Depth Modulation")]
        [Range(0f, 1f)] public float depthStrength = 0f;
        public float focusDistance = 10f;
        public float focusRange = 5f;

        [Header("Performance")]
        // 0 = full, 1 = half, 2 = quarter
        [Range(0, 2)] public int downsample = 1;

        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    class BlurPass : ScriptableRenderPass
    {
        private readonly Settings settings;

        // RTHandles
        private RTHandle source;
        private RTHandle temp1;
        private RTHandle temp2;

        public BlurPass(Settings s)
        {
            settings = s;
            renderPassEvent = s.passEvent;
        }

        public void Setup(RTHandle src)
        {
            source = src;
        }

        // Unity asks us to mark overrides obsolete when the base is obsolete
        [System.Obsolete("URP compatibility path (no Render Graph).")]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            int ds = Mathf.Clamp(settings.downsample, 0, 2);
            desc.width = Mathf.Max(1, desc.width >> ds);
            desc.height = Mathf.Max(1, desc.height >> ds);

            // Allocate temps (new API on 2023.3+)
#if UNITY_2023_3_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref temp1, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GB_Temp1");
            RenderingUtils.ReAllocateHandleIfNeeded(ref temp2, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GB_Temp2");
#else
#pragma warning disable CS0618
            RenderingUtils.ReAllocateIfNeeded(ref temp1, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 1, 0.0f, "_GB_Temp1");
            RenderingUtils.ReAllocateIfNeeded(ref temp2, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 1, 0.0f, "_GB_Temp2");
#pragma warning restore CS0618
#endif
        }

        [System.Obsolete("URP compatibility path (no Render Graph).")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.blurMaterial == null)
                return;

            var cmd = CommandBufferPool.Get("Gaussian Blur (URP RTHandles)");

            // Shared uniforms
            settings.blurMaterial.SetInt("_Radius", settings.radius);
            settings.blurMaterial.SetFloat("_Sigma", settings.sigma);
            settings.blurMaterial.SetFloat("_DepthStrength", settings.depthStrength);
            settings.blurMaterial.SetFloat("_FocusDistance", settings.focusDistance);
            settings.blurMaterial.SetFloat("_FocusRange", Mathf.Max(0.0001f, settings.focusRange));

            // Downsample copy: source -> temp1
            Blitter.BlitCameraTexture(cmd, source, temp1);

            // Horizontal
            settings.blurMaterial.SetVector("_Direction", new Vector2(1f, 0f));
            Blitter.BlitCameraTexture(cmd, temp1, temp2, settings.blurMaterial, 0);

            // Vertical
            settings.blurMaterial.SetVector("_Direction", new Vector2(0f, 1f));
            Blitter.BlitCameraTexture(cmd, temp2, temp1, settings.blurMaterial, 0);

            // Upsample back to source
            Blitter.BlitCameraTexture(cmd, temp1, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            temp1?.Release();
            temp2?.Release();
        }
    }

    public Settings settings = new Settings();
    private BlurPass pass;

    public override void Create()
    {
        pass = new BlurPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blurMaterial == null)
            return;

        pass.renderPassEvent = settings.passEvent;

        // This property is flagged obsolete when Render Graph is disabled.
        // Suppress just this use; it's the correct handle in compatibility mode.
#pragma warning disable CS0618
        pass.Setup(renderer.cameraColorTargetHandle);
#pragma warning restore CS0618

        renderer.EnqueuePass(pass);
    }
}
