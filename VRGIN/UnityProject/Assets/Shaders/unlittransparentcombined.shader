Shader "UnlitTransparentCombined" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_SubTex ("Base (RGB)", 2D) = "white" {}
	}

    SubShader {
        Tags {"Queue"="Transparent+1000"  "IgnoreProjector"="True"}
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            sampler2D _SubTex;

            struct v2f {
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 pos : SV_POSITION;
            };

            v2f vert(
                float4 vertex : POSITION,
                float2 uv0 : TEXCOORD0,
                float2 uv1 : TEXCOORD1)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv0 = uv0;
                o.uv1 = uv1;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 texval0 = tex2D(_MainTex, i.uv0);
                float4 texval1 = tex2D(_SubTex, i.uv1);
                // If the UI texture has an alpha value near 0.5, it is
                // impossible to tell whether it is a genuinely transparent
                // UI element or just an artifact of a semi-transparent
                // topmost layer drawn in front of a solid layer.
                // We use some crude heuristic here to try to bump the alpha
                // to 1 in the latter case.
                float alpha0 = 0.5 < texval0.a ? 1 : texval0.a;
                float alpha1 = 0.5 < texval1.a ? 1 : texval1.a;
                float total_alpha = 1 - (1 - alpha0) * (1 - alpha1);
                float4 color = lerp(texval0, texval1, alpha1 / total_alpha);
                clip(total_alpha - 0.01);
                return float4(color.rgb, total_alpha);
            }
            ENDCG
        }
    }
}
