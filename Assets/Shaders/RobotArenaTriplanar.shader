// Physically based material for procedural geometry that has no useful UVs.
// Textures are projected from three sides (triplanar) in the object's own space,
// scaled by the object's size, so they stick to moving robots and never stretch.
//   _MainTex : albedo (multiplied by _Color where painted, by _BareColor where chipped)
//   _MaskTex : R = roughness, G = ambient occlusion, B = paint mask (1 painted, 0 bare metal)
//   _BumpMap : OpenGL-style normal map
Shader "RobotArena/Triplanar"
{
    Properties
    {
        _Color ("Paint colour", Color) = (1,1,1,1)
        _BareColor ("Bare metal colour", Color) = (0.55,0.56,0.58,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _MaskTex ("Rough (R) AO (G) Paint (B)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _Tiling ("Metres per texture tile", Float) = 4
        _Metallic ("Paint metallic", Range(0,1)) = 0.2
        _Glossiness ("Smoothness scale", Range(0,1.5)) = 1
        _AOStrength ("AO strength", Range(0,1)) = 0.7
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Grime ("Grime and rain streaks", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex, _MaskTex, _BumpMap;
        fixed4 _Color, _BareColor, _EmissionColor;
        half _BumpScale, _Tiling, _Metallic, _Glossiness, _AOStrength, _Grime;

        struct Input
        {
            float3 objPos;
            float3 objNormal;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Object scale, so a 10 m wall gets ten times the texture of a 1 m cube.
            float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                                  length(unity_ObjectToWorld._m01_m11_m21),
                                  length(unity_ObjectToWorld._m02_m12_m22));
            o.objPos = v.vertex.xyz * scale;
            o.objNormal = v.normal;
            // Meshes built in code may lack tangents: make a valid one from the normal.
            float3 up = abs(v.normal.y) < 0.99 ? float3(0,1,0) : float3(1,0,0);
            v.tangent = float4(normalize(cross(up, v.normal)), 1);
        }

        half3 WorldToTangentNormal(Input IN, half3 n)
        {
            half3 t2w0 = WorldNormalVector(IN, half3(1,0,0));
            half3 t2w1 = WorldNormalVector(IN, half3(0,1,0));
            half3 t2w2 = WorldNormalVector(IN, half3(0,0,1));
            half3x3 t2w = half3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, n));
        }

        half3 UnpackGL(half4 c)
        {
            half3 n;
            n.xy = (c.xy * 2 - 1) * _BumpScale;
            n.z = sqrt(saturate(1 - dot(n.xy, n.xy)));
            return n;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.objPos / max(_Tiling, 0.01);
            float3 n = normalize(IN.objNormal);
            half3 w = pow(abs(n), 4);
            w /= (w.x + w.y + w.z);

            float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;

            half4 albedo = tex2D(_MainTex, uvX) * w.x + tex2D(_MainTex, uvY) * w.y + tex2D(_MainTex, uvZ) * w.z;
            half4 mask = tex2D(_MaskTex, uvX) * w.x + tex2D(_MaskTex, uvY) * w.y + tex2D(_MaskTex, uvZ) * w.z;

            // Whiteout-blended triplanar normals, in object space.
            half3 tX = UnpackGL(tex2D(_BumpMap, uvX));
            half3 tY = UnpackGL(tex2D(_BumpMap, uvY));
            half3 tZ = UnpackGL(tex2D(_BumpMap, uvZ));
            tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
            tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
            tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
            half3 objN = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
            half3 worldN = normalize(UnityObjectToWorldNormal(objN));

            half paint = mask.b;
            // Weathering: vertical rain/oil streaks from a stretched sample of the
            // AO map, and dirt collecting on surfaces that face down. Big machines
            // look used and heavy; clean even paint reads as a plastic toy.
            half streak = tex2D(_MaskTex, float2((p.x + p.z) * 0.9, p.y * 0.12)).g;
            half grime = lerp(1, 0.5 + 0.5 * streak, _Grime);
            grime *= 1 - 0.35 * _Grime * saturate(-worldN.y);
            o.Albedo = albedo.rgb * lerp(_BareColor.rgb, _Color.rgb, paint) * grime;
            o.Metallic = lerp(0.85, _Metallic, paint);
            o.Smoothness = saturate((1 - mask.r) * _Glossiness) * lerp(1, 0.75, _Grime * (1 - streak));
            o.Occlusion = lerp(1, mask.g, _AOStrength);
            o.Normal = WorldToTangentNormal(IN, worldN);
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
