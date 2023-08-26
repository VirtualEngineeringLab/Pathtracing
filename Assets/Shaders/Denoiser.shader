// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Denoiser" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" { }
	}
		SubShader{

		ZTest Always Cull Off ZWrite Off Fog{ Mode Off }

		Pass{
		CGPROGRAM

	#pragma vertex vert
	#pragma fragment frag
	#include "UnityCG.cginc"

		sampler2D _MainTex;
		float4 _MainTex_TexelSize;
		float step_w;
		float step_h;

	struct v2f {
		float4  pos : SV_POSITION;
		float2  uv : TEXCOORD0;
	};

	float4 _MainTex_ST;
	float4 _MainTex_ST_TexelSize;

	v2f vert(appdata_base v) {
		v2f o;
		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
		return o;
	}

	// Adapted from https://www.shadertoy.com/view/ldKBRV
	// Edge-Avoiding À-TrousWavelet Transform for denoising
	// implemented on https://www.shadertoy.com/view/ldKBzG
	// feel free to use it

	// here, the denoising kernel stride ranges from 1.0 to 4.0
	#define DENOISE_RANGE float2(1, 4)

	float hash1(float seed) {
		return frac(sin(seed)*43758.5453123);
	}

	float4 denoiser(float2 fragCoord) 
	{    
		
		float2 offset[25];
		offset[0] = float2(-2,-2);
		offset[1] = float2(-1,-2);
		offset[2] = float2(0,-2);
		offset[3] = float2(1,-2);
		offset[4] = float2(2,-2);
		
		offset[5] = float2(-2,-1);
		offset[6] = float2(-1,-1);
		offset[7] = float2(0,-1);
		offset[8] = float2(1,-1);
		offset[9] = float2(2,-1);
		
		offset[10] = float2(-2,0);
		offset[11] = float2(-1,0);
		offset[12] = float2(0,0);
		offset[13] = float2(1,0);
		offset[14] = float2(2,0);
		
		offset[15] = float2(-2,1);
		offset[16] = float2(-1,1);
		offset[17] = float2(0,1);
		offset[18] = float2(1,1);
		offset[19] = float2(2,1);
		
		offset[20] = float2(-2,2);
		offset[21] = float2(-1,2);
		offset[22] = float2(0,2);
		offset[23] = float2(1,2);
		offset[24] = float2(2,2);
		
		
		float kernel[25];
		kernel[0] = 1.0f/256.0f;
		kernel[1] = 1.0f/64.0f;
		kernel[2] = 3.0f/128.0f;
		kernel[3] = 1.0f/64.0f;
		kernel[4] = 1.0f/256.0f;
		
		kernel[5] = 1.0f/64.0f;
		kernel[6] = 1.0f/16.0f;
		kernel[7] = 3.0f/32.0f;
		kernel[8] = 1.0f/16.0f;
		kernel[9] = 1.0f/64.0f;
		
		kernel[10] = 3.0f/128.0f;
		kernel[11] = 3.0f/32.0f;
		kernel[12] = 9.0f/64.0f;
		kernel[13] = 3.0f/32.0f;
		kernel[14] = 3.0f/128.0f;
		
		kernel[15] = 1.0f/64.0f;
		kernel[16] = 1.0f/16.0f;
		kernel[17] = 3.0f/32.0f;
		kernel[18] = 1.0f/16.0f;
		kernel[19] = 1.0f/64.0f;
		
		kernel[20] = 1.0f/256.0f;
		kernel[21] = 1.0f/64.0f;
		kernel[22] = 3.0f/128.0f;
		kernel[23] = 1.0f/64.0f;
		kernel[24] = 1.0f/256.0f;
		
		float3 sum = float3(0.0,0.0,0.0);
		float3 sum_f = float3(0.0,0.0,0.0);
		float c_phi = 1.0;
		float r_phi = 1.0;
		float n_phi = 0.5;
		float p_phi = 0.25;
		
		// float3 cval = texelFetch(iChannel0, ifloat2(fragCoord), 0).xyz;    
		float3 cval = tex2D(_MainTex,fragCoord).xyz;
		float3 rval = tex2D(_MainTex,fragCoord).xyz;
		float3 nval = tex2D(_MainTex,fragCoord).xyz;
		// float3 rval = texelFetch(iChannel2, ifloat2(fragCoord), 0).xyz;
		// float3 nval = texelFetch(iChannel1, ifloat2(fragCoord), 0).xyz;

		float ang = 2.0*3.1415926535*hash1(251.12860182*fragCoord.x + 729.9126812*fragCoord.y+5.1839513);
		float2x2 m = float2x2(cos(ang),sin(ang),-sin(ang),cos(ang));
		
		float cum_w = 0.0;
		float cum_fw = 0.0;
		
		float denoiseStrength = (DENOISE_RANGE.x + (DENOISE_RANGE.y-DENOISE_RANGE.x)*hash1(641.128752*fragCoord.x + 312.321374*fragCoord.y+1.92357812))*1.0;
		// uint pwidth, pheight;
		// _MainTex.GetDimensions(pwidth, pheight);
		float2 textureSize = float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
		
		for(int i=0; i<25; i++)
		{
			// float2 uv = (fragCoord+m*(offset[i]* denoiseStrength))/textureSize.xy;
			float2 uv = (fragCoord+(offset[i]* denoiseStrength/textureSize));
			
			// float3 ctmp = texture(iChannel0, uv).xyz;
			float3 ctmp = tex2D(_MainTex,uv).xyz;
			float3 t = cval - ctmp;
			float dist2 = dot(t,t);
			float c_w = min(exp(-(dist2)/c_phi), 1.0);
			
			// float3 ntmp = texture(iChannel1, uv).xyz;
			float3 ntmp = tex2D(_MainTex,uv).xyz;
			t = nval - ntmp;
			dist2 = max(dot(t,t), 0.0);
			float n_w = min(exp(-(dist2)/n_phi), 1.0);
			
			// float3 rtmp = texture(iChannel2, uv).xyz;
			float3 rtmp = tex2D(_MainTex,uv).xyz;
			t = rval - rtmp;
			dist2 = dot(t,t);
			float r_w = min(exp(-(dist2)/r_phi), 1.0);
			
			// new denoised frame
			float weight0 = c_w*n_w;
			sum += ctmp*weight0*kernel[i];
			cum_w += weight0*kernel[i];
			
			// denoise the previous denoised frame again
			float weight1 = r_w*n_w;
			sum_f += rtmp*weight1*kernel[i];
			cum_fw += weight1*kernel[i];
		}
		
		// mix in more of the just-denoised frame if it differs significantly from the
		// frame from feedback
		float3 ptmp = float3(0.0,0.0,0.0);
		// float3 ptmp = texture(iChannel2, fragCoord/iResolution.xy).xyz;
		float3 t = sum/cum_w - ptmp;
		float dist2 = dot(t,t);
		float p_w = min(exp(-(dist2)/p_phi), 1.0);
		
		return clamp(float4(lerp(sum/cum_w,sum_f/cum_fw,p_w),0.0),0.0,1.0);
	}

	fixed4 frag(v2f i) : COLOR{
		// step_w = _MainTex_TexelSize.x;
		// step_h = _MainTex_TexelSize.y;

		// float2 offset[9] = {
			
		// 	  float2(-step_w, -step_h),      float2(0.0, -step_h),     float2(step_w, -step_h),  
		// 	        float2(-step_w, 0.0),          float2(0.0, 0.0),         float2(step_w, 0.0),      
		// 	    float2(-step_w, step_h),       float2(0.0, step_h),      float2(step_w, step_h)

		// };

		// float kernel[9] = {


		// 	   0.059912,    0.094907,    0.059912,   
		// 	   0.094907,    0.150342,    0.094907,  
		// 	    0.059912,    0.094907,    0.059912
		// };

		// float4 sum = float4(0.0, 0.0, 0.0, 0.0);

		// for (int j = 0; j < 9; j++) {
		// 	float4 tmp = tex2D(_MainTex, i.uv + offset[j]);
		// 	sum += tmp * kernel[j]*1.33;
		// }

		// return sum;
		return denoiser(i.uv);
	}

	ENDCG //Shader End
	}

	}

}

