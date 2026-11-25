Shader "Molak/SpriteInkNight"
{
    // 먹색(야간) 모드용: 검은 먹은 흰 붓으로 뒤집고, 붉은 악센트는 색을 유지한다. 일반 알파 블렌딩.
    Properties { _MainTex ("Sprite Texture", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; fixed4 _Color;
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
            v2f vert (appdata_t IN) { v2f OUT; OUT.vertex = UnityObjectToClipPos(IN.vertex); OUT.texcoord = IN.texcoord; OUT.color = IN.color * _Color; return OUT; }
            fixed4 frag (v2f IN) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, IN.texcoord);
                float lum = dot(t.rgb, float3(0.3, 0.59, 0.11));
                float sat = max(t.r, max(t.g, t.b)) - min(t.r, min(t.g, t.b));
                fixed3 ink = fixed3(1,1,1) * (1.0 - lum) * 0.92 + 0.06;   // 먹 → 밝은 붓
                fixed3 c = lerp(ink, t.rgb * 1.15, saturate(sat * 2.5));   // 붉은 악센트 유지
                fixed4 o; o.rgb = c * IN.color.rgb; o.a = t.a * IN.color.a;
                return o;
            }
            ENDCG
        }
    }
}