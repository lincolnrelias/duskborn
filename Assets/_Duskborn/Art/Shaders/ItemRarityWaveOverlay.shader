Shader "Duskborn/Loot/ItemRarityWaveOverlay"
{
    Properties
    {
        _Color ("Rarity Color", Color) = (0.18, 0.88, 0.35, 1)
        _OutlineColor ("Outline Color", Color) = (0.2, 1.0, 0.4, 1)
        _OutlineWidth ("Outline Width", Range(0.005, 0.08)) = 0.022
        _WaveSpeed ("Wave Speed", Float) = 2.4
        _WaveFrequency ("Wave Frequency", Float) = 4.0
        _WaveGlow ("Wave Glow Multiplier", Float) = 1.6
        _FresnelPower ("Fresnel Power", Float) = 2.5
        _RimIntensity ("Rim Intensity", Float) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // ── Pass 1: Inverted Hull Rarity Outline ─────────────────────────────
        Pass
        {
            Name "RarityOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _WaveSpeed;
                float  _WaveFrequency;
                float  _WaveGlow;
                float  _FresnelPower;
                float  _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Extrusão uniforme dos vértices ao longo das normais para silhueta exterior nítida
                float3 extrudedPos = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extrudedPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 col = _OutlineColor.rgb * _Color.rgb * 1.2;
                return half4(col, 0.85);
            }
            ENDHLSL
        }

        // ── Pass 2: Animated Wave Gradient & Rim Glow ────────────────────────
        Pass
        {
            Name "WaveGradientOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One // Additive overlay over base model texture

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : NORMAL;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _WaveSpeed;
                float  _WaveFrequency;
                float  _WaveGlow;
                float  _FresnelPower;
                float  _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Onda primária senoidal animada em gradiente ascendente
                float timeVal = _Time.y * _WaveSpeed;
                float wavePhase1 = (input.positionOS.y * _WaveFrequency - timeVal);
                float wave1 = sin(wavePhase1 * 6.2831853) * 0.5 + 0.5;
                wave1 = pow(wave1, 3.5);

                // 2. Onda harmônica secundária para dar riqueza orgânica e fluxo contínuo
                float wavePhase2 = (input.positionOS.y * (_WaveFrequency * 1.5) - timeVal * 1.3 + 0.35);
                float wave2 = sin(wavePhase2 * 6.2831853) * 0.5 + 0.5;
                wave2 = pow(wave2, 4.0) * 0.45;

                float totalWave = saturate(wave1 + wave2);

                // 3. Efeito Fresnel nos contornos do modelo 3D
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float NdotV = saturate(dot(normalize(input.normalWS), viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // 4. Composição aditiva: gradiente da onda na cor do tier + brilho esbranquiçado na crista
                half3 waveColor = _Color.rgb * (totalWave * _WaveGlow);
                half3 rimColor  = _Color.rgb * (fresnel * _RimIntensity);
                half3 crestHotspot = half3(1.0, 1.0, 1.0) * (pow(wave1, 6.0) * 0.6);

                half3 finalColor = waveColor + rimColor + crestHotspot;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
