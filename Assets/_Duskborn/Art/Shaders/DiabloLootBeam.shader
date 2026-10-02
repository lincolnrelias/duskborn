Shader "Duskborn/VFX/DiabloLootBeam"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Energy Stream Speed", Float) = 2.2
        _WaveFrequency ("Wave Frequency", Float) = 3.5
        _WaveAmplitude ("Wave Amplitude", Float) = 0.20
        _CoreIntensity ("White Core Intensity", Float) = 0.75
        _TopFadePower ("Top Dissipation Power", Float) = 1.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
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
            Name "DiabloLootBeamPass"
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
                float  _ScrollSpeed;
                float  _WaveFrequency;
                float  _WaveAmplitude;
                float  _CoreIntensity;
                float  _TopFadePower;
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
                // 1. Lateral distance from the column center (0 at the center, 1 at the edge).
                float lateral = abs(input.uv.x - 0.5) * 2.0;

                // 2. Smooth lateral Gaussian falloff (eliminates hard geometric edges).
                float lateralFalloff = exp(-3.8 * lateral * lateral);

                // 3. Continuous upward energy flow toward the sky (Diablo liquid plasma style).
                float timeVal = _Time.y * _ScrollSpeed;
                float wave1 = sin((input.uv.y * _WaveFrequency - timeVal) * 6.2831853);
                float wave2 = sin((input.uv.y * (_WaveFrequency * 1.8) - timeVal * 1.4) * 6.2831853) * 0.5;
                float energyFlow = 1.0 + _WaveAmplitude * (wave1 + wave2);

                // 4. White incandescent core at the column center.
                float coreMask = pow(saturate(1.0 - lateral * 1.6), 5.0) * _CoreIntensity;
                half3 finalRGB = lerp(input.color.rgb, half3(1.0, 1.0, 1.0), coreMask);

                // 5. Vertical attenuation:
                // Base: quick fade-in over the first centimeters for a smooth anchor.
                // Top: smooth fade into the atmospheric sky.
                float baseFade = saturate(input.uv.y * 22.0);
                float skyFade  = pow(saturate(1.0 - input.uv.y), _TopFadePower);

                float combinedAlpha = lateralFalloff * energyFlow * baseFade * skyFade * input.color.a;

                // Blend aditivo (Blend One One)
                return half4(finalRGB * combinedAlpha, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
