Shader "Duskborn/VFX/DiabloGroundFlare"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _HotspotIntensity ("Hotspot Intensity", Float) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+40"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "DiabloGroundFlarePass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _PulseSpeed;
                float  _HotspotIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = input.uv;
                output.color      = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Distância radial ao centro do disco (0 no centro, 1 na borda)
                float2 centerOffset = input.uv - float2(0.5, 0.5);
                float dist = length(centerOffset) * 2.0;

                if (dist >= 1.0)
                    return half4(0, 0, 0, 0);

                // 2. Núcleo de impacto central (hotspot brilhante no ponto de contato)
                float hotspot = exp(-dist * dist * 6.5) * _HotspotIntensity;

                // 3. Poça difusa de luz ao redor
                float diffuseGlow = pow(saturate(1.0 - dist), 2.2) * 0.45;

                // 4. Ondulação concêntrica sutil pulsante
                float ripple = sin((dist * 7.0 - _Time.y * _PulseSpeed) * 6.2831853) * 0.08 * (1.0 - dist);

                float combinedIntensity = (hotspot + diffuseGlow + ripple) * input.color.a;

                // Centro ligeiramente mais branco / quente
                float whiteCenter = pow(saturate(1.0 - dist * 2.0), 3.0) * 0.6;
                half3 finalColor = lerp(input.color.rgb, half3(1.0, 1.0, 1.0), whiteCenter);

                return half4(finalColor * combinedIntensity, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
