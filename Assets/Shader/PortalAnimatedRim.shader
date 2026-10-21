Shader "Custom/Portal Animated Rim"
{
    Properties
    {
        [Enum(Circle or Oval, 0, Square or Rectangle, 1)] _ShapeMode ("Shape", Float) = 0
        _ShapeSize ("Portal Shape Size", Range(0.5, 1.0)) = 0.85
        [HDR] _Color ("Rim Color", Color) = (0.1, 0.65, 1.0, 1.0)
        _EdgeWidth ("Rim Width", Range(0.01, 0.5)) = 0.16
        _EdgeSoftness ("Inner Edge Softness", Range(0.001, 0.2)) = 0.035
        _OuterEdgeSoftness ("Outer Edge Softness", Range(0.001, 0.4)) = 0.14
        _OuterWaveStrength ("Outer Edge Wave Strength", Range(0, 0.2)) = 0.07
        _WaveStrength ("Wave Strength", Range(0, 0.15)) = 0.035
        _WaveFrequency ("Wave Frequency", Range(1, 32)) = 12
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 2
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ShapeMode;
                float _ShapeSize;
                half4 _Color;
                float _EdgeWidth;
                float _EdgeSoftness;
                float _OuterEdgeSoftness;
                float _OuterWaveStrength;
                float _WaveStrength;
                float _WaveFrequency;
                float _WaveSpeed;
                float _PulseSpeed;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Assumes a circular or oval mesh whose UVs run from 0 to 1.
                float2 centeredUV = input.uv * 2.0 - 1.0;
                float radius = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);
                float time = _Time.y * _WaveSpeed;

                // Use whole-number harmonics so the wave joins seamlessly after a full turn.
                float waveFrequency = floor(_WaveFrequency + 0.5);
                float wave = sin(angle * waveFrequency + time)
                           + 0.5 * sin(angle * (waveFrequency * 2.0) - time * 1.4);
                // Circle uses radial distance; rectangle uses the nearest UV edge.
                float rectangleEdgeDistance = 1.0 - 2.0 * min(
                    min(input.uv.x, 1.0 - input.uv.x),
                    min(input.uv.y, 1.0 - input.uv.y));
                float shapeSize = max(_ShapeSize, 0.001);
                float edgeDistance = lerp(radius, rectangleEdgeDistance, step(0.5, _ShapeMode)) / shapeSize;
                float innerEdge = 1.0 - _EdgeWidth + wave * _WaveStrength;
                float softness = max(_EdgeSoftness, 0.001);
                float outerSoftness = max(_OuterEdgeSoftness, 0.001);

                // The alpha is zero inside the ring, so the portal underneath stays visible.
                float rimAlpha = smoothstep(innerEdge - softness, innerEdge + softness, edgeDistance);
                // Fade to transparent inside the shape so an oversized mesh cannot cut off the fade.
                float circleShapeAlpha = 1.0 - smoothstep(
                    1.0 - outerSoftness,
                    1.0,
                    radius / shapeSize - wave * _OuterWaveStrength);
                float rectangleShapeAlpha = 1.0 - smoothstep(
                    1.0 - outerSoftness,
                    1.0,
                    rectangleEdgeDistance / shapeSize - wave * _OuterWaveStrength);
                float shapeAlpha = lerp(circleShapeAlpha, rectangleShapeAlpha, step(0.5, _ShapeMode));
                float pulse = 0.82 + 0.18 * sin(_Time.y * _PulseSpeed);

                half4 color = _Color;
                color.rgb *= pulse;
                color.a *= rimAlpha * shapeAlpha * _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
