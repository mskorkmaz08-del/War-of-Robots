// Screen effects for the built-in pipeline: soft bloom on bright things
// (explosions, glowing parts, lasers), colour grading and a vignette.
Shader "Hidden/RobotArena/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _Intensity;
    float _Contrast;
    float _Saturation;
    float _Vignette;
    float4 _Tint;

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 c = tex2D(_MainTex, uv + o.xy).rgb;
        c += tex2D(_MainTex, uv + o.zy).rgb;
        c += tex2D(_MainTex, uv + o.xw).rgb;
        c += tex2D(_MainTex, uv + o.zw).rgb;
        return c * 0.25;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half brightness = max(c.r, max(c.g, c.b));
        half soft = saturate((brightness - _Threshold) / max(0.0001, 1.0 - _Threshold));
        return half4(c * soft * soft, 1.0);
    }

    half4 FragDown(v2f_img i) : SV_Target
    {
        return half4(Box4(i.uv, 1.0), 1.0);
    }

    half4 FragUp(v2f_img i) : SV_Target
    {
        return half4(Box4(i.uv, 0.5), 1.0);
    }

    half3 Grade(half3 c, float2 uv)
    {
        c = (c - 0.5) * _Contrast + 0.5;
        half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(half3(lum, lum, lum), c, _Saturation);
        c *= _Tint.rgb;
        float2 d = uv - 0.5;
        c *= saturate(1.0 - dot(d, d) * _Vignette);
        return saturate(c);
    }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        half3 b = tex2D(_BloomTex, i.uv).rgb;
        c = c + b * _Intensity;
        return half4(Grade(c, i.uv), 1.0);
    }

    half4 FragGrade(v2f_img i) : SV_Target
    {
        return half4(Grade(tex2D(_MainTex, i.uv).rgb, i.uv), 1.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0 prefilter
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }

        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }

        Pass // 2 upsample (added on top)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }

        Pass // 3 composite with bloom
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG
        }

        Pass // 4 grading only (medium quality)
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragGrade
            ENDCG
        }
    }
    Fallback Off
}
