Shader "Duskborn/StylizedFoliage"
{
    Properties
    {
        [Header(Foliage Colors and Root Blending)]
        _RootColor("Root / Base Foliage Color", Color) = (0.18, 0.42, 0.16, 1.0)
        _TipColor("Tip / Upper Foliage Color", Color) = (0.42, 0.78, 0.28, 1.0)
        _TerrainBlendHeight("Terrain Blend Height (UV.y)", Range(0.0, 1.0)) = 0.35
        _TerrainBlendStrength("Terrain Blend Strength", Range(0.0, 1.0)) = 0.85
        _BaseMap("Texture Map (Optional)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff (0 = Opaque)", Range(0.0, 1.0)) = 0.0

        [Header(Wind Animation)]
        _WindDirection("Wind Direction (XZ)", Vector) = (1.0, 0.35, 0.0, 0.0)
        _WindSpeed("Wind Speed", Range(0.1, 5.0)) = 1.6
        _WindStrength("Wind Sway Amplitude", Range(0.0, 1.5)) = 0.28
        _WindFrequency("Gust Wave Spatial Frequency", Range(0.05, 2.0)) = 0.38
        _WindFlutterSpeed("Leaf Flutter Frequency", Range(1.0, 20.0)) = 6.5
        _WindFlutterStrength("Leaf Flutter Micro-Shake", Range(0.0, 0.3)) = 0.06

        [Header(Subsurface Scattering Backlight)]
        _SSSColor("Subsurface Scatter Tint", Color) = (0.65, 0.95, 0.32, 1.0)
        _SSSIntensity("SSS Backlight Intensity", Range(0.0, 2.5)) = 1.15
        _SSSPower("SSS Directional Sharpness", Range(1.0, 8.0)) = 2.8

        [Header(Cel Shading and Lighting)]
        _CelCutoff("Cel Shadow Cutoff", Range(0.0, 1.0)) = 0.42
        _CelSmoothness("Cel Smoothness", Range(0.01, 0.4)) = 0.08
        _ShadowTint("Shadow Color Tint", Color) = (0.38, 0.42, 0.54, 1.0)
        _SunlightBoost("Sunlight Intensity Boost", Range(0.5, 2.5)) = 1.15
        _RimIntensity("Rim Light Intensity", Range(0.0, 1.0)) = 0.22
        _RimPower("Rim Light Power", Range(1.0, 8.0)) = 4.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        Cull Off
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _RootColor;
            half4  _TipColor;
            float  _TerrainBlendHeight;
            float  _TerrainBlendStrength;
            float4 _BaseMap_ST;
            float  _Cutoff;

            float4 _WindDirection;
            float  _WindSpeed;
            float  _WindStrength;
            float  _WindFrequency;
            float  _WindFlutterSpeed;
            float  _WindFlutterStrength;

            half4  _SSSColor;
            float  _SSSIntensity;
            float  _SSSPower;

            float  _CelCutoff;
            float  _CelSmoothness;
            half4  _ShadowTint;
            float  _SunlightBoost;
            float  _RimIntensity;
            float  _RimPower;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Deslocamento de vértice por vento com ancoragem no solo
        float3 ApplyFoliageWind(float3 positionWS, float anchorWeight)
        {
            float anchor = saturate(anchorWeight);
            if (anchor < 0.001) return positionWS;

            float2 windDir = normalize(_WindDirection.xy);
            float t = _Time.y * _WindSpeed;

            // 1. Rajada de Vento Harmônica (Gusts)
            float gustCoord = dot(positionWS.xz, windDir) * _WindFrequency - t;
            float gust = sin(gustCoord) * 0.72 + sin(gustCoord * 1.85 + 1.3) * 0.28;
            float gustEnvelope = pow(sin(gustCoord * 0.4) * 0.5 + 0.5, 2.0);
            float totalGust = (gust + gustEnvelope * 1.3) * _WindStrength;

            // 2. Tremor / Fluttering de Alta Frequência nas Folhas e Pontas
            float flutterPhase = (positionWS.x * 2.1 + positionWS.y * 3.4 + positionWS.z * 1.8) + _Time.y * _WindFlutterSpeed;
            float flutter = sin(flutterPhase) * _WindFlutterStrength;

            // 3. Deslocamento com curvatura orgânica e conservação de volume
            float displacement = (totalGust + flutter) * anchor;
            positionWS.xz += windDir * displacement;
            positionWS.y -= abs(displacement) * 0.16;

            return positionWS;
        }
        ENDHLSL

        // -------------------------------------------------------------
        // Pass 1: ForwardLit
        // -------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float4 color        : COLOR;
                float  fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                // Ancoragem do vento: usa canal alpha da cor do vértice se disponível, senão uv.y
                float windWeight = input.color.a > 0.01 ? input.color.a : input.uv.y;
                posWS = ApplyFoliageWind(posWS, windWeight);

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS   = normWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color      = input.color;
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Textura albedo opcional com recorte alfa
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (_Cutoff > 0.001)
                {
                    clip(texColor.a - _Cutoff);
                }

                // Inverte a normal caso a face seja traseira (Two-Sided foliage)
                float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);

                // 1. Gradiente Base -> Ponta
                half3 foliageBase = lerp(_RootColor.rgb, _TipColor.rgb, saturate(input.uv.y)) * texColor.rgb;

                // 2. Mescla Orgânica de Cor com o Terreno no Pé da Planta (elimina corte brusco)
                if (_TerrainBlendStrength > 0.01)
                {
                    // Usa input.color.rgb (cor do vértice do terreno bakeada no mesh ou enviada pelo placer)
                    half3 terrainColor = input.color.rgb;
                    float blendRatio = saturate(1.0 - input.uv.y / max(0.01, _TerrainBlendHeight)) * _TerrainBlendStrength;
                    foliageBase = lerp(foliageBase, terrainColor, blendRatio);
                }

                // 3. Iluminação Cel-Shaded com Sombras Suaves
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = dot(normalWS, mainLight.direction);
                half halfLambert = NdotL * 0.5 + 0.5;
                half celDiffuse = smoothstep(_CelCutoff - _CelSmoothness, _CelCutoff + _CelSmoothness, halfLambert);

                // Atenuação de sombras projetadas suave (preserva penumbra suave do URP)
                half shadowSoft = smoothstep(0.0, 1.0, mainLight.shadowAttenuation);
                half lightFactor = celDiffuse * shadowSoft;

                half3 ambientSH = SampleSH(normalWS);
                half3 shadowColor = _ShadowTint.rgb * (ambientSH + half3(0.25, 0.25, 0.30));
                half3 litLight = mainLight.color * _SunlightBoost;
                half3 directLight = lerp(shadowColor, litLight, lightFactor);

                // 4. Subsurface Scattering (Translucidez da Luz Atravessando a Folhagem)
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                half backlight = saturate(dot(viewDirWS, -mainLight.direction));
                half sssVal = pow(backlight, _SSSPower) * _SSSIntensity * saturate(input.uv.y * 1.4);
                half3 sssHighlight = _SSSColor.rgb * (sssVal * mainLight.color * shadowSoft);

                // 5. Rim Light Estilizado na Borda (apenas em áreas expostas à luz)
                half NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                half rim = pow(NdotV, _RimPower) * _RimIntensity * lightFactor;

                half3 ambient = ambientSH * 0.40;
                half3 finalColor = foliageBase * (directLight + ambient) + sssHighlight + (rim * mainLight.color);

                // Luzes Adicionais
                #if defined(_ADDITIONAL_LIGHTS)
                uint addLightsCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < addLightsCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    finalColor += foliageBase * (addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation));
                }
                #endif

                // Atmosfera / Neblina URP (Fog) sincronizada com o horizonte
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // Pass 2: ShadowCaster
        // -------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                float windWeight = input.color.a > 0.01 ? input.color.a : input.uv.y;
                posWS = ApplyFoliageWind(posWS, windWeight);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_Cutoff > 0.001)
                {
                    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(texColor.a - _Cutoff);
                }
                return 0;
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // Pass 3: DepthOnly
        // -------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float windWeight = input.color.a > 0.01 ? input.color.a : input.uv.y;
                posWS = ApplyFoliageWind(posWS, windWeight);

                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_Cutoff > 0.001)
                {
                    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(texColor.a - _Cutoff);
                }
                return 0;
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // Pass 4: DepthNormals
        // -------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float windWeight = input.color.a > 0.01 ? input.color.a : input.uv.y;
                posWS = ApplyFoliageWind(posWS, windWeight);

                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_Cutoff > 0.001)
                {
                    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(texColor.a - _Cutoff);
                }
                return half4(NormalizeNormalPerPixel(normalize(input.normalWS)), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
