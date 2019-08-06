Shader "Hidden/ShiftMat"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_xOffset ("XOffset", float) = 0
		_yOffset ("YOffset", float) = 0
		_zOffset ("ZOffset", float) = 0

	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always
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
				float4 screenCoord : TEXCOORD1;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;				
				float4 screenCoord : TEXCOORD1;
			};

			float _xOffset;
			float _yOffset;
			float _zOffset;


			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv.x = v.uv.x + _xOffset;
				o.uv.y = v.uv.y + _yOffset;

		/*		o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.uv = v.uv;
				o.screenCoord = v.screenCoord;*/
				//o.uv.y = o.uv.xy/o.uv.x;
				


				o.uv -= 0.5;
				float s = sin(_zOffset);
				float c = cos(_zOffset);
				float2x2 rotationMatrix = float2x2(c, -s, s, c);
				rotationMatrix *= 0.5;
				rotationMatrix += 0.5;
				rotationMatrix = rotationMatrix * 2 - 1;
				o.uv = mul(o.uv, rotationMatrix);
				o.uv += 0.5;
				return o;
			}
			
			sampler2D _MainTex;
			float _Sample;

			float4 frag (v2f i) : SV_Target
			{
				return float4(tex2D(_MainTex, i.uv).rgb, 1.0);
			}
			ENDCG
		}
	}
}
