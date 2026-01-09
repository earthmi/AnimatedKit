Shader "MiniGame/Standard-Lit(Low)"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    	_Tint("Tint",Color)=(1,1,1,1)
    	
    	[Header(Normal)]
    	[Toggle(_NORMALMAP)] _IsEnableNormalMap ("Is Enable Normal Map", Int) = 0
         _BumpScale("Bump Scale", Range(-1, 1)) = 1.0
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _AmbientIntensity("Ambient Intensity", Range(1, 3)) = 1.0
    	[Header(RimLight)]
    	[Toggle(_RimLight)] _RimLightEnable ("Is Enable Rim Light", Int) = 0

    	_RimLightColor("RimLightColor",Color)=(0,0,0,0)
    	_RimPower("RimPower",Range(1,10))=8
        _RimLightThreshold("RimLightThreshold",Range(0,1))=0
        
    	[Header(Dissolve)]
    	[Toggle(_DISSOLVEEDGE)] _DissolveEdge ("Is Enable Dissolve Edge", Int) = 0
		_NoiseTex ("Noise", 2D) = "white" { }
        _EdgeLength ("Edge Length", Range(0.0, 0.2)) = 0.1
        _EdgeBlur ("Edge Blur", Range(0.0, 1.0)) = 0.0
        _EdgeFirstColor ("EdgeFirstColor", Color) = (1, 1, 1, 1)
        _EdgeSecondColor ("EdgeSecondColor", Color) = (1, 1, 1, 1)
    	_DissolveThreshold ("Edge Threshold", Range(0.0, 1.0)) = 0.5
    	

    }
    SubShader
    {
        Pass
        {
            Tags{"LightMode"="ForwardBase"}
        CGPROGRAM

 	        #pragma exclude_renderers ps3 xbox360 flash xboxone ps4 psp2
	        #pragma skip_variants DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_ON
	        #pragma skip_variants SHADOWS_CUBE LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK FOG_EXP FOG_EXP2
 	        #pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
 	        // #pragma multi_compile_instancing

			#pragma shader_feature __ _DISSOLVEEDGE
			#pragma shader_feature __ _NORMALMAP
			#pragma shader_feature __ _RimLight

 	        #include "IlluminationLib.hlsl" 
            #pragma vertex vert
            #pragma fragment fragLowQualityLit
            struct a2v
            {
                 float4 vertex : POSITION;
                 float3 normal:NORMAL;
                 float2 uv : TEXCOORD0;
                 float2 uv1:TEXCOORD1;
                 fixed4 tangent : TANGENT;
 	        	UNITY_VERTEX_INPUT_INSTANCE_ID

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                #ifndef LIGHTMAP_OFF 
                half2 uv1:TEXCOORD1;
                #endif
                float4 pos : SV_POSITION;
                float3 normal:TEXCOORD2;
                float3 worldPos:TEXCOORD3;
                float4 tangent:TEXCOORD4;
                float3x3 tangentToWorld : TEXCOORD5; 
                float3 objectspaceViewdir:COLOR2;
                float3x3 tangentMatrix: TEXCOORD8; 
                SHADOW_COORDS(11)
            };
			UNITY_INSTANCING_BUFFER_START(Props)
			#ifdef _RimLight
			UNITY_DEFINE_INSTANCED_PROP(float, _RimLightThreshold)
			#define _RimLightThreshold_arr Props
			#endif
			 
			#ifdef _DISSOLVEEDGE
			 UNITY_DEFINE_INSTANCED_PROP(float, _DissolveThreshold)
			#define _DissolveThreshold_arr Props
			#endif

			UNITY_INSTANCING_BUFFER_END(Props)


            vertexOut vert (a2v v)
            {
				UNITY_SETUP_INSTANCE_ID(v);
                vertexOut o =(vertexOut)0;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal=UnityObjectToWorldNormal(v.normal);
                
                #ifdef _NORMALMAP
                	o.tangentToWorldMatrix=GetTangent2WorldMatrix(v.tangent,o.worldNormal);
		        #endif 
                
                #ifdef _RimLight
        			float rimThreshold = UNITY_ACCESS_INSTANCED_PROP(_RimLightThreshold_arr, _RimLightThreshold);
					o.rimLightThreshold = rimThreshold;
		        #endif

                
                #ifdef _DISSOLVEEDGE
                o.dissolveValue = UNITY_ACCESS_INSTANCED_PROP(_DissolveThreshold_arr, _DissolveThreshold);
                #endif
                
                TRANSFER_SHADOW(o);
                return o;
            }
        ENDCG
        }
    }
    FallBack"Diffuse"
}