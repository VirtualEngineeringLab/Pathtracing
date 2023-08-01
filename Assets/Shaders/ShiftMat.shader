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
			sampler2D _MainTex;
			float _Sample;


			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv.x = 1.0-v.uv.x ;//+ _xOffset
				o.uv.y = 1.0-v.uv.y ;//+ _yOffset
				//o.uv = v.uv;

				//float x1 = 0, x2 = 1, x3 = 1, x4 = 0;
				//float y1 = 1, y2 = 0.75, y3 = 0.25, y4 = 0;

				//float2 uv = o.uv;
				//o.uv.x += (x4 - x1) * uv.y - x4;
				//o.uv.x /= (x3 - x4 + uv.y * (x2 - x1 - x3 + x4));
				//o.uv.y += (y4 - y3) * uv.x - y4;
				//o.uv.y /= (y1 - y4 + uv.x * (y2 - y3 - y1 + y4));

				//float3 one;
				//float3 two;
				//float3 three;
				//float3 four;

				//float ax = x3 - x1;
				//float ay = y3 - y1;
				//float bx = x4 - x2;
				//float by = y4 - y2;
				//float cross = ax * by - ay * bx;

				//if (cross != 0) {
				//	float cy = y1 - y2;
				//	float cx = x1 - x2;

				//	float s = (ax * cy - ay * cx) / cross;

				//	if (s > 0 && s < 1) {
				//		float t = (bx * cy - by * cx) / cross;

				//		if (t > 0 && t < 1) {
				//			float q0 = 1 / (1 - t);
				//			float q1 = 1 / (1 - s);
				//			float q2 = 1 / t;
				//			float q3 = 1 / s;

				//			// you can now pass (u * q, v * q, q) to OpenGL

				//			one = (q0*x1, q0*y1, q0);
				//			two = (q1*x2, q1*y2, q1);
				//			three = (q2*x3, q2*y3, q2);
				//			four = (q3*x4, q3*y4, q3);
				//		}
				//	}
				//}

				//float3 q = v.vertex - one;
				//float3 b1 = two - one;
				//float3 b2 = three - one;
				//float3 b3 = one - two - three + four;

				//o.uv = one;



				// o.uv -= 0.5;
				// float s = sin(_zOffset);
				// float c = cos(_zOffset);
				// float2x2 rotationMatrix = float2x2(c, -s, s, c);
				// rotationMatrix *= 0.5;
				// rotationMatrix += 0.5;
				// rotationMatrix = rotationMatrix * 2 - 1;
				// o.uv = mul(o.uv, rotationMatrix);
				// o.uv += 0.5;
				return o;
			}
			
			
			float4 frag (v2f i) : SV_Target
			{
				return float4(tex2D(_MainTex, i.uv).rgb, 1.0);
			}
			ENDCG
		}
	}
}
