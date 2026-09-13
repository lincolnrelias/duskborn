Shader "Duskborn/StylizedSkybox"
{
    Properties
    {
        [Header(Day Sky Gradient)]
        _DayZenithColor("Day Zenith Color", Color) = (0.28, 0.58, 0.95, 1.0)
        _DayHorizonColor("Day Horizon Color", Color) = (0.72, 0.88, 0.98, 1.0)
        _DayGroundColor("Day Ground Color", Color) = (0.35, 0.45, 0.38, 1.0)

        [Header(Night Sky Gradient)]
        _NightZenithColor("Night Zenith Color", Color) = (0.04, 0.06, 0.16, 1.0)
        _NightHorizonColor("Night Horizon Color", Color) = (0.12, 0.18, 0.32, 1.0)
        _NightGroundColor("Night Ground Color", Color) = (0.03, 0.04, 0.08, 1.0)

        [Header(Horizon Calibration)]
        _HorizonOffset("Horizon Offset", Range(-0.5, 0.5)) = 0.0
        _HorizonHeight("Horizon Blend Height", Range(0.05, 1.0)) = 0.35
        _HorizonFalloff("Horizon Falloff Power", Range(0.2, 4.0)) = 1.4
        _GroundHeight("Ground Blend Height", Range(0.05, 1.0)) = 0.45
        _GroundFalloff("Ground Falloff Power", Range(0.2, 4.0)) = 1.2

        [Header(Dusk and Dawn Horizon Flare)]
        _DuskDawnColor("Dusk/Dawn Glow Color", Color) = (1.0, 0.48, 0.22, 1.0)
        _DuskDawnIntensity("Dusk/Dawn Flare Intensity", Range(0.0, 3.0)) = 1.45

        [Header(Stylized Sun)]
        _SunColor("Sun Disc Color", Color) = (1.0, 0.96, 0.78, 1.0)
        _SunHaloColor("Sun Corona / Halo Color", Color) = (1.0, 0.82, 0.45, 1.0)
        _SunDirection("Sun Direction WS", Vector) = (0.0, 0.75, 0.65, 0.0)
        _SunRadius("Sun Disc Angular Radius (deg)", Range(1.0, 20.0)) = 5.5
        _SunSoftness("Sun Edge Softness (deg)", Range(0.05, 5.0)) = 0.35
        _SunIntensity("Sun Brightness Boost", Range(0.5, 4.0)) = 1.8
        _SunHaloFalloff("Sun Halo Falloff Power", Range(2.0, 64.0)) = 12.0
        _SunHaloIntensity("Sun Halo Intensity", Range(0.0, 2.5)) = 0.85

        [Header(Stylized Moon)]
        _MoonColor("Moon Disc Color", Color) = (0.85, 0.92, 1.0, 1.0)
        _MoonHaloColor("Moon Aura Glow Color", Color) = (0.35, 0.55, 0.95, 1.0)
        _MoonDirection("Moon Direction WS", Vector) = (0.0, -0.75, -0.65, 0.0)
        _MoonRadius("Moon Disc Angular Radius (deg)", Range(1.0, 20.0)) = 4.8
        _MoonSoftness("Moon Edge Softness (deg)", Range(0.05, 5.0)) = 0.25
        _MoonIntensity("Moon Brightness Boost", Range(0.5, 3.0)) = 1.5
        _MoonCrescentOffset("Moon Crescent Cutoff Offset", Vector) = (0.32, 0.18, 0.0, 0.0)
        _MoonCrescentSize("Moon Crescent Shadow Size", Range(0.5, 1.5)) = 0.92
        _MoonCrescentStrength("Moon Crescent Shadow Cut", Range(0.0, 1.0)) = 0.95
        _MoonHaloFalloff("Moon Halo Falloff Power", Range(2.0, 64.0)) = 16.0
        _MoonHaloIntensity("Moon Halo Intensity", Range(0.0, 2.0)) = 0.65

        [Header(Twinkling Night Stars)]
        _StarColor("Star Brightness Color", Color) = (0.95, 0.98, 1.0, 1.0)
        _StarDensity("Star Threshold Cutoff", Range(0.92, 0.999)) = 0.985
        _StarScale("Star Celestial Grid Scale", Range(20.0, 250.0)) = 95.0
        _StarSize("Star Point Radius", Range(0.02, 0.35)) = 0.14
        _StarBrightness("Star Peak Brightness", Range(0.5, 6.0)) = 2.8
        _StarTwinkleSpeed("Star Twinkle Frequency", Range(0.5, 10.0)) = 3.2

        [Header(Anime Ghibli Clouds)]
        _CloudBaseColor("Cloud Lit Base Color", Color) = (0.98, 0.98, 1.0, 1.0)
        _CloudShadowColor("Cloud Shadow Tint", Color) = (0.58, 0.68, 0.88, 1.0)
        _CloudSunHighlight("Cloud Sun Edge Highlight", Color) = (1.0, 0.92, 0.72, 1.0)
        _CloudSpeed("Cloud Panning Velocity", Vector) = (0.008, 0.004, 0.0, 0.0)
        _CloudScale("Cloud Density Scale", Range(0.1, 5.0)) = 0.75
        _CloudCutoff("Cloud Coverage Threshold", Range(0.1, 0.9)) = 0.52
        _CloudSoftness("Cloud Edge Cel Softness", Range(0.01, 0.4)) = 0.08
        _CloudCurvature("Sky Dome Curvature Factor", Range(0.05, 0.8)) = 0.28
        _CloudOpacity("Cloud Overall Opacity", Range(0.0, 1.0)) = 0.88

        [Header(Day Night Global Controller)]
        _DayNightBlend("Day to Night Blend (0=Day, 1=Night)", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Background" 
            "RenderType" = "Background" 
            "PreviewType" = "Skybox" 
        }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "StylizedSkyboxForward"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DayZenithColor;
                half4 _DayHorizonColor;
                half4 _DayGroundColor;

                half4 _NightZenithColor;
                half4 _NightHorizonColor;
                half4 _NightGroundColor;

                float _HorizonOffset;
                float _HorizonHeight;
                float _HorizonFalloff;
                float _GroundHeight;
                float _GroundFalloff;

                half4 _DuskDawnColor;
                float _DuskDawnIntensity;

                half4 _SunColor;
                half4 _SunHaloColor;
                float4 _SunDirection;
                float _SunRadius;
                float _SunSoftness;
                float _SunIntensity;
                float _SunHaloFalloff;
                float _SunHaloIntensity;

                half4 _MoonColor;
                half4 _MoonHaloColor;
                float4 _MoonDirection;
                float _MoonRadius;
                float _MoonSoftness;
                float _MoonIntensity;
                float4 _MoonCrescentOffset;
                float _MoonCrescentSize;
                float _MoonCrescentStrength;
                float _MoonHaloFalloff;
                float _MoonHaloIntensity;

                half4 _StarColor;
                float _StarDensity;
                float _StarScale;
                float _StarSize;
                float _StarBrightness;
                float _StarTwinkleSpeed;

                half4 _CloudBaseColor;
                half4 _CloudShadowColor;
                half4 _CloudSunHighlight;
                float4 _CloudSpeed;
                float _CloudScale;
                float _CloudCutoff;
                float _CloudSoftness;
                float _CloudCurvature;
                float _CloudOpacity;

                float _DayNightBlend;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // No espaço do skybox, a posição do vértice normalizada é a direção de visão
                output.viewDirWS = normalize(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Força o skybox para o plano distante (far clip plane)
                #if UNITY_REVERSED_Z
                    output.positionCS.z = 1.0e-5f;
                #else
                    output.positionCS.z = output.positionCS.w - 1.0e-5f;
                #endif

                return output;
            }

            // ==========================================
            // Ruído Procedural para Estrelas & Nuvens
            // ==========================================
            float Hash3D(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Hash2D(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p.x * 12.9898 + p.y * 78.233) * 43758.5453123);
            }

            float SmoothNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash2D(i + float2(0.0, 0.0));
                float b = Hash2D(i + float2(1.0, 0.0));
                float c = Hash2D(i + float2(0.0, 1.0));
                float d = Hash2D(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
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
                        float2 r = b - f + float2(Hash2D(p + b), Hash2D((p + b) * 1.618));
                        float d = dot(r, r);
                        minDist = min(minDist, d);
                    }
                }
                return sqrt(minDist);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDirWS);
                float y = viewDir.y - _HorizonOffset;

                // 1. Interpolação de Cores Dia / Noite
                half3 zenithTarget  = lerp(_DayZenithColor.rgb,  _NightZenithColor.rgb,  _DayNightBlend);
                half3 horizonTarget = lerp(_DayHorizonColor.rgb, _NightHorizonColor.rgb, _DayNightBlend);
                half3 groundTarget  = lerp(_DayGroundColor.rgb,  _NightGroundColor.rgb,  _DayNightBlend);

                // Brilho alaranjado de Alvorada/Crepúsculo na linha do horizonte quando o sol está próximo ao nível 0
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunHorizonProximity = saturate(1.0 - abs(sunDir.y) * 3.8);
                float sunAzimuthProximity = saturate(dot(normalize(float3(viewDir.x, 0.0, viewDir.z)), normalize(float3(sunDir.x, 0.0, sunDir.z))));
                half3 duskFlare = _DuskDawnColor.rgb * (pow(sunAzimuthProximity, 3.5) * sunHorizonProximity * _DuskDawnIntensity);
                horizonTarget += duskFlare;

                // 2. Gradiente do Céu de Três Paradas (Zenith -> Horizon -> Ground)
                half3 skyColor;
                if (y >= 0.0)
                {
                    float t = saturate(y / max(0.01, _HorizonHeight));
                    skyColor = lerp(horizonTarget, zenithTarget, pow(t, _HorizonFalloff));
                }
                else
                {
                    float t = saturate(-y / max(0.01, _GroundHeight));
                    skyColor = lerp(horizonTarget, groundTarget, pow(t, _GroundFalloff));
                }

                // 3. Estrelas Noturnas Cintilantes (Twinkling Stars)
                if (viewDir.y > 0.01 && _DayNightBlend > 0.05)
                {
                    float3 starPos = viewDir * _StarScale;
                    float3 starCell = floor(starPos);
                    float starRand = Hash3D(starCell);

                    if (starRand > _StarDensity)
                    {
                        float3 cellFract = frac(starPos) - 0.5;
                        float dist = length(cellFract);
                        float starGlow = saturate(1.0 - dist / max(0.01, _StarSize));
                        starGlow = pow(starGlow, 2.5);

                        // Cintilação orgânica temporal com fase individual por estrela
                        float twinkle = sin(_Time.y * _StarTwinkleSpeed + starRand * 45.0) * 0.5 + 0.5;
                        twinkle = lerp(0.35, 1.0, twinkle);

                        // Esmaecimento suave nas bordas do horizonte e desaparecimento de dia
                        float horizonStarFade = saturate(viewDir.y * 6.0);
                        float nightStarVisibility = pow(_DayNightBlend, 1.8);

                        half3 starLight = _StarColor.rgb * (starGlow * twinkle * _StarBrightness * horizonStarFade * nightStarVisibility);
                        skyColor += starLight;
                    }
                }

                // 4. Disco Solar Estilizado (Crisp Cel-Shaded Sun)
                float sunCosAngle = dot(viewDir, sunDir);
                float sunAngleRad = acos(clamp(sunCosAngle, -1.0, 1.0));
                float sunRadLimit = _SunRadius * 0.0174532925; // Graus para radianos
                float sunSoftnessRad = max(0.001, _SunSoftness * 0.0174532925);

                float sunDisc = 1.0 - smoothstep(sunRadLimit - sunSoftnessRad, sunRadLimit + sunSoftnessRad, sunAngleRad);
                float sunHalo = pow(saturate(sunCosAngle), _SunHaloFalloff) * _SunHaloIntensity;
                float sunElevationFade = saturate(sunDir.y * 4.0 + 0.25);

                half3 sunContribution = (_SunColor.rgb * sunDisc * _SunIntensity + _SunHaloColor.rgb * sunHalo) * sunElevationFade;
                skyColor += sunContribution;

                // 5. Disco Lunar Estilizado com Fase Crescente (Stylized Cel Moon)
                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonCosAngle = dot(viewDir, moonDir);
                float moonAngleRad = acos(clamp(moonCosAngle, -1.0, 1.0));
                float moonRadLimit = _MoonRadius * 0.0174532925;
                float moonSoftnessRad = max(0.001, _MoonSoftness * 0.0174532925);

                float moonDisc = 1.0 - smoothstep(moonRadLimit - moonSoftnessRad, moonRadLimit + moonSoftnessRad, moonAngleRad);

                // Corte da sombra crescente (crescent cutout)
                float3 moonMaskDir = normalize(moonDir + _MoonCrescentOffset.xyz * 0.12);
                float moonMaskAngleRad = acos(clamp(dot(viewDir, moonMaskDir), -1.0, 1.0));
                float moonMaskDisc = 1.0 - smoothstep(moonRadLimit * _MoonCrescentSize - moonSoftnessRad, 
                                                      moonRadLimit * _MoonCrescentSize + moonSoftnessRad, 
                                                      moonMaskAngleRad);
                float crescentMoon = saturate(moonDisc - moonMaskDisc * _MoonCrescentStrength);

                float moonHalo = pow(saturate(moonCosAngle), _MoonHaloFalloff) * _MoonHaloIntensity * _DayNightBlend;
                float moonElevationFade = saturate(moonDir.y * 4.0 + 0.25);

                half3 moonContribution = (_MoonColor.rgb * crescentMoon * _MoonIntensity + _MoonHaloColor.rgb * moonHalo) * moonElevationFade;
                skyColor += moonContribution;

                // 6. Camada de Nuvens Estilizadas Estilo Anime / Ghibli
                if (viewDir.y > 0.02)
                {
                    // Projeção esférica na cúpula celeste com amortecimento no horizonte
                    float domeY = max(0.05, viewDir.y + _CloudCurvature);
                    float2 cloudUV = (viewDir.xz / domeY) * _CloudScale + _Time.y * _CloudSpeed.xy;

                    // Duas oitavas de ruído harmônico orgânico
                    float n1 = SmoothNoise2D(cloudUV * 3.5);
                    float n2 = Voronoi2D(cloudUV * 7.0 + float2(0.4, 0.8));
                    float combinedNoise = n1 * 0.65 + (1.0 - n2) * 0.35;

                    // Corte estilizado cel-shaded com bordas nítidas
                    float cloudMask = smoothstep(_CloudCutoff - _CloudSoftness, _CloudCutoff + _CloudSoftness, combinedNoise);

                    // Iluminação de 2 tons nas nuvens (crina iluminada pelo sol e corpo com sombra atmosférica)
                    float sunLightAlignment = saturate(dot(viewDir, sunDir) * 0.5 + 0.5);
                    half3 cloudSunColor = lerp(_CloudBaseColor.rgb, _CloudSunHighlight.rgb, sunLightAlignment * (1.0 - _DayNightBlend));
                    half3 cloudShadowColor = lerp(_CloudShadowColor.rgb, _NightHorizonColor.rgb * 0.8, _DayNightBlend);
                    half3 cloudFinalColor = lerp(cloudShadowColor, cloudSunColor, saturate(combinedNoise * 1.4));

                    // Desvanecimento natural da nuvem na borda do horizonte
                    float horizonCloudFade = saturate(viewDir.y * 3.5);
                    float totalCloudAlpha = cloudMask * horizonCloudFade * _CloudOpacity;

                    skyColor = lerp(skyColor, cloudFinalColor, totalCloudAlpha);
                }

                return half4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
