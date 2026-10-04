Shader "Duskborn/RuneSurface"
{
    Properties { _RunesA("Flame Venom Storm Stone", Vector)=(0,0,0,0) _RunesB("Frost Blood Hex Radiance", Vector)=(0,0,0,0) _ActiveCount("Active count", Float)=1 _Intensity("Intensity", Range(0,1))=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _RunesA, _RunesB;
            float4x4 _RuneWorldToLocal;
            float _ActiveCount, _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionOS:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                // Imported mesh scales vary dramatically: offset in meters, never mesh units.
                o.positionWS=p.positionWS + normalize(o.normalWS)*.0015;
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.positionOS=mul(_RuneWorldToLocal,float4(p.positionWS,1)).xyz;
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            half4 frag(Varyings i):SV_Target
            {
                float t=_Time.y;
                float3 p=i.positionOS;
                float rim=pow(1-saturate(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS)))),1.4);
                float n=hash(floor(p*22));
                float fire=saturate(sin(p.y*17-t*8+n*5)*.65+.35)*(rim*.55+.45);
                float poison=pow(saturate(sin(p.x*21+p.z*18+t*2+n*3)),6);
                float storm=pow(saturate(sin(p.y*33+p.x*21+sin(t*19)*4)),24);
                float stone=1-smoothstep(.04,.12,abs(frac((p.x+p.y*.6+p.z)*12+n*.3)-.5));
                float frost=(.15+.85*pow(saturate(sin((p.x-p.y+p.z)*25)),6))*(.3+rim*.7);
                float blood=pow(saturate(sin(p.x*32+p.z*23)),8)*saturate(sin(p.y*9+t*4));
                float hex=pow(saturate(cos(p.y*24-t*2)),18)*pow(saturate(sin((p.x+p.z)*16+t)),4);
                float light=pow(saturate(sin((p.x+p.y+p.z)*19-t*3)),14)*(.25+rim);
                float strengths[8]={_RunesA.x,_RunesA.y,_RunesA.z,_RunesA.w,_RunesB.x,_RunesB.y,_RunesB.z,_RunesB.w};
                float patterns[8]={fire,poison,storm,stone,frost,blood,hex,light};
                float3 colors[8]={float3(1,.27,.025),float3(.25,.95,.07),float3(.2,.55,1),float3(.62,.54,.4),float3(.25,.9,1),float3(.9,.025,.09),float3(.68,.15,1),float3(1,.83,.25)};
                // Select the strongest local pattern instead of adding colors into white.
                // Stable spatial offsets give each family a separate region on a crowded target.
                float weight=0; float3 color=0;
                for(int k=0;k<8;k++)
                {
                    float lane=pow(saturate(sin(p.y*4+p.x*3+k*2.39996)*.5+.5),2);
                    lane=lerp(1,.2+.8*lane,step(1.5,_ActiveCount));
                    float w=step(.5,strengths[k])*max(patterns[k],rim*.3)*lane*(.6+.4*saturate(strengths[k]/8));
                    if(w>weight){weight=w;color=colors[k];}
                }
                return half4(color*1.6,saturate(weight*.78)*_Intensity);
            }
            ENDHLSL
        }
    }
}
