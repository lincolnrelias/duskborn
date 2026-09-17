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
        _StarDensity("Star Threshold Cutoff", Range(0.90, 0.999)) = 0.975
        _StarScale("Star Celestial Grid Scale", Range(20.0, 250.0)) = 110.0
        _StarSize("Star Point Radius", Range(0.02, 0.45)) = 0.20
        _StarBrightness("Star Peak Brightness", Range(0.5, 8.0)) = 3.8
        _StarTwinkleSpeed("Star Twinkle Frequency", Range(0.5, 10.0)) = 3.2

        [Header(Stylized Ghibli Anime Clouds)]
        _CloudBaseColor("Cloud Lit Base Color", Color) = (0.98, 0.98, 1.0, 1.0)
        _CloudShadowColor("Cloud Shadow Tint", Color) = (0.58, 0.68, 0.88, 1.0)
        _CloudSunHighlight("Cloud Sun Edge Highlight", Color) = (1.0, 0.92, 0.72, 1.0)
        _CloudDuskHighlight("Cloud Dusk/Dawn Rim Color", Color) = (1.0, 0.52, 0.28, 1.0)
        _CloudMoonHighlight("Cloud Moon Silver Highlight", Color) = (0.80, 0.88, 1.0, 1.0)
        _CloudSpeed("Cloud Panning Velocity", Vector) = (0.006, 0.003, 0.0, 0.0)
        _CloudScale("Cloud Density Scale", Range(0.1, 5.0)) = 0.75
        _CloudCutoff("Cloud Coverage Threshold (Day)", Range(0.1, 0.9)) = 0.46
        _NightCloudCutoff("Cloud Coverage Cutoff (Night)", Range(0.1, 0.95)) = 0.62
        _CloudSoftness("Cloud Edge Cel Softness", Range(0.01, 0.4)) = 0.08
        _CloudCurvature("Sky Dome Curvature Factor", Range(0.05, 0.8)) = 0.28
        _CloudOpacity("Cloud Overall Opacity (Day)", Range(0.0, 1.0)) = 0.92
        _NightCloudOpacity("Cloud Overall Opacity (Night)", Range(0.0, 1.0)) = 0.55
        _CloudWarpStrength("Cloud Domain Warp (Anti-Spheroid)", Range(0.0, 2.0)) = 0.65
        _CloudCirrusStrength("High Cirrus Wisps", Range(0.0, 1.0)) = 0.30
        _CloudNormalBump("Cloud Volumetric Cel Shading", Range(0.5, 10.0)) = 4.5

        [Header(Day Night Global Controller)]
        _DayNightBlend("Day to Night Blend (0=Day, 1=Night)", Range(0.0, 1.0)) = 0.0
        _BossNightBlend("Boss Night Blood Moon Blend", Range(0.0, 1.0)) = 0.0
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
                half4 _CloudDuskHighlight;
                half4 _CloudMoonHighlight;
                float4 _CloudSpeed;
                float _CloudScale;
                float _CloudCutoff;
                float _NightCloudCutoff;
                float _CloudSoftness;
                float _CloudCurvature;
                float _CloudOpacity;
                float _NightCloudOpacity;
                float _CloudWarpStrength;
                float _CloudCirrusStrength;
                float _CloudNormalBump;

                float _DayNightBlend;
                float _BossNightBlend;
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

            // Rotated 2D FBM (Fractal Brownian Motion)
            float FBM2D(float2 p)
            {
                float val = 0.0;
                float amp = 0.5;
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    val += amp * SmoothNoise2D(p);
                    p = mul(rot, p) * 2.02 + float2(4.2, 7.8);
                    amp *= 0.5;
                }
                return val;
            }

            // Billow FBM for voluminous, puffy anime cloud formations
            float BillowFBM2D(float2 p)
            {
                float val = 0.0;
                float amp = 0.5;
                float2x2 rot = float2x2(0.80, 0.60, -0.60, 0.80);
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float n = SmoothNoise2D(p);
                    val += amp * (1.0 - abs(n * 2.0 - 1.0));
                    p = mul(rot, p) * 2.05 + float2(12.3, 15.7);
                    amp *= 0.5;
                }
                return val;
            }

            // Professional Domain Warping: twists, billows and breaks spherical symmetry completely
            float SampleCloudDensity(float2 uv)
            {
                float2 q = float2(
                    FBM2D(uv * 1.25),
                    FBM2D(uv * 1.25 + float2(5.2, 1.3))
                );

                float2 r = float2(
                    FBM2D(uv * 2.1 + 2.8 * q + float2(1.7, 9.2)),
                    FBM2D(uv * 2.1 + 2.8 * q + float2(8.3, 2.8))
                );

                float2 warpedUV = uv + r * _CloudWarpStrength;

                float fbmBase = FBM2D(warpedUV * 2.2);
                float billow = BillowFBM2D(warpedUV * 2.5);
                float cumulus = lerp(fbmBase, billow, 0.52);

                // Wispy high cirrus streaks drifting in the upper atmosphere
                float cirrus = SmoothNoise2D(uv * 5.2 + float2(uv.y * 1.8, 0.0)) * _CloudCirrusStrength;

                return saturate(cumulus * 0.88 + cirrus * 0.22);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDirWS);
                float y = viewDir.y - _HorizonOffset;

                // 1. Sky Gradients with Night 7 Blood Moon support
                half3 dayZenith = _DayZenithColor.rgb;
                half3 dayHorizon = _DayHorizonColor.rgb;
                half3 dayGround = _DayGroundColor.rgb;

                half3 nightZenith = _NightZenithColor.rgb;
                half3 nightHorizon = _NightHorizonColor.rgb;
                half3 nightGround = _NightGroundColor.rgb;

                // Boss Night (Night 7): dramatic deep crimson / blood sky
                if (_BossNightBlend > 0.01)
                {
                    half3 bossZenith = half3(0.24, 0.04, 0.06);
                    half3 bossHorizon = half3(0.55, 0.12, 0.10);
                    half3 bossGround = half3(0.12, 0.02, 0.03);

                    nightZenith = lerp(nightZenith, bossZenith, _BossNightBlend);
                    nightHorizon = lerp(nightHorizon, bossHorizon, _BossNightBlend);
                    nightGround = lerp(nightGround, bossGround, _BossNightBlend);
                }

                half3 zenithTarget  = lerp(dayZenith, nightZenith, _DayNightBlend);
                half3 horizonTarget = lerp(dayHorizon, nightHorizon, _DayNightBlend);
                half3 groundTarget  = lerp(dayGround, nightGround, _DayNightBlend);

                // Sunset / Sunrise horizon flare aligned with the Sun
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunHorizonProximity = saturate(1.0 - abs(sunDir.y) * 3.5);
                float sunAzimuthProximity = saturate(dot(normalize(float3(viewDir.x, 0.0, viewDir.z)), 
                                                         normalize(float3(sunDir.x, 0.0, sunDir.z))));
                half3 duskFlare = _DuskDawnColor.rgb * (pow(sunAzimuthProximity, 3.5) * sunHorizonProximity * _DuskDawnIntensity * (1.0 - _DayNightBlend * 0.7));
                horizonTarget += duskFlare;

                // 2. Three-Stop Spherical Sky Gradient
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

                // 3. Twinkling Stars (prominent anchor stars + deep field celestial canopy)
                half3 starLight = half3(0, 0, 0);
                if (viewDir.y > 0.01 && _DayNightBlend > 0.02)
                {
                    // Horizon fade (stars fade out near misty horizon) and dusk-to-night fade
                    float horizonStarFade = smoothstep(0.02, 0.20, viewDir.y);
                    float nightStarVisibility = smoothstep(0.05, 0.70, _DayNightBlend);

                    // --- Layer 1: Prominent Bright Anchor Stars (with subtle celestial color variations) ---
                    float3 starPos1 = viewDir * _StarScale;
                    float3 starCell1 = floor(starPos1);
                    float starRand1 = Hash3D(starCell1);

                    if (starRand1 > _StarDensity)
                    {
                        // Jitter star position safely inside cell interior (0.3 to 0.7) to prevent cell edge clipping
                        float3 jitter1 = float3(
                            frac(starRand1 * 17.13),
                            frac(starRand1 * 31.41),
                            frac(starRand1 * 73.79)
                        ) - 0.5;
                        float3 starCenter1 = starCell1 + 0.5 + jitter1 * 0.38;
                        float3 starDir1 = normalize(starCenter1);

                        // True angular distance scaled into cell coordinate space
                        float dist1 = _StarScale * length(viewDir - starDir1);
                        float size1 = _StarSize * lerp(0.75, 1.40, frac(starRand1 * 43.1));

                        if (dist1 < size1)
                        {
                            float starGlow = saturate(1.0 - dist1 / size1);
                            starGlow = pow(starGlow, 2.0);

                            // Twinkle with individual phase
                            float twinkle = sin(_Time.y * _StarTwinkleSpeed + starRand1 * 62.83) * 0.5 + 0.5;
                            twinkle = lerp(0.45, 1.25, twinkle);

                            // Extra radiance boost for major anchor stars
                            float anchorBoost = (starRand1 > (_StarDensity + (1.0 - _StarDensity) * 0.65)) ? 1.8 : 1.0;

                            // Subtle spectral tinting: sapphire diamond, golden topaz, or pure white
                            half3 starTint = _StarColor.rgb;
                            float tintRand = frac(starRand1 * 11.3);
                            if (tintRand > 0.68)
                                starTint *= half3(0.85, 0.94, 1.18); // cool diamond
                            else if (tintRand < 0.28)
                                starTint *= half3(1.15, 1.02, 0.86); // warm gold

                            starLight += starTint * (starGlow * twinkle * _StarBrightness * anchorBoost * horizonStarFade * nightStarVisibility);
                        }
                    }

                    // --- Layer 2: Deep Field Shimmering Stars (rich cosmic depth) ---
                    float fieldScale = _StarScale * 1.65;
                    float3 starPos2 = viewDir * fieldScale;
                    float3 starCell2 = floor(starPos2);
                    float starRand2 = Hash3D(starCell2 + float3(117.5, 31.2, 79.8));

                    if (starRand2 > lerp(0.962, 0.980, _StarDensity))
                    {
                        float3 jitter2 = float3(
                            frac(starRand2 * 23.41),
                            frac(starRand2 * 47.19),
                            frac(starRand2 * 89.23)
                        ) - 0.5;
                        float3 starCenter2 = starCell2 + 0.5 + jitter2 * 0.38;
                        float3 starDir2 = normalize(starCenter2);

                        float dist2 = fieldScale * length(viewDir - starDir2);
                        float size2 = _StarSize * 0.62;

                        if (dist2 < size2)
                        {
                            float starGlow2 = saturate(1.0 - dist2 / size2);
                            starGlow2 = pow(starGlow2, 2.4);

                            float twinkle2 = sin(_Time.y * (_StarTwinkleSpeed * 1.35) + starRand2 * 41.0) * 0.5 + 0.5;
                            twinkle2 = lerp(0.3, 1.05, twinkle2);

                            starLight += _StarColor.rgb * (starGlow2 * twinkle2 * (_StarBrightness * 0.6) * horizonStarFade * nightStarVisibility);
                        }
                    }

                    skyColor += starLight;
                }

                // 4. Stylized Cel Sun Disc & Corona
                float sunCosAngle = dot(viewDir, sunDir);
                float sunAngleRad = acos(clamp(sunCosAngle, -1.0, 1.0));
                float sunRadLimit = _SunRadius * 0.0174532925;
                float sunSoftnessRad = max(0.001, _SunSoftness * 0.0174532925);

                float sunDisc = 1.0 - smoothstep(sunRadLimit - sunSoftnessRad, sunRadLimit + sunSoftnessRad, sunAngleRad);
                float sunHalo = pow(saturate(sunCosAngle), _SunHaloFalloff) * _SunHaloIntensity;
                // Sun fades gracefully when crossing below horizon (sunDir.y <= 0)
                float sunElevationFade = saturate((sunDir.y + 0.08) * 8.0);

                half3 sunContribution = (_SunColor.rgb * sunDisc * _SunIntensity + _SunHaloColor.rgb * sunHalo) * sunElevationFade;
                skyColor += sunContribution;

                // 5. Stylized Cel Moon Disc & Crescent Aura
                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonCosAngle = dot(viewDir, moonDir);
                float moonAngleRad = acos(clamp(moonCosAngle, -1.0, 1.0));
                float moonRadLimit = _MoonRadius * 0.0174532925;
                float moonSoftnessRad = max(0.001, _MoonSoftness * 0.0174532925);

                float moonDisc = 1.0 - smoothstep(moonRadLimit - moonSoftnessRad, moonRadLimit + moonSoftnessRad, moonAngleRad);

                float3 moonMaskDir = normalize(moonDir + _MoonCrescentOffset.xyz * 0.12);
                float moonMaskAngleRad = acos(clamp(dot(viewDir, moonMaskDir), -1.0, 1.0));
                float moonMaskDisc = 1.0 - smoothstep(moonRadLimit * _MoonCrescentSize - moonSoftnessRad, 
                                                      moonRadLimit * _MoonCrescentSize + moonSoftnessRad, 
                                                      moonMaskAngleRad);
                float crescentMoon = saturate(moonDisc - moonMaskDisc * _MoonCrescentStrength);

                float moonHalo = pow(saturate(moonCosAngle), _MoonHaloFalloff) * _MoonHaloIntensity * _DayNightBlend;
                // Moon fades gracefully when crossing below horizon (moonDir.y <= 0)
                float moonElevationFade = saturate((moonDir.y + 0.08) * 8.0);

                half3 moonCol = _MoonColor.rgb;
                half3 moonHaloCol = _MoonHaloColor.rgb;
                if (_BossNightBlend > 0.01)
                {
                    moonCol = lerp(moonCol, half3(1.0, 0.28, 0.20), _BossNightBlend);
                    moonHaloCol = lerp(moonHaloCol, half3(0.95, 0.15, 0.10), _BossNightBlend);
                }

                half3 moonContribution = (moonCol * crescentMoon * _MoonIntensity + moonHaloCol * moonHalo) * moonElevationFade;
                skyColor += moonContribution;

                // 6. Professional Anime Ghibli Cloud System
                if (viewDir.y > 0.02)
                {
                    // Dynamic Day to Night Cloud Modulation:
                    // At night, clouds part into sparser, lighter wisps allowing the starry cosmos to shine through
                    float activeCutoff = lerp(_CloudCutoff, _NightCloudCutoff, _DayNightBlend);
                    float activeOpacity = lerp(_CloudOpacity, _NightCloudOpacity, _DayNightBlend);

                    // Hemispherical dome UV projection with smooth horizon curve
                    float domeY = max(0.06, viewDir.y + _CloudCurvature);
                    float2 cloudUV = (viewDir.xz / domeY) * _CloudScale + _Time.y * _CloudSpeed.xy;

                    // Volumetric density sampling
                    float eps = 0.016;
                    float d0 = SampleCloudDensity(cloudUV);
                    float dX = SampleCloudDensity(cloudUV + float2(eps, 0.0)) - d0;
                    float dY = SampleCloudDensity(cloudUV + float2(0.0, eps)) - d0;

                    // Multi-tier Cel Thresholding using active day/night cutoff
                    float cloudMask = smoothstep(activeCutoff - _CloudSoftness, activeCutoff + _CloudSoftness, d0);

                    // Procedural surface normal for 3D volumetric cel shading
                    float3 cloudNormal = normalize(float3(-dX * _CloudNormalBump, 1.0, -dY * _CloudNormalBump));

                    // Celestial light vector for cloud illumination (Sun by day, Moon by night)
                    float3 activeCelestialDir = lerp(sunDir, moonDir, _DayNightBlend);
                    float NdotL = saturate(dot(cloudNormal, activeCelestialDir));

                    // Three-tone Cel bands: Highlight -> Lit Body -> Ambient Shadow
                    float celHighlight = smoothstep(0.62, 0.70, NdotL);
                    float celBody = smoothstep(0.28, 0.36, NdotL);

                    // Active highlight color based on period
                    float isSunset = saturate(1.0 - abs(sunDir.y) * 2.8) * (1.0 - _DayNightBlend);
                    half3 sunHighlight = lerp(_CloudSunHighlight.rgb, _CloudDuskHighlight.rgb, isSunset);
                    half3 activeHighlight = lerp(sunHighlight, _CloudMoonHighlight.rgb, _DayNightBlend);
                    if (_BossNightBlend > 0.01)
                    {
                        activeHighlight = lerp(activeHighlight, half3(1.0, 0.32, 0.22), _BossNightBlend);
                    }

                    // Shadow tone harmonized with sky atmosphere
                    half3 dayShadow = _CloudShadowColor.rgb;
                    half3 nightShadow = lerp(_NightHorizonColor.rgb * 0.75, half3(0.20, 0.05, 0.08), _BossNightBlend);
                    half3 ambientCloudShadow = lerp(dayShadow, nightShadow, _DayNightBlend);

                    // Lit body color (with sunset warm apricot tinting at dusk)
                    half3 litBodyColor = lerp(_CloudBaseColor.rgb, _CloudDuskHighlight.rgb * 0.95, isSunset * 0.65);

                    // Combine cel bands
                    half3 cloudTone = lerp(ambientCloudShadow, litBodyColor, celBody);
                    cloudTone = lerp(cloudTone, activeHighlight, celHighlight);

                    // Forward scattering / Silver lining fringe when viewing clouds against Sun or Moon
                    float sunAlign = saturate(dot(viewDir, sunDir));
                    float moonAlign = saturate(dot(viewDir, moonDir));
                    float celestialAlign = lerp(sunAlign, moonAlign, _DayNightBlend);
                    float silverLining = pow(celestialAlign, 6.0) * smoothstep(activeCutoff, activeCutoff + 0.15, d0) * (1.0 - smoothstep(activeCutoff + 0.18, activeCutoff + 0.38, d0));
                    cloudTone += activeHighlight * (silverLining * 1.35);

                    // Smooth horizon fade to seamlessly integrate with distant mountains and fog
                    float horizonFade = saturate(viewDir.y * 4.2);
                    float totalCloudAlpha = cloudMask * horizonFade * activeOpacity;

                    // Composite cloud deck over sky and stars (clouds softly veil or clear for background stars)
                    skyColor = lerp(skyColor, cloudTone, totalCloudAlpha);
                }

                return half4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
