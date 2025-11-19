Shader "Custom/TriplanarDecalFlatLit"
{
    Properties
    {
        _MainTex ("Decal Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Scale ("Projection Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _Scale;

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

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 wpos = i.worldPos * _Scale;

                // Triplanar blend
                float3 blend = abs(normalize(i.worldNormal));
                blend = pow(blend, 4.0);
                blend /= (blend.x + blend.y + blend.z + 1e-5);

                float4 x = tex2D(_MainTex, wpos.yz);
                float4 y = tex2D(_MainTex, wpos.xz);
                float4 z = tex2D(_MainTex, wpos.xy);

                float4 tex = x * blend.x + y * blend.y + z * blend.z;
                tex *= _Color;

                // Flat diffuse lighting based on directional light only (no view influence)
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(float3(0.3, 1, 0.3)); // Hardcoded light direction
                float NdotL = saturate(dot(normal, lightDir));
                float3 litColor = tex.rgb * NdotL;

                return float4(litColor, tex.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
