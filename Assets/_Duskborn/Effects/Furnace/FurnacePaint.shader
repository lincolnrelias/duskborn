Shader "Duskborn/Furnace Painted Effects"
{
    Properties
    {
        _BaseMap ("Painted smoke atlas", 2D) = "white" {}
        _Mode ("0 Fire, 1 Smoke, 2 Coals", Float) = 0
        _Heat ("Heat", Range(0,2)) = 0
        _Phase ("Tongue phase", Float) = 0
        _AnimationTime ("Local animation clock", Float) = 0
        _Tint ("Amber tint", Color) = (1,.62,.18,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 10
        [Toggle] _ZWrite ("Depth write", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _Tint;
                float _Mode, _Heat, _Phase, _SrcBlend, _DstBlend, _ZWrite, _AnimationTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 local : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 p = v.positionOS.xyz;
                if (_Mode < .5)
                {
                    float y = v.uv.y, time = _AnimationTime;
                    p.y *= (.82 + .12 * sin(time * 3.1 + _Phase) + .08 * sin(time * 5.3 + _Phase * 1.7));
                    p.y *= lerp(.25, 1, saturate(_Heat));
                    p.x += y * y * .28 * sin(time * 2.6 - y * 3.5 + _Phase);
                    p.z += y * y * .18 * cos(time * 2.1 - y * 2 + _Phase);
                }
                o.positionCS = TransformObjectToHClip(p);
                o.uv = v.uv; o.color = v.color; o.local = p;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_Mode > 1.5)
                {
                    float vein = .5 + .5 * sin(i.local.x * 62 + sin(i.local.z * 37) * 2 + i.local.y * 29);
                    float3 coal = lerp(float3(.029,.024,.023), float3(.065,.045,.034), vein);
                    return half4(coal + _Heat * lerp(float3(.23,.025,.004), float3(.85,.16,.012), vein * vein), 1);
                }
                if (_Mode > .5)
                {
                    half4 paint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                    return half4(i.color.rgb * paint.rgb, i.color.a * paint.a);
                }
                float y = i.uv.y;
                float x = abs(i.uv.x - .5) * 2;
                float width = pow(saturate(1 - y), .72) * (.84 + .08 * sin(y * 13 - _AnimationTime * 3 + _Phase));
                float edge = 1 - smoothstep(width * .65, width, x);
                float alpha = edge * smoothstep(0, .06, y) * (1 - smoothstep(.85, 1, y)) * saturate(_Heat * 2);
                float core = (1 - smoothstep(0, .42 * (1-y), x)) * (1 - smoothstep(.15,.62,y));
                float3 body = lerp(float3(.75,.085,.009), _Tint.rgb, saturate(y * 1.8 + .25));
                float3 color = lerp(body, float3(1.25,.86,.33), core * .85);
                return half4(color * lerp(.6,1,saturate(_Heat)), alpha * .92);
            }
            ENDHLSL
        }
    }
}
