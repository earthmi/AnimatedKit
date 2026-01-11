/*
Created by jiadong chen
https://jiadong-chen.medium.com/
*/

Shader "GPUAnimation/SkinnedSkeleton-Lit(Simple)"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    	_Tint("Tint",Color)=(1,1,1,1)
    	[Header(Normal)]
    	[Toggle(_NORMALMAP)] _IsEnableNormalMap ("Is Enable Normal Map", Int) = 0
         _BumpScale("Bump Scale", Range(-1, 1)) = 1.0
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
    	
    	[Header(Specular)]
    	[Gamma]_SpecularPower("Specular Power",Range(0,1))=0
    	_Smoothness("Smoothness(Metallic.a)",Range(0,1))=0.5
    	
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
    	
		[Header(GPU Animation)]
		[NoScaleOffset] _AnimTex("Animation Texture", 2D) = "white" {}
    	[KeywordEnum(RGBM, RGBAHALF)] _Format ("Animation Texture Format", Float) = 1
    	[KeywordEnum(Bone1, Bone2,Bone4)] _Skin ("Skinned Quality", Float) = 2
        [Toggle(_INTERPOLATION)] _Interpolation ("Is Enable Interpolation", int) = 0

    	[PerRendererData]_KeepingTime("KeepingTime Time", Float) = 0
	    [PerRendererData]_StartFrame("StartFrame", Int) = 0
	    [PerRendererData]_EndFrame("EndFrame", Int) = 0
	    [PerRendererData]_FrameCount("FrameCount", Int) = 1
	    [PerRendererData]_OffsetSeconds("OffsetSeconds", Float) = 0
	    [PerRendererData]_PixelCountPerFrame("PixelCountPerFrame", Int) = 0
    	[PerRendererData]_BoundsRange("BoundsRange(RGBM Only)", Vector) = (0,0,0,0)

    }

    
        CGINCLUDE
 	   //  #pragma exclude_renderers ps3 xbox360 flash xboxone ps4 psp2
	    // #pragma skip_variants DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_ON
	    // #pragma skip_variants SHADOWS_CUBE LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK FOG_EXP FOG_EXP2
 	    #pragma shader_feature_local_vertex _FORMAT_RGBM _FORMAT_RGBAHALF
 	    #pragma shader_feature_local_vertex _SKIN_BONE1 _SKIN_BONE2 _SKIN_BONE4
		#pragma shader_feature_local_vertex  _INTERPOLATION

 	    #include "IlluminationLib.hlsl" 
        #include "GPUAnimationLib.hlsl"
 	    
		ENDCG



		SubShader
        {
			Pass
            {
                Tags {
                	"LightMode"="ForwardBase"
	                "RenderType" = "Opaque"
                	"Queue"="AlphaTest+1"
                }
				Cull off
                CGPROGRAM
                #pragma target 3.0
                #pragma vertex animatedShadingVert
                #pragma fragment fragSimpleLit
                //开启gpu instancing
                #pragma multi_compile_instancing
				#pragma multi_compile_fwdbase
				#pragma shader_feature __ _DISSOLVEEDGE
				#pragma shader_feature __ _NORMALMAP
				#pragma shader_feature __ _RimLight
                
                vertexOut animatedShadingVert(appdata v)
				{
					UNITY_SETUP_INSTANCE_ID(v);
					AnimatedVertexInfo animaInfo = GetAnimationInfo(v.vertex,v.normal,v.boneIndex,v.boneWeight);
					vertexOut o = (vertexOut)0;
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					o.worldNormal =UnityObjectToWorldNormal(animaInfo.modelSpaceVertNormal);
                	fixed4 worldPos  = mul(unity_ObjectToWorld, animaInfo.modelSpaceVertPos);
                	o.worldPos = worldPos;
                	o.pos = mul(UNITY_MATRIX_VP, worldPos);
 
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

			Pass
			{
				Name "SHADOWCASTER"
				Tags { "LightMode" = "ShadowCaster" }

				CGPROGRAM
				#pragma vertex vertexShadow
				#pragma fragment fragShadow
				#pragma multi_compile_instancing
				#pragma multi_compile_shadowcaster
				struct a2fs
	            {
          			float4 vertex:POSITION; 
	                float3 normal:NORMAL;
	                float4 boneIndex:TEXCOORD2;
	                float4 boneWeight:TEXCOORD3;
	                UNITY_VERTEX_INPUT_INSTANCE_ID
	            };
				struct shadowVert2Frag
		        {
					V2F_SHADOW_CASTER;
		        };
				
				shadowVert2Frag vertexShadow(a2fs v)
				{
					UNITY_SETUP_INSTANCE_ID(v);
					AnimatedVertexInfo animaInfo = GetAnimationInfo(v.vertex,v.normal,v.boneIndex,v.boneWeight);
					v.vertex = animaInfo.modelSpaceVertPos;
					v.normal = animaInfo.modelSpaceVertNormal;
					shadowVert2Frag o;
					 TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
					return o;
				}


		        float4 fragShadow(shadowVert2Frag i) : SV_Target
		        {
		            SHADOW_CASTER_FRAGMENT(i)
		        }
				ENDCG
			}
        }
		FallBack "Diffuse"
}

