Shader "Duskborn/Pixelation"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // x = blocks across, y = blocks down, z = effect intensity
            float4 _PixelParams;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Snap UV to the center of a coarse grid cell to get the blocky look.
                float2 blockUV = (floor(uv * _PixelParams.xy) + 0.5) / _PixelParams.xy;

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 pixelated = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, blockUV);

                return lerp(original, pixelated, saturate(_PixelParams.z));
            }
            ENDHLSL
        }
    }
}
