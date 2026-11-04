Shader "Janitor/URP Object Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidthPixels ("Outline Width (render pixels)", Range(0.5,6)) = 2
        _PulseFrequency ("Pulse Frequency", Float) = 1
        _PulseAmount ("Pulse Amount", Range(0,0.8)) = 0
        [HideInInspector] _SurfaceMode ("Surface Ring", Float) = 0
        [HideInInspector] _OutlineCull ("Cull", Float) = 1
        [HideInInspector] _OutlineDepthBias ("Depth Bias", Float) = 0
        [HideInInspector] _OutlineZTest ("Depth Test", Float) = 4
        [HideInInspector] _OutlineColorMask ("Color Mask", Float) = 15
        [HideInInspector] _OutlineStencilRef ("Stencil Reference", Float) = 0
        [HideInInspector] _OutlineStencilReadMask ("Stencil Read Mask", Float) = 0
        [HideInInspector] _OutlineStencilWriteMask ("Stencil Write Mask", Float) = 0
        [HideInInspector] _OutlineStencilComp ("Stencil Compare", Float) = 8
        [HideInInspector] _OutlineStencilPass ("Stencil Pass", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Name "ObjectOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_OutlineCull]
            ZTest [_OutlineZTest]
            ZWrite Off
            ColorMask [_OutlineColorMask]
            Stencil
            {
                Ref [_OutlineStencilRef]
                ReadMask [_OutlineStencilReadMask]
                WriteMask [_OutlineStencilWriteMask]
                Comp [_OutlineStencilComp]
                Pass [_OutlineStencilPass]
            }
            Offset [_OutlineDepthBias], [_OutlineDepthBias]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidthPixels;
                float _PulseFrequency;
                float _PulseAmount;
                float _SurfaceMode;
                float _OutlineCull;
                float _OutlineDepthBias;
                float _OutlineZTest;
                float _OutlineColorMask;
                float _OutlineStencilRef;
                float _OutlineStencilReadMask;
                float _OutlineStencilWriteMask;
                float _OutlineStencilComp;
                float _OutlineStencilPass;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 direction = normalVS.xy * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * _ScaledScreenParams.xy;
                direction *= rsqrt(max(dot(direction, direction), 0.000001));
                // Closed hull: expand every vertex. Flat surface: expand only the outer
                // perimeter copy, leaving a genuinely hollow center with no alpha-mask sampling.
                float expand = lerp(1.0, input.uv.x, _SurfaceMode);
                output.positionCS.xy += direction * (_OutlineWidthPixels * 2.0 / _ScaledScreenParams.xy) * output.positionCS.w * expand;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half pulse = 1.0h - _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseFrequency * 6.2831853));
                return half4(_OutlineColor.rgb, _OutlineColor.a * pulse);
            }
            ENDHLSL
        }
    }
}
