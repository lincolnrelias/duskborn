Shader "Duskborn/LowPolyWater"
{
    Properties
    {
        [Header(Water Color and Depth Absorption)]
        _ShallowColor("Shallow Water Color", Color) = (0.24, 0.82, 0.88, 0.78)
        _DeepColor("Deep Water Color", Color) = (0.05, 0.22, 0.48, 0.95)
        _HorizonColor("Horizon / Grazing Tint", Color) = (0.62, 0.85, 0.98, 0.65)
        _DepthDensity("Depth Absorption Density", Range(0.1, 4.0)) = 0.85
        _BaseShallowTint("Minimum Shallow Tint", Range(0.0, 1.0)) = 0.45
        _FresnelBase("Base Perpendicular Sheen", Range(0.0, 0.5)) = 0.18

        [Header(Screen Space Refraction)]
        _RefractionStrength("Refraction Distortion", Range(0.0, 0.25)) = 0.045
        _ChromaticAberration("Chromatic Aberration", Range(0.0, 0.05)) = 0.012

        [Header(Underwater Caustics)]
        _CausticsColor("Caustics Color", Color) = (0.75, 0.96, 1.0, 1.0)
        _CausticsStrength("Caustics Intensity", Range(0.0, 3.0)) = 1.25
        _CausticsScale("Caustics Scale", Range(0.2, 5.0)) = 1.6
        _CausticsSpeed("Caustics Animation Speed", Range(0.1, 3.0)) = 0.85
        _CausticsPower("Caustics Sharpness", Range(1.0, 8.0)) = 3.2
        _CausticsDepthFade("Caustics Depth Fade", Range(0.1, 2.0)) = 0.45

        [Header(Stylized Foam System)]
        _FoamColor("Foam Color", Color) = (0.98, 0.99, 1.0, 1.0)
        _FoamDistance("Shoreline Foam Reach", Range(0.05, 1.5)) = 0.28
        _ContactRimWidth("Shore Contact Rim Width", Range(0.01, 0.15)) = 0.035
        _FoamNoiseScale("Foam Bubble Scale", Range(0.5, 10.0)) = 4.0
        _FoamNoiseSpeed("Foam Drift Speed", Range(0.01, 0.5)) = 0.08
        _FoamCutoff("Foam Bubble Coverage", Range(0.1, 0.9)) = 0.52
        _ShoreWaveSpeed("Tidal Breathing Speed", Range(0.1, 2.0)) = 0.45
        _ShoreTideAmount("Tidal Reach Variation", Range(0.0, 0.5)) = 0.08
        _CrestFoamThreshold("Wave Crest Foam Trigger", Range(0.3, 1.0)) = 0.75

        [Header(Gerstner Waves and Surface Motion)]
        _WaveSpeed("Wave Overall Speed", Range(0.1, 3.0)) = 0.85
        _WaveHeight("Wave Amplitude", Range(0.0, 0.3)) = 0.040
        _WaveSteepness("Wave Crest Pinch", Range(0.0, 1.0)) = 0.30
        _WaveLength("Base Wavelength", Range(1.0, 25.0)) = 10.0
        _FacetStrength("Low-Poly Faceting (0=Smooth 1=Faceted)", Range(0.0, 1.0)) = 0.0

        [Header(Lighting Specular and SSS)]
        _SunSpecPower("Specular Focus", Range(16.0, 256.0)) = 64.0
        _SunSpecCutoff("Cel Specular Threshold", Range(0.0, 0.99)) = 0.65
        _SunSpecIntensity("Specular Brightness", Range(0.0, 3.0)) = 1.35
        _FresnelPower("Fresnel Reflection Power", Range(1.0, 8.0)) = 3.5
        _SSSColor("Subsurface Scatter Color", Color) = (0.15, 0.92, 0.82, 1.0)
        _SSSIntensity("SSS Glow Intensity", Range(0.0, 2.0)) = 0.65
        _SSSPower("SSS Sun Focus", Range(1.0, 8.0)) = 3.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float2 waveData     : TEXCOORD3;
                float  fogFactor    : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _HorizonColor;
                float  _DepthDensity;
                float  _BaseShallowTint;
                float  _FresnelBase;

                float  _RefractionStrength;
                float  _ChromaticAberration;

                float4 _CausticsColor;
                float  _CausticsStrength;
                float  _CausticsScale;
                float  _CausticsSpeed;
                float  _CausticsPower;
                float  _CausticsDepthFade;

                float4 _FoamColor;
                float  _FoamDistance;
                float  _ContactRimWidth;
                float  _FoamNoiseScale;
                float  _FoamNoiseSpeed;
                float  _FoamCutoff;
                float  _ShoreWaveSpeed;
                float  _ShoreTideAmount;
                float  _CrestFoamThreshold;

                float  _WaveSpeed;
                float  _WaveHeight;
                float  _WaveSteepness;
                float  _WaveLength;
                float  _FacetStrength;

                float  _SunSpecPower;
                float  _SunSpecCutoff;
                float  _SunSpecIntensity;
                float  _FresnelPower;
                float4 _SSSColor;
                float  _SSSIntensity;
                float  _SSSPower;
            CBUFFER_END

            // Parâmetros do jogador na água (enviados por PlayerWaterInteraction)
            // x = worldX, y = waterY, z = worldZ, w = wadingStrength
            uniform float4 _PlayerWaterData;

            // ==========================================
            // Funções Matemáticas Procedurais de Onda
            // ==========================================
            void EvaluateGerstnerWave(
                float2 dir, float length, float steepness, float t,
                float3 originWS, inout float3 disp, inout float3 tangent, inout float3 binormal, inout float crestAccum)
            {
                float2 d = normalize(dir);
                float k = 6.28318530718 / max(0.1, length);
                float c = sqrt(9.8 / k) * _WaveSpeed;
                float a = (steepness / k) * _WaveHeight;
                float q = steepness / (k * a * 3.0 + 0.0001);
                q = saturate(q * _WaveSteepness);

                float theta = k * dot(d, originWS.xz) - t * c;
                float sinTheta = sin(theta);
                float cosTheta = cos(theta);

                disp.x += q * a * d.x * cosTheta;
                disp.z += q * a * d.y * cosTheta;
                disp.y += a * sinTheta;

                crestAccum += sinTheta;

                float wa = a * k;
                tangent.x += -q * d.x * d.x * wa * sinTheta;
                tangent.y += d.x * wa * cosTheta;
                tangent.z += -q * d.x * d.y * wa * sinTheta;

                binormal.x += -q * d.x * d.y * wa * sinTheta;
                binormal.y += d.y * wa * cosTheta;
                binormal.z += -q * d.y * d.y * wa * sinTheta;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y;

                float3 disp = float3(0, 0, 0);
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float crestAccum = 0.0;

                // 3 Oitavas de Ondas de Gerstner Harmônicas (Comprimentos maiores e velocidades suaves)
                EvaluateGerstnerWave(float2(0.8, 0.6), _WaveLength, 0.45, t, positionWS, disp, tangent, binormal, crestAccum);
                EvaluateGerstnerWave(float2(-0.6, 0.8), _WaveLength * 0.68, 0.35, t * 1.08, positionWS, disp, tangent, binormal, crestAccum);
                EvaluateGerstnerWave(float2(0.9, -0.4), _WaveLength * 0.45, 0.20, t * 1.18, positionWS, disp, tangent, binormal, crestAccum);

                positionWS += disp;

                // Ondulação do jogador na água (balanço intermediário harmônico)
                if (_PlayerWaterData.w > 0.01)
                {
                    float distPlayer = distance(positionWS.xz, _PlayerWaterData.xz);
                    if (distPlayer < 2.4)
                    {
                        float rippleWave = sin(distPlayer * 5.5 - t * 5.5);
                        float rippleAtten = saturate(1.0 - distPlayer / 2.4) * exp(-distPlayer * 1.35) * _PlayerWaterData.w;
                        positionWS.y += rippleWave * 0.016 * rippleAtten;
                    }
                }

                float3 normalWS = normalize(cross(binormal, tangent));

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS   = normalWS;
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.waveData   = float2(saturate((crestAccum / 3.0) * 0.5 + 0.5), disp.y);
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // ==========================================
            // Ruído Procedural Rápido Voronoi (Cáusticas & Espuma)
            // ==========================================
            float2 Hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float Voronoi2D(float2 uv)
            {
                float2 p = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;

                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 b = float2(i, j);
                        float2 r = b - f + Hash2D(p + b);
                        float d = dot(r, r);
                        minDist = min(minDist, d);
                    }
                }
                return sqrt(minDist);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normal da superfície: interpolação entre analítica suave e facetada low-poly
                float3 flatNormalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                float3 normalWS = normalize(lerp(input.normalWS, flatNormalWS, _FacetStrength));

                // Coordenadas de Tela
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                float surfaceLinearDepth = input.screenPos.w;

                // Amostragem de Profundidade Bruta e Linear
                #if UNITY_REVERSED_Z
                    float rawDepth = SampleSceneDepth(screenUV);
                #else
                    float rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(screenUV));
                #endif
                float sceneLinearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterDepth = max(0.0, sceneLinearDepth - surfaceLinearDepth);

                // 1. Refração Screen-Space Segura com Dispersão Cromática
                float2 refractOffset = normalWS.xz * (_RefractionStrength / max(1.0, sceneLinearDepth));
                float2 refractUV = screenUV + refractOffset;

                // Teste de oclusão de refração: impede que objetos acima do nível da água sangrem na água
                #if UNITY_REVERSED_Z
                    float testRawDepth = SampleSceneDepth(refractUV);
                #else
                    float testRawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(refractUV));
                #endif
                float testSceneDepth = LinearEyeDepth(testRawDepth, _ZBufferParams);
                if (testSceneDepth < surfaceLinearDepth)
                {
                    refractUV = screenUV;
                }

                // Amostragem de Cor do Fundo Submerso (_CameraOpaqueTexture)
                float2 caOffset = refractOffset * _ChromaticAberration;
                half sceneR = SampleSceneColor(refractUV - caOffset).r;
                half sceneG = SampleSceneColor(refractUV).g;
                half sceneB = SampleSceneColor(refractUV + caOffset).b;
                half3 underwaterColor = half3(sceneR, sceneG, sceneB);

                // 2. Absorção Óptica Exponencial de Profundidade (Beer-Lambert)
                float depthFactor = saturate(1.0 - exp(-waterDepth * _DepthDensity));
                half4 waterCol = lerp(_ShallowColor, _DeepColor, depthFactor);
                float shallowTintFactor = saturate(_BaseShallowTint + depthFactor * (1.0 - _BaseShallowTint));
                half3 blendedBase = lerp(underwaterColor, waterCol.rgb, shallowTintFactor);

                // 3. Cáusticas Subaquáticas Animadas Projetadas no Fundo
                float3 underwaterWS = ComputeWorldSpacePosition(refractUV, rawDepth, UNITY_MATRIX_I_VP);
                float t = _Time.y;
                float2 cUV1 = underwaterWS.xz * _CausticsScale + float2(0.7, 0.5) * (t * _CausticsSpeed);
                float2 cUV2 = underwaterWS.xz * (_CausticsScale * 1.18) - float2(0.6, 0.8) * (t * _CausticsSpeed * 0.85);

                float c1 = Voronoi2D(cUV1);
                float c2 = Voronoi2D(cUV2);
                float causticVal = min(c1, c2);
                causticVal = pow(saturate(1.0 - causticVal), _CausticsPower) * _CausticsStrength;

                // Atenuação de cáusticas por profundidade e na arrebentação rasa
                float causticDepthFade = exp(-waterDepth * _CausticsDepthFade);
                float causticShoreFade = saturate(waterDepth * 3.5);
                float totalCaustic = causticVal * causticDepthFade * causticShoreFade;

                // Sombras e Iluminação Principal
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadowAtten = mainLight.shadowAttenuation;

                half3 causticColor = _CausticsColor.rgb * (totalCaustic * shadowAtten * mainLight.color);
                blendedBase += causticColor;

                // 4. Sistema de Espuma Estilizada Multi-Camada (Otimizado para poças, margens e gargantas estreitas)
                float foamSpeed = _FoamNoiseSpeed;
                float2 fUV1 = input.positionWS.xz * _FoamNoiseScale + float2(0.25, 0.18) * (t * foamSpeed);
                float2 fUV2 = input.positionWS.xz * (_FoamNoiseScale * 1.65) - float2(0.18, 0.22) * (t * foamSpeed * 1.25);
                float foamNoise = Voronoi2D(fUV1) * 0.65 + Voronoi2D(fUV2) * 0.35;

                // Profundidade estabilizada: desacopla o contorno da margem da oscilação vertical da onda.
                // Em gargantas/canais estreitos onde as bordas estão muito próximas, isso impede que
                // as duas margens balancem, respirem e se empurrem em direção ao centro em alta frequência.
                float stableDepth = max(0.001, waterDepth - input.waveData.y * 0.85);

                // 4.1 Contorno Nítido de Contato na Margem e Obstáculos
                // Cria um outline nítido na linha de encontro com a terra (0 a 3.5cm), sem preencher o canal
                float rimFactor = 1.0 - saturate(stableDepth / max(0.005, _ContactRimWidth));
                float contactRim = smoothstep(0.25, 0.85, rimFactor * 1.15 - foamNoise * 0.35);

                // 4.2 Espuma de Arrebentação e Maré (com amortecimento em gargantas estreitas)
                float tidalCycle = sin(t * _ShoreWaveSpeed + input.positionWS.x * 0.2 + input.positionWS.z * 0.2) * _ShoreTideAmount;

                // Em canais estreitos/gargantas onde as margens são próximas e rasas, amortece a respiração de maré
                // para evitar que as ondas das duas margens se acumulem e se sobreponham no centro
                float tidalDamp = smoothstep(0.06, 0.32, stableDepth);
                float effectiveShoreDist = max(0.04, _FoamDistance * (1.0 + tidalCycle * tidalDamp));
                float shoreFactor = saturate(1.0 - stableDepth / effectiveShoreDist);

                // Atenuação suave em águas ultra rasas: mantém apenas a borda de contato limpa
                // e o centro translúcido com cáusticas, sem virar uma mancha branca sólida
                float washFoamShallowFade = smoothstep(0.02, 0.08, stableDepth);
                float foamThreshold = lerp(0.85, _FoamCutoff, shoreFactor);
                float washFoam = smoothstep(foamThreshold, foamThreshold + 0.08, foamNoise) * shoreFactor * washFoamShallowFade;

                float shoreFoam = saturate(contactRim + washFoam);

                // 4.3 Espuma de Crista de Onda (Whitecaps ativas apenas em águas abertas/profundas)
                float crestFoam = 0.0;
                if (input.waveData.x > _CrestFoamThreshold)
                {
                    float deepWaterMask = smoothstep(0.18, 0.45, stableDepth);
                    float crestFrac = (input.waveData.x - _CrestFoamThreshold) / max(0.01, 1.0 - _CrestFoamThreshold);
                    crestFoam = smoothstep(_FoamCutoff, _FoamCutoff + 0.15, crestFrac * 1.3 - foamNoise * 0.5) * deepWaterMask;
                }

                // 4.4 Espuma de Contato e Esteira nos Pés do Jogador (ponto médio estilizado)
                float playerFoam = 0.0;
                if (_PlayerWaterData.w > 0.01)
                {
                    float distPlayer = distance(input.positionWS.xz, _PlayerWaterData.xz);
                    if (distPlayer < 0.85)
                    {
                        // Colar de espuma suave nos pés com rastro em esteira
                        float contactRing = smoothstep(0.85, 0.20, distPlayer) * smoothstep(0.02, 0.15, distPlayer);
                        float froth = saturate(contactRing * 1.35 - foamNoise * 0.40);
                        playerFoam = froth * 0.72 * _PlayerWaterData.w;
                    }
                }

                float totalFoam = saturate(shoreFoam + crestFoam + playerFoam);

                // 5. Iluminação Cel-Shaded, Especular Solar e Translucidez (SSS)
                half NdotL = dot(normalWS, mainLight.direction);
                half halfL = NdotL * 0.5 + 0.5;
                half celDiffuse = smoothstep(0.32, 0.52, halfL);
                half shadowSoft = smoothstep(0.0, 1.0, shadowAtten);

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));

                // Especular Cel-Shaded com Glint Solar Estilizado
                half specBase = pow(NdotH, _SunSpecPower);
                half specGlint = smoothstep(_SunSpecCutoff, _SunSpecCutoff + 0.06, specBase) * _SunSpecIntensity * shadowSoft;
                half specAura = pow(NdotH, max(4.0, _SunSpecPower * 0.2)) * (_SunSpecIntensity * 0.22) * shadowSoft;
                half3 specularHighlight = (specGlint + specAura) * mainLight.color;

                // Subsurface Scattering (Translucidez vibrante na crista da onda contra o Sol)
                half sssDot = saturate(dot(viewDirWS, -mainLight.direction));
                half sss = pow(sssDot, _SSSPower) * saturate(input.waveData.x * 1.8) * _SSSIntensity * shadowSoft;
                half3 sssHighlight = _SSSColor.rgb * (sss * mainLight.color);

                // Reflexão de Fresnel no Horizonte (com base F0 mínima para manter brilho em visão perpendicular)
                half rawFresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half fresnel = lerp(_FresnelBase, 1.0, rawFresnel);
                half3 horizonReflection = _HorizonColor.rgb;

                // 6. Composição Visual Final
                half3 finalRGB = blendedBase + sssHighlight;
                finalRGB = lerp(finalRGB, horizonReflection, fresnel * _HorizonColor.a);
                finalRGB += specularHighlight;

                // Mescla com a Espuma Estilizada
                finalRGB = lerp(finalRGB, _FoamColor.rgb, totalFoam * _FoamColor.a);

                // Opacidade Geral consistente (evita que a água suma ao olhar de perto/cima)
                half baseAlpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);
                half finalAlpha = saturate(baseAlpha + totalFoam * 0.95 + fresnel * 0.35);

                // Integração com Neblina URP (Fog)
                finalRGB = MixFog(finalRGB, input.fogFactor);

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}
