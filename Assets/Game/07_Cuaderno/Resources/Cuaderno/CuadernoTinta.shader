// Dibuja papel, renglones y letras sobre las texturas de página con GL.
Shader "Hidden/Gamejam/CuadernoTinta"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AlphaOnly ("Alpha Only", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _AlphaOnly;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                if (_AlphaOnly > 0.5) return fixed4(i.color.rgb, i.color.a * t.a);
                return t * i.color;
            }
            ENDCG
        }
    }
}
