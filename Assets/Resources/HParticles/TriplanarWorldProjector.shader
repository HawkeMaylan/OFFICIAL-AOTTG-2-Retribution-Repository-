Shader "Custom/TriplanarWorldProjector"
{
    Properties
    {
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _DecalColor ("Decal Color", Color) = (1,1,1,1)
        _ProjectionCenter ("Projection Center", Vector) = (0,0,0,0)
        _ProjectionSize ("Projection Size (Box)", Vector) = (1,1,1,0)
        _Falloff ("Edge Falloff", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _DecalTex;
            float4 _DecalColor;
            float3 _ProjectionCenter;
            float3 _ProjectionSize;
            float _Falloff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 localPos = (i.worldPos - _ProjectionCenter) / _ProjectionSize;
                if (abs(localPos.x) > 0.5 || abs(localPos.y) > 0.5 || abs(localPos.z) > 0.5)
                    discard;

                float3 blend = abs(normalize(i.worldNormal));
                blend = pow(blend, 4.0); // Sharpen blending
                blend /= (blend.x + blend.y + blend.z);

                float2 xUV = i.worldPos.yz / _ProjectionSize.yz;
                float2 yUV = i.worldPos.xz / _ProjectionSize.xz;
                float2 zUV = i.worldPos.xy / _ProjectionSize.xy;

                fixed4 xTex = tex2D(_DecalTex, xUV);
                fixed4 yTex = tex2D(_DecalTex, yUV);
                fixed4 zTex = tex2D(_DecalTex, zUV);

                fixed4 tex = xTex * blend.x + yTex * blend.y + zTex * blend.z;
                tex *= _DecalColor;

                // Optional falloff
                float dist = length(localPos);
                float fade = saturate(1.0 - dist * _Falloff);
                tex.a *= fade;

                return tex;
            }
            ENDCG
        }
    }

    FallBack Off
}
