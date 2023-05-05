Shader "Hidden/AddMat"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
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
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			sampler2D _MainTex;
			float _Sample;

			float4 frag(v2f i) : SV_Target
			{
				//float4 rgba = ;
				// if(tex2D(_MainTex, i.uv).r<0.1 && tex2D(_MainTex, i.uv).g<0.1 && tex2D(_MainTex, i.uv).b<0.1){
				// 	return float4(tex2D(_MainTex, i.uv).aaa, 0);
				// }


				// float alpha = 1.0;
				// if(tex2D(_MainTex, i.uv).r == 0.0 && tex2D(_MainTex, i.uv).g == 0.0 && tex2D(_MainTex, i.uv).b == 0.0){
				// 	alpha=0;
				// }
				float alpha = tex2D(_MainTex, i.uv).a;
				float firefly = 0.8;
				if(tex2D(_MainTex, i.uv).r>firefly||tex2D(_MainTex, i.uv).g>firefly||tex2D(_MainTex, i.uv).b>firefly){
					 alpha = 0.1;
				}

				float t = 1.0+_Sample*alpha;
				return float4(tex2D(_MainTex, i.uv).rgb, 1.0-1.0/t);

				
				//return float4(tex2D(_MainTex, i.uv).rgb, tex2D(_MainTex, i.uv).a/t);
				//return lerp(tex2D(_MainTex, i.uv).rgba, tex2D(SV_Target, i.uv).rgba, 1/t);
			}
			ENDCG
		}
	}
}
