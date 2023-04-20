    //
    // Light projection strong schader,
    // based on Unity Standard ProjectionLight shader
    // shader's coolness was improved by C. Franz
    //
     
    Shader "Projector/LightStrong" {
        Properties {
            _Color ("Main Color", Color) = (1,1,1,1)
            _MainTex ("Cookie", 2D) = "" {}
            _Intensity ("Intensity", Float) = 5.0
        }
     
        Subshader {
            Tags {"Queue"="Transparent"}
            Pass {
                ZWrite Off
                ColorMask RGB
                Blend SrcAlpha OneMinusSrcAlpha //Traditional Transparency, WAS: Blend DstColor One
                Offset -1, -1
     
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_fog
                #include "UnityCG.cginc"
             
                struct v2f {
                    float4 uvShadow : TEXCOORD0;
                    UNITY_FOG_COORDS(2)
                    float4 pos : SV_POSITION;
                };
             
                float4x4 unity_Projector;
                float4x4 unity_ProjectorClip;
             
                v2f vert (float4 vertex : POSITION)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(vertex);
                    o.uvShadow = mul (unity_Projector, vertex);
                    UNITY_TRANSFER_FOG(o,o.pos);
                    return o;
                }
             
                fixed4 _Color;
                sampler2D _MainTex;
                Float _Intensity;
             
                fixed4 frag (v2f i) : SV_Target
                {
                    fixed4 texS = tex2Dproj (_MainTex, UNITY_PROJ_COORD(i.uvShadow));
                    texS.rgba *= (_Intensity * _Color.rgba) ;// WAS: texS.rgb *= Color.rgb -- we now use alpha and intensity
     
     
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, texS, fixed4(0,0,0,0)); // was: UNITY_APPLY_FOG_COLOR(i.fogCoord, res, fixed4(0,0,0,0));
                    return texS;// was: return res
                }
                ENDCG
            }
        }
    }
