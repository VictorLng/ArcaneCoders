Shader "ArcaneCode/WorldProp"
{
    Properties { _BaseColor ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                // Fixed presentation lighting: works with the existing 2D renderer and lights.
                output.shade=.48h+.52h*saturate(dot(normal,normalize(float3(-.4,.7,-1))));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            { return half4(_BaseColor.rgb*input.shade,_BaseColor.a); }
            ENDHLSL
        }
    }
}
