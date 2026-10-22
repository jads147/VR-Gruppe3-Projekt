Shader "Custom/PortalSkybox"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [IntRange] _StencilID ("Stencil ID", Range(0,255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            // Rendered from inside the sphere, behind everything else.
            Cull Front
            ZWrite Off
            ZTest LEqual

            // Only draw where the portal wrote its stencil value.
            Stencil
            {
                Ref [_StencilID]
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            TEXTURECUBE(_Tex);
            SAMPLER(sampler_Tex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tex_HDR;
                half4 _Tint;
                half _Exposure;
                float _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundY(float3 v, float degrees)
            {
                float rad = radians(degrees);
                float s, c;
                sincos(rad, s, c);
                return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Like a real skybox: centered on the eye, so the sky never moves with the head.
                // Only the object's rotation is applied, so the sky turns along with its parent (e.g. the level).
                float3 dir = normalize(IN.positionOS.xyz);
                float3 dirWS = normalize(TransformObjectToWorldDir(dir));
                OUT.positionCS = TransformWorldToHClip(GetCameraPositionWS() + dirWS);

                // Push the depth onto the far plane, so every object in front wins the depth test.
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = OUT.positionCS.w * 1e-5;
                #else
                    OUT.positionCS.z = OUT.positionCS.w * (1.0 - 1e-5);
                #endif

                OUT.direction = RotateAroundY(dir, _Rotation);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, IN.direction);
                half3 color = DecodeHDREnvironment(tex, _Tex_HDR);
                color *= _Tint.rgb * _Exposure;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
