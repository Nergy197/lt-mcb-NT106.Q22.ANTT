Shader "Custom/UITexture_Rounded"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Corner Radius (0-0.5)", Range(0,0.5)) = 0.1
        _AspectRatio ("Aspect Ratio (W/H)", Float) = 1.0
        
        // Cần thiết cho UI Canvas
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        
        // Cấu hình Stencil cho UI
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        ColorMask [_ColorMask]

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // UI color
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Radius;
            float _AspectRatio;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // --- Tính toán bo góc ---
                // Chuyển đổi UV sang hệ tọa độ trung tâm (-0.5 đến 0.5)
                float2 centered_uv = i.uv - 0.5;
                
                // Điều chỉnh theo tỷ lệ khung hình
                centered_uv.x *= _AspectRatio;
                
                // Tính toán khoảng cách đến các góc
                float2 d = abs(centered_uv) - (0.5 * float2(_AspectRatio, 1.0) - _Radius);
                float dist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
                
                // Tạo mask bo góc (Antialiasing nhẹ)
                float4 rounded_mask = float4(1,1,1, smoothstep(0.0, 0.005, _Radius - dist));
                
                return col * rounded_mask;
            }
            ENDCG
        }
    }
}