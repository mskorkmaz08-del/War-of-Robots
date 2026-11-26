// Ground and mountains: three photo-scanned layers blended per vertex.
//   Base  : the main ground (dirt, asphalt, sand, snow), projected from above
//   Patch : a second ground (rubble, gravel, drifts) in patches, from above
//   Rock  : cliff rock on steep slopes, projected from three sides
// Vertex colour: R = patch weight, G = rock weight, B = darkening (1 = none).
// A second, much larger sample of the base layer breaks up visible tiling.
Shader "RobotArena/Terrain"
{
    Properties
    {
        _BaseTex ("Base albedo", 2D) = "white" {}
        _BaseMask ("Base rough (R) AO (G)", 2D) = "white" {}
        _BaseN ("Base normal", 2D) = "bump" {}
        _BaseColor ("Base tint", Color) = (1,1,1,1)
        _BaseTiling ("Base metres per tile", Float) = 6

        _PatchTex ("Patch albedo", 2D) = "white" {}
        _PatchMask ("Patch rough (R) AO (G)", 2D) = "white" {}
        _PatchN ("Patch normal", 2D) = "bump" {}
        _PatchColor ("Patch tint", Color) = (1,1,1,1)
        _PatchTiling ("Patch metres per tile", Float) = 5

        _RockTex ("Rock albedo", 2D) = "white" {}
        _RockMask ("Rock rough (R) AO (G)", 2D) = "white" {}
        _RockN ("Rock normal", 2D) = "bump" {}
        _RockColor ("Rock tint", Color) = (1,1,1,1)
        _RockTiling ("Rock metres per tile", Float) = 14

        _BumpScale ("Normal strength", Float) = 1.2
        _Macro ("Large-scale variation", Range(0,1)) = 0.55
        _Wet ("Smoothness scale", Range(0,1.5)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _BaseTex, _BaseMask, _BaseN, _PatchTex, _PatchMask, _PatchN, _RockTex, _RockMask, _RockN;
        fixed4 _BaseColor, _PatchColor, _RockColor;
        half _BaseTiling, _PatchTiling, _RockTiling, _BumpScale, _Macro, _Wet;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Height-field meshes built in code: make a valid tangent from the normal.
            float3 up = abs(v.normal.y) < 0.99 ? float3(0,1,0) : float3(1,0,0);
            v.tangent = float4(normalize(cross(up, v.normal)), 1);
        }

        half3 UnpackGL(half4 c)
        {
            half3 n;
            n.xy = (c.xy * 2 - 1) * _BumpScale;
            n.z = sqrt(saturate(1 - dot(n.xy, n.xy)));
            return n;
        }

        half3 WorldToTangentNormal(Input IN, half3 n)
        {
            half3 t2w0 = WorldNormalVector(IN, half3(1,0,0));
            half3 t2w1 = WorldNormalVector(IN, half3(0,1,0));
            half3 t2w2 = WorldNormalVector(IN, half3(0,0,1));
            half3x3 t2w = half3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, n));
        }

        // Projection from above, whiteout-blended onto the surface normal (world space).
        half3 TopNormal(sampler2D tex, float2 uv, half3 n)
        {
            half3 t = UnpackGL(tex2D(tex, uv));
            t = half3(t.xy + n.xz, abs(t.z) * n.y);
            return normalize(t.xzy);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half3 n = normalize(WorldNormalVector(IN, half3(0,0,1)));
            float3 p = IN.worldPos;
            half4 vc = IN.color;

            // ---- Base layer, with a large rotated sample to hide the tiling ----
            float2 uvB = p.xz / _BaseTiling;
            half3 baseCol = tex2D(_BaseTex, uvB).rgb;
            float2 uvM = float2(p.x * 0.8 - p.z * 0.6, p.x * 0.6 + p.z * 0.8) / (_BaseTiling * 9.0);
            half3 macro = tex2D(_BaseTex, uvM).rgb;
            half3 avg = tex2Dlod(_BaseTex, float4(0.5, 0.5, 0, 12)).rgb;
            half macroL = dot(macro, half3(0.3, 0.59, 0.11)) / max(dot(avg, half3(0.3, 0.59, 0.11)), 0.05);
            baseCol *= lerp(1, clamp(macroL, 0.6, 1.4), _Macro);
            baseCol *= _BaseColor.rgb;
            half4 baseMask = tex2D(_BaseMask, uvB);
            half3 baseN = TopNormal(_BaseN, uvB, n);

            // ---- Patch layer, with a ragged edge driven by its own AO ----
            float2 uvP = p.xz / _PatchTiling;
            half3 patchCol = tex2D(_PatchTex, uvP).rgb * _PatchColor.rgb;
            half4 patchMask = tex2D(_PatchMask, uvP);
            half3 patchN = TopNormal(_PatchN, uvP, n);
            half wp = saturate((vc.r - 0.5) * 3 + (patchMask.g - 0.5) * 1.5 + 0.5);
            wp = smoothstep(0.35, 0.65, wp) * step(0.02, vc.r);

            half3 col = lerp(baseCol, patchCol, wp);
            half4 mask = lerp(baseMask, patchMask, wp);
            half3 nrm = normalize(lerp(baseN, patchN, wp));

            // ---- Rock on slopes (triplanar) ----
            half wr = saturate(vc.g);
            if (wr > 0.01)
            {
                float3 q = p / _RockTiling;
                half3 w = pow(abs(n), 4);
                w /= (w.x + w.y + w.z);
                float2 uvX = q.zy, uvY = q.xz, uvZ = q.xy;
                half3 rockCol = (tex2D(_RockTex, uvX).rgb * w.x + tex2D(_RockTex, uvY).rgb * w.y + tex2D(_RockTex, uvZ).rgb * w.z) * _RockColor.rgb;
                half4 rockMask = tex2D(_RockMask, uvX) * w.x + tex2D(_RockMask, uvY) * w.y + tex2D(_RockMask, uvZ) * w.z;
                half3 tX = UnpackGL(tex2D(_RockN, uvX));
                half3 tY = UnpackGL(tex2D(_RockN, uvY));
                half3 tZ = UnpackGL(tex2D(_RockN, uvZ));
                tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
                tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
                tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
                half3 rockN = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
                // Rock shows through first where the ground layer is darkest (in its cracks).
                half edge = saturate((wr - 0.5) * 3 + (0.5 - dot(col, half3(0.3, 0.59, 0.11))) * 0.8 + 0.5);
                edge = smoothstep(0.3, 0.7, edge);
                col = lerp(col, rockCol, edge);
                mask = lerp(mask, rockMask, edge);
                nrm = normalize(lerp(nrm, rockN, edge));
            }

            o.Albedo = col * vc.b;
            o.Metallic = 0;
            o.Smoothness = saturate((1 - mask.r) * _Wet);
            o.Occlusion = mask.g;
            o.Normal = WorldToTangentNormal(IN, nrm);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
