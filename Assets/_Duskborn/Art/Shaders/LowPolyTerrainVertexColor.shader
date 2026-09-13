Shader "Duskborn/LowPolyTerrainVertexColor"
{
    Properties
    {
        _CelCutoff("Cel Shadow Cutoff", Range(0.0, 1.0)) = 0.42
        _CelSmoothness("Cel Smoothness", Range(0.01, 0.4)) = 0.06
        _ShadowTint("Shadow Color Tint", Color) = (0.42, 0.44, 0.58, 1.0)
        _SunlightBoost("Sunlight Intensity", Range(0.5, 2.5)) = 1.15
        _RimIntensity("Rim Light Intensity", Range(0.0, 1.0)) = 0.18
        _RimPower("Rim Light Power", Range(1.0, 8.0)) = 4.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float  _CelCutoff;
                float  _CelSmoothness;
                float4 _ShadowTint;
                float  _SunlightBoost;
                float  _RimIntensity;
                float  _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = normInputs.normalWS;
                output.color      = input.color;
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                
                // Stylized Half-Lambert Cel Shading com Sombras Suaves
                half NdotL = dot(normalWS, mainLight.direction);
                half halfLambert = NdotL * 0.5 + 0.5;
                half celDiffuse = smoothstep(_CelCutoff - _CelSmoothness, _CelCutoff + _CelSmoothness, halfLambert);

                // Atenuação de sombras projetadas suave (preserva penumbra suave do URP)
                half shadowSoft = smoothstep(0.0, 1.0, mainLight.shadowAttenuation);
                half lightFactor = celDiffuse * shadowSoft;

                // Cor da sombra estilizada enriquecida com luz ambiente hemisférica suave
                half3 ambientSH = SampleSH(normalWS);
                half3 shadowColor = _ShadowTint.rgb * (ambientSH + half3(0.25, 0.25, 0.30));
                half3 litLight = mainLight.color * _SunlightBoost;
                half3 directLight = lerp(shadowColor, litLight, lightFactor);

                // Subtle Sunlit Rim Light (apenas em superfícies expostas à luz)
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                half NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                half rim = pow(NdotV, _RimPower) * _RimIntensity * lightFactor;

                half3 ambient = ambientSH * 0.40;
                half3 finalColor = input.color.rgb * (directLight + ambient) + (rim * mainLight.color);

                // Additional Lights (tochas, habilidades, lanternas)
                #if defined(_ADDITIONAL_LIGHTS)
                uint addLightsCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < addLightsCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    finalColor += input.color.rgb * (addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation));
                }
                #endif

                // Atmosfera / Neblina URP (Fog)
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, input.color.a);
            }
            ENDHLSL
        }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
