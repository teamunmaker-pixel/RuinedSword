Shader "Molak/SpriteMultiply"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend DstColor Zero
        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };
            fixed4 _Color; float _Fade;
            v2f vert(appdata_t IN) { v2f OUT; OUT.vertex = UnityObjectToClipPos(IN.vertex); OUT.texcoord = IN.texcoord; OUT.color = IN.color * _Color; return OUT; }
            sampler2D _MainTex;
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord);
                float a = c.a * IN.color.a * _Fade;
                fixed3 ink = c.rgb * IN.color.rgb;
                return fixed4(lerp(fixed3(1,1,1), ink, a), 1);
            }
        ENDCG
        }
    }
}