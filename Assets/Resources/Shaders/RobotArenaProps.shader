// Town and countryside props (houses, cars, trees, lamps, roads) merged into a few
// big meshes. Colour comes from the vertex colour, smoothness from vertex alpha;
// a triplanar concrete/grime texture in world metres adds surface detail so the
// props never look like flat plastic. Lives in Resources so it is always in builds.
Shader "RobotArena/Props"
{
    Properties
    {
        _MainTex ("Detail albedo", 2D) = "white" {}
        _MaskTex ("Rough (R) AO (G)", 2D) = "white" {}
        _Tiling ("Metres per texture tile", Float) = 4
        _Detail ("Texture strength", Range(0,1)) = 0.55
        _AOStrength ("AO strength", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex, _MaskTex;
        half _Tiling, _Detail, _AOStrength;

        struct Input
        {
            float3 wPos;
            float3 wNrm;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.wNrm = UnityObjectToWorldNormal(v.normal);
            o.color = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.wPos / max(_Tiling, 0.01);
            half3 w = pow(abs(normalize(IN.wNrm)), 4);
            w /= (w.x + w.y + w.z);
            half4 albedo = tex2D(_MainTex, p.zy) * w.x + tex2D(_MainTex, p.xz) * w.y + tex2D(_MainTex, p.xy) * w.z;
            half4 mask = tex2D(_MaskTex, p.zy) * w.x + tex2D(_MaskTex, p.xz) * w.y + tex2D(_MaskTex, p.xy) * w.z;
            half lum = dot(albedo.rgb, half3(0.3, 0.59, 0.11));
            half detail = lerp(1, lum * 1.6, _Detail);
            // Rain streaks and dirt collect low on walls.
            half low = saturate(1 - (IN.wPos.y - 0.2) * 0.35) * 0.25;
            o.Albedo = IN.color.rgb * detail * (1 - low * saturate(1 - abs(IN.wNrm.y)));
            o.Metallic = 0;
            o.Smoothness = IN.color.a * lerp(1, 1 - mask.r * 0.5, _Detail);
            o.Occlusion = lerp(1, mask.g, _AOStrength);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
