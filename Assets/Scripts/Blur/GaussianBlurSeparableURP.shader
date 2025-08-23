Shader "Hidden/GaussianBlurSeparableURP"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}

        // Separable Gaussian controls
        _Radius ("Kernel Radius (1-12)", Range(1,12)) = 6

        // Depth-gradient blur (Near -> Far)
        _SigmaNear ("Sigma Near", Range(0.1,10)) = 1.0
        _SigmaFar  ("Sigma Far",  Range(0.1,20)) = 6.0

        // Map linear eye depth to [0..1]
        _DepthNear  ("Depth Near (eye units)", Float) = 2.0
        _DepthFar   ("Depth Far (eye units)",  Float) = 25.0
        _DepthGamma ("Depth Curve (gamma)",    Range(0.2,4)) = 1.0

        // Direction set per pass by the feature: (1,0)=H, (0,1)=V
        _Direction ("Direction", Vector) = (1,0,0,0)

        // Optional switch: 0 = use depth-gradient (recommended)
        //                 1 = focus band mode (classic DoF)
        _UseFocusMode ("Use Focus Mode (0=Gradient,1=Focus)", Float) = 0

        // Focus-band parameters (only if _UseFocusMode=1)
        _FocusDistance ("Focus Distance (eye)", Float) = 10
        _FocusRange    ("Focus Range", Float)    = 5
        _DepthStrength ("Depth Influence", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
        ZTest Always Cull Off ZWrite Off

        Pass
        {
            Name "GaussianBlurSeparableURP"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Source texture (set by Feature/Blitter)
            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // x=1/w, y=1/h

            // Uniforms
            int   _Radius;
            float2 _Direction;

            // Depth-gradient params
            float _SigmaNear, _SigmaFar;
            float _DepthNear, _DepthFar, _DepthGamma;

            // Focus-mode params (optional)
            float _UseFocusMode;
            float _FocusDistance, _FocusRange, _DepthStrength;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            inline float Gaussian(float x, float sigma)
            {
                float s2 = sigma * sigma;
                return exp(- (x * x) / (2.0 * s2));
            }

            float ComputeSigma(float2 uv)
            {
                // Sample raw hardware depth
                float raw = SampleSceneDepth(uv);
                // Convert to linear eye depth (world units along view dir)
                float eyeDepth = LinearEyeDepth(raw, _ZBufferParams);

                // Mode 0: depth-gradient (near->far)
                if (_UseFocusMode < 0.5)
                {
                    // Map eyeDepth into [0..1] across [_DepthNear, _DepthFar]
                    float t = saturate( (eyeDepth - _DepthNear) / max(1e-4, (_DepthFar - _DepthNear)) );
                    // Shape with gamma
                    t = pow(t, _DepthGamma);
                    // Lerp sigma
                    return lerp(_SigmaNear, _SigmaFar, t);
                }
                // Mode 1: focus band (classic DoF-like)
                else
                {
                    float distFromFocus = abs(eyeDepth - _FocusDistance);
                    float t = saturate(distFromFocus / max(1e-4, _FocusRange));
                    // Start from SigmaNear as baseline
                    return lerp(_SigmaNear, _SigmaNear + (_SigmaFar - _SigmaNear), t * _DepthStrength);
                }
            }

            float4 Frag (Varyings i) : SV_Target
            {
                float sigma = ComputeSigma(i.uv);
                float2 texel = _MainTex_TexelSize.xy * _Direction;

                float4 color = 0;
                float  weightSum = 0;

                // center
                float w0 = Gaussian(0, sigma);
                color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv) * w0;
                weightSum += w0;

                // wings
                [unroll(24)]
                for (int k = 1; k <= 12; k++)
                {
                    if (k > _Radius) break;
                    float w = Gaussian(k, sigma);
                    float2 off = texel * k;

                    color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv + off) * w;
                    color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv - off) * w;
                    weightSum += (w * 2.0);
                }

                return color / max(1e-5, weightSum);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
