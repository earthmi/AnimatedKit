#include "UnityStandardBRDF.cginc"
#include "Lighting.cginc"
#include "UnityCG.cginc"
#include "AutoLight.cginc"


sampler2D _MainTex;
float4 _MainTex_ST;
float4 _Tint;

#ifdef _NORMALMAP
sampler2D _NormalMap;
float4 _NormalMap_ST;
fixed _BumpScale;
#endif


#if _RimLight
float3 _RimLightColor;
float _RimPower;

void ShadingRimLight(float threshold,float nDotv,inout float3 shadingResult)
{
	float3 rimLightTerm = lerp(0,(1-pow(nDotv,_RimPower)) * _RimLightColor,threshold);
	shadingResult =clamp(shadingResult+rimLightTerm,0,1);
}
#endif



float _Metallic;
float _Smoothness;

float _SpecularPower;

float _AmbientIntensity;

#ifdef _DISSOLVEEDGE
sampler2D _NoiseTex;
float4 _NoiseTex_ST;
 	        
half _EdgeLength;
half _EdgeBlur;
fixed4 _EdgeFirstColor;
fixed4 _EdgeSecondColor;

void ShadingDissolveEdge(float dissolveValue,float2 uv,inout float3 shadingResult)
{
	half cutout = tex2D(_NoiseTex, uv).r;
	//cutout = cutout/ 2;
	half dissolvThreshold = dissolveValue;
	half cutoutThreshold = cutout - dissolvThreshold;
	clip(cutoutThreshold);

	cutoutThreshold = cutoutThreshold / _EdgeLength;
	//边缘颜色
	half degree = saturate(cutoutThreshold - _EdgeBlur);
	half3 edgeColor = lerp(_EdgeFirstColor, _EdgeSecondColor, degree);			
	shadingResult = lerp(edgeColor, shadingResult, degree);
}

#endif

struct vertexOut
{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 worldPos : TEXCOORD1;
	#ifdef _NORMALMAP
	half3 worldNormal                 : TEXCOORD2;    // xyz: normal, w: viewDir.x
	float3x3 tangentToWorldMatrix : TEXCOORD3; 
	#else
	half3  worldNormal                : TEXCOORD2;
	#endif
			  
	#ifdef _RimLight
	float rimLightThreshold:TEXCOORD6;
	#endif
			  
	#ifdef _DISSOLVEEDGE
	half dissolveValue:TEXCOORD7;
	#endif
	UNITY_SHADOW_COORDS(8)
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SurfaceDotData
{
    half NdotL;
    half NdotV;
    half NdotH;
    half LdotH;
};
SurfaceDotData GetNormalLightData(half3 normalDir,half3 viewDir,half3 lightDir)
{
	SurfaceDotData data = (SurfaceDotData)0;
	float3 halfVector = normalize(lightDir + viewDir);
	data.NdotH = max(saturate(dot(normalDir, halfVector)), 0.0000001);
	data.NdotL = max(saturate(dot(normalDir , lightDir ) ) , 0.0000001);//防止除0
	data.NdotV = max(saturate(dot(normalDir, viewDir)), 0.0000001);
	data.LdotH = max(saturate(dot(lightDir, halfVector)), 0.0000001);
	return data;
}

float3x3 GetTangent2WorldMatrix(float4 tangent,float3 worldNormal)
{
	float4 tangentWorld = float4(UnityObjectToWorldDir(tangent.xyz), tangent.w);
	half sign = tangentWorld.w * unity_WorldTransformParams.w;
	half3 binormal = cross(worldNormal, tangentWorld) * sign;
	return half3x3(tangentWorld.xyz, binormal, worldNormal);
}

half3 GetWorldNormal(float4 normalMap,float bumpScale,float3x3 tangentToWorldMatrix)
{
	half3 tangent1 = tangentToWorldMatrix[0].xyz;
	half3 binormal1 = tangentToWorldMatrix[1].xyz;
	half3 normal1 =tangentToWorldMatrix[2].xyz;
	float3 normalTangent =UnpackNormal(normalMap);
	normalTangent.xy *= bumpScale;
	half3 normal=normalize((float3)(tangent1 * normalTangent.x + binormal1 * normalTangent.y + normal1 * normalTangent.z));
	return normal;
}

fixed4 fragLowQualityLit(vertexOut i) : SV_Target
{
	float3 Albedo = _Tint* tex2D(_MainTex,i.uv);
	#ifdef _NORMALMAP
	half3 normal=GetWorldNormal(tex2D(_NormalMap,i.uv),_BumpScale,i.tangentToWorldMatrix);
	#else
	half3 normal =normalize(i.worldNormal);
	#endif
	#ifdef USING_DIRECTIONAL_LIGHT
	fixed3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
	#else
	half3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos.xyz);
	#endif
	// 计算漫反射
	half ndotl = saturate(dot(normal, worldLightDir));
	half3 diffuse = _LightColor0.rgb * Albedo.rgb * ndotl;
	// 添加环境光和顶点颜色
	half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * Albedo.rgb * _AmbientIntensity;
	//统一管理光照衰减和阴影
	UNITY_LIGHT_ATTENUATION(atten,i,i.worldPos);
	// 最终颜色
	half3 finalColor = ambient + (diffuse)*atten;

	#ifdef _RimLight
	half3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
	half nv = saturate(dot(normal, viewDir));

	ShadingRimLight(i.rimLightThreshold,nv,finalColor);
	#endif
    
	#ifdef _DISSOLVEEDGE
	ShadingDissolveEdge(i.dissolveValue,i.uv,finalColor);
	#endif
	return half4(finalColor,1);
}


fixed4 fragSimpleLit(vertexOut i) : SV_Target
{
	float3 Albedo = _Tint* tex2D(_MainTex,i.uv);
	#ifdef _NORMALMAP
	half3 normal=GetWorldNormal(tex2D(_NormalMap,i.uv),_BumpScale,i.tangentToWorldMatrix);
	#else
	half3 normal =normalize(i.worldNormal);
	#endif
	#ifdef USING_DIRECTIONAL_LIGHT
	fixed3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
	#else
	half3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos.xyz);
	#endif
	// 计算漫反射
	half ndotl = saturate(dot(normal, worldLightDir));
	half3 diffuse = _LightColor0.rgb * Albedo.rgb * ndotl;
	// 计算高光 (Blinn-Phong模型)
	half3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
	half3 halfDir = normalize(worldLightDir + viewDir);
	half ndoth = saturate(dot(normal, halfDir));
	half specularPower = exp2(10 * _Smoothness + 1); // 转换为类似Unity的光泽度
	half specularTerm = pow(ndoth, specularPower);
	half3 specular = _LightColor0.rgb *  specularTerm * _SpecularPower ;
	// 添加环境光和顶点颜色
	half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * Albedo.rgb;
	//统一管理光照衰减和阴影
	UNITY_LIGHT_ATTENUATION(atten,i,i.worldPos);
	// 最终颜色
	half3 finalColor = ambient + (diffuse + specular)*atten;

	#ifdef _RimLight
	half nv = saturate(dot(normal, viewDir));

	ShadingRimLight(i.rimLightThreshold,nv,finalColor);
	#endif
    
	#ifdef _DISSOLVEEDGE
	ShadingDissolveEdge(i.dissolveValue,i.uv,finalColor);
	#endif
	return half4(finalColor,1);
}


fixed4 fragPBR(vertexOut i) : SV_Target
{
    float3 Albedo = _Tint* tex2D(_MainTex,i.uv);
    #ifdef _NORMALMAP
		half3 normal=GetWorldNormal(tex2D(_NormalMap,i.uv),_BumpScale,i.tangentToWorldMatrix);
	#else
	    half3 normal =normalize(i.worldNormal);
	#endif
    
    #ifdef USING_DIRECTIONAL_LIGHT
    fixed3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz);
    #else
    half3 worldLightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos.xyz);
    #endif
    
    float3 viewDir=normalize(UnityWorldSpaceViewDir(i.worldPos));
    SurfaceDotData lightData = GetNormalLightData(normal,viewDir,worldLightDir);
    float3 lightColor = _LightColor0.rgb;
    float nh = lightData.NdotH;
    //roughness相关
    float perceptualRoughness = 1 - _Smoothness;
    float roughness = perceptualRoughness * perceptualRoughness;
    roughness=max(roughness,0.002);//即便smoothness为1，也要有点高光在
    float nl = lightData.NdotL;
    float nv = lightData.NdotV;
    float lh = lightData.LdotH;
    //1.2.直接光镜面反射部分
    // 1.2.1 D项（GGX）
    float D=GGXTerm(nh,roughness);
	float G=SmithJointGGXVisibilityTerm(nl,nv,roughness);
    //1.2.3 F项 菲涅尔反射 金属反射强边缘反射强
    float3 F0 = lerp(unity_ColorSpaceDielectricSpec.rgb, Albedo, _Metallic);
    //1.2.3 F项 菲涅尔反射 金属反射强边缘反射强
    float3 F=FresnelTerm(F0,lh);
    float3 specular = D * G * F ;

    float3 specColor = specular * lightColor*nl*UNITY_PI * _SpecularPower;//直接光镜面反射部分。镜面反射的系数就是F。漫反射之前少除π了，所以为了保证漫反射和镜面反射的比例，这里还得乘一个π
	float3 rawDiffColor = DisneyDiffuse(nv,nl,lh,perceptualRoughness)* nl * lightColor;
    //	漫反射系数kd
    float3 kd = OneMinusReflectivityFromMetallic(_Metallic);
    kd*=Albedo;
    float3 diffColor = kd * rawDiffColor;//直接光漫反射部分。
    float3 directLightResult = diffColor + specColor;
    //	至此，直接光部分结束
	//	2.开始间接光部分
	//  2.1间接光漫反射
	half3 iblDiffuse = ShadeSH9(float4(normal,1));
    float3 iblDiffuseResult = iblDiffuse*kd;//乘间接光漫反射系数
	//  2.2间接光镜面反射
    float mip_roughness = perceptualRoughness * (1.7 - 0.7*perceptualRoughness );
    float3 reflectVec = reflect(-viewDir, normal);
    half mip = mip_roughness * UNITY_SPECCUBE_LOD_STEPS;//得出mip层级。默认UNITY_SPECCUBE_LOD_STEPS=6（定义在UnityStandardConfig.cginc）
    half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectVec, mip);//视线方向的反射向量，去取样，同时考虑mip层级
    half3 iblSpecular = DecodeHDR(rgbm, unity_SpecCube0_HDR);//使用DecodeHDR将颜色从HDR编码下解码。可以看到采样出的rgbm是一个4通道的值，
    half surfaceReduction=1.0/(roughness*roughness+1.0);
	float oneMinusReflectivity = unity_ColorSpaceDielectricSpec.a-unity_ColorSpaceDielectricSpec.a*_Metallic;   //grazingTerm压暗非金属的边缘异常高亮
	half grazingTerm=saturate(_Smoothness+(1-oneMinusReflectivity));
    float3 iblSpecularResult = surfaceReduction*iblSpecular*FresnelLerp(F0,grazingTerm,nv);
    float3 indirectResult = (iblDiffuseResult + iblSpecularResult);
    UNITY_LIGHT_ATTENUATION(atten,i,i.worldPos);

    float3 pbrTerm = directLightResult * atten + indirectResult ;
    //最终加和
    float3 finalResult =pbrTerm;

    #ifdef _RimLight
    ShadingRimLight(i.rimLightThreshold,nv,finalResult);
    #endif
    
    #ifdef _DISSOLVEEDGE
    ShadingDissolveEdge(i.dissolveValue,i.uv,finalResult);
	#endif
    
    return half4(finalResult,1);
}
