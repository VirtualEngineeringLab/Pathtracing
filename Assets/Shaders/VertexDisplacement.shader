// Upgrade NOTE: replaced '_CameraToWorld' with 'unity_CameraToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "VertexDisplacement"
{
    Properties 
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _DispTex ("Displacement Texture", 2D) = "gray" {}
        _Displacement ("Displacement", Range(0, 1.0)) = 0.1
        _ChannelFactor ("ChannelFactor (r,g,b)", Vector) = (1,0,0)
        _Range ("Range (min,max)", Vector) = (0,0.5,0)
        _ClipRange ("ClipRange [0,1]", float) = 0.8
    }

    SubShader 
    {
        Tags {
            "RenderType"="Opaque"  "Queue"="Geometry"
        } 
        Pass {
            Name "FORWARD"
            Tags {
            "LightMode"="ForwardBase"
        }

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define UNITY_PASS_FORWARDBASE
        #include "UnityCG.cginc"
        #include "AutoLight.cginc"
        #include "Lighting.cginc"
        #pragma multi_compile_fwdbase_fullshadows
        #pragma multi_compile_fog
        #pragma target 3.0
        float4 _Color;
        sampler2D _DispTex;
        float4 _DispTex_ST;
        sampler2D _MainTex;
        float4 _MainTex_ST;
        float _Displacement;
        float3 _ChannelFactor;
        float2 _Range;
        float _ClipRange;

        float4x4 clipToWorld;

        struct VertexInput {
            float4 vertex : POSITION;       //local vertex position
            float3 normal : NORMAL;         //normal direction
            float4 tangent : TANGENT;       //tangent direction    
            float2 texcoord0 : TEXCOORD0;   //uv coordinates
            float2 texcoord1 : TEXCOORD1;   //lightmap uv coordinates
        };

        struct VertexOutput {
            float4 pos : SV_POSITION;              //screen clip space position and depth
            float2 uv0 : TEXCOORD0;                //uv coordinates
            float2 uv1 : TEXCOORD1;                //lightmap uv coordinates

            //below we create our own variables with the texcoord semantic. 
            float3 normalDir : TEXCOORD3;          //normal direction   
            float3 posWorld : TEXCOORD4;          //normal direction   
            LIGHTING_COORDS(7,8)                   //this initializes the unity lighting and shadow
            UNITY_FOG_COORDS(9)                    //this initializes the unity fog
        };

        float4 ComputeClipSpacePosition(float2 positionNDC, float deviceDepth)
        {
            float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);
            /*#if UNITY_UV_STARTS_AT_TOP
            positionCS.y = -positionCS.y;
            #endif*/
            return positionCS;
        }

        float3 ComputeWorldSpacePosition(float2 positionNDC, float deviceDepth, float4x4 invViewProjMatrix)
        {
            float4 positionCS = ComputeClipSpacePosition(positionNDC, deviceDepth);
            float4 hpositionWS = mul(invViewProjMatrix, positionCS);
            return hpositionWS.xyz / hpositionWS.w;
        }    

        VertexOutput vert (VertexInput v) {
            VertexOutput o = (VertexOutput)0;           
            o.uv0 = v.texcoord0;
            o.uv1 = v.texcoord1;
            o.normalDir = UnityObjectToWorldNormal(v.normal);
            UNITY_TRANSFER_FOG(o,o.pos);
            TRANSFER_VERTEX_TO_FRAGMENT(o)
            float4 dcolor = tex2Dlod (_MainTex, float4(o.uv0 * _MainTex_ST.xy,0,0));
            float d = dcolor.a*_Displacement;
            d = d*d;
            // d = -(d * 2.0) + 1.0;
            // d = d/1024;
            // d = sqrt(d);
            d = clamp(d,0.0001,1);
            
            float3 worldspace = ComputeWorldSpacePosition(o.uv0.xy, d, clipToWorld);
            // float3 clipSpace = ComputeClipSpacePosition(o.uv0.xy, 0.01);
            // worldspace -= _WorldSpaceCameraPos;
            // float3 viewpos = UnityObjectToViewPos(v.vertex);
            // viewpos.z = d * _Displacement;
            // float3 clipPos = mul(unity_CameraProjection, viewpos).xyz;
            // // float3 clipPos = UnityObjectToClipPos(v.vertex);

            // // clipPos.z = d * _Displacement;

            // viewpos = mul(unity_CameraInvProjection, clipPos).xyz;

            // // float4x4 o2 = mul(unity_WorldToObject, unity_CameraToWorld);
            // // float4 objectPos = mul(o2, viewpos);
            // // float4 pos_view = float4(o.uv0, d * _Displacement, 1.0);
            // float3 depth_view = mul(unity_CameraInvProjection, float3(o.uv0-0.5, 100));
            
            // get linear depth from the depth
            // float sceneZ = LinearEyeDepth(_Displacement);

            // // calculate the view plane vector
            // // note: Something like normalize(i.camRelativeWorldPos.xyz) is what you'll see other
            // // examples do, but that is wrong! You need a vector that at a 1 unit view depth, not
            // // a1 unit magnitude.
            // float3 camRelativeWorldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz - _WorldSpaceCameraPos;
            // float3 viewPlane = camRelativeWorldPos.xyz / dot(camRelativeWorldPos.xyz, unity_WorldToCamera._m20_m21_m22);

            // // calculate the world position
            // // multiply the view plane by the linear depth to get the camera relative world space position
            // // add the world space camera position to get the world space position from the depth texture
            // float3 worldPos = viewPlane * sceneZ + _WorldSpaceCameraPos;
            // float4 depth_world = mul(unity_CameraToWorld, float4(worldPos, 1.0));

            // // float3 depth_world = mul(unity_CameraToWorld, depth_view).xyz;
            // // float4 pos_world = mul(unity_ObjectToWorld, v.vertex);
            // // // pos_world.z -= d * _Displacement;
            // float4 pos_obj = mul(unity_WorldToObject, depth_world);
            // // v.vertex.xyz = pos_world;
            // // 
            // v.vertex = pos_obj;

            // float delta = UNITY_Z_0_FAR_FROM_CLIPSPACE(v.vertex.z);
            // v.vertex.z -= d * _Displacement/delta;
            // o.pos.z += normalize(d * _Displacement);
        
            // o.posWorld = mul(unity_ObjectToWorld, v.vertex);
            // float3 dir = mul(unity_CameraProjection,o.pos);
            // o.posWorld += dir * d * _Displacement;

            // o.vertex = mul (UNITY_MATRIX_MVP, v.vertex);       
            // o.screenPos = ComputeScreenPos(o.vertex);

            v.vertex = mul(unity_WorldToObject, worldspace);
            // float3 viewpos = UnityObjectToViewPos(v.vertex);
            o.pos = UnityObjectToClipPos(worldspace);
            // o.pos.z += d * _Displacement;
            return o;
        }

        float4 frag(VertexOutput i) : COLOR {
    
            //normal direction calculations
            half3 normalDirection = normalize(i.normalDir);
            
            //diffuse color calculations
            float3 dcolor = tex2D (_DispTex, TRANSFORM_TEX(i.uv0,_DispTex));
            float d = (dcolor.r*_ChannelFactor.r + dcolor.g*_ChannelFactor.g + dcolor.b*_ChannelFactor.b) * (_Range.y-_Range.x) + _Range.x;
            clip (_ClipRange-d);
            half4 c = tex2D (_MainTex, float2(d,0.5));
            float3 diffuseColor = c.rgb;
            

            float4 finalDiffuse = tex2D(_MainTex, i.uv0);//float4(diffuseColor,1);
            return finalDiffuse;
        }
            ENDCG
        }
    }
    FallBack "Diffuse"
 }