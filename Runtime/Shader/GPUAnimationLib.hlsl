


// sampler2D _MainTex;
// float4 _MainTex_ST;
// float4 _Tint;
//
// #ifdef _NORMALMAP
// sampler2D _NormalMap;
// float4 _NormalMap_ST;
// fixed _BumpScale;
// #endif
//
// float3 _RimLightColor;
// float _RimPower;
//
// float _Metallic;
// float _Smoothness;
//
// #ifdef _DISSOLVEEDGE
// sampler2D _NoiseTex;
// float4 _NoiseTex_ST;
//  	        
// half _EdgeLength;
// half _EdgeBlur;
// fixed4 _EdgeFirstColor;
// fixed4 _EdgeSecondColor;
// #endif

sampler2D _AnimTex;
float4 _AnimTex_TexelSize;
int _PixelCountPerFrame;
float3 _BoundsRange;

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float4 tangent:TANGENT;
    float3 normal : NORMAL;
    half4 boneIndex : TEXCOORD2;
    fixed4 boneWeight : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


UNITY_INSTANCING_BUFFER_START(Props)

UNITY_DEFINE_INSTANCED_PROP(int, _StartFrame)
#define _StartFrame_arr Props

UNITY_DEFINE_INSTANCED_PROP(int, _EndFrame)
#define _EndFrame_arr Props

UNITY_DEFINE_INSTANCED_PROP(int, _FrameCount)
#define _FrameCount_arr Props

UNITY_DEFINE_INSTANCED_PROP(float, _OffsetSeconds)
#define _OffsetSeconds_arr Props

UNITY_DEFINE_INSTANCED_PROP(float, _RimLightThreshold)
#define _RimLightThreshold_arr Props

#ifdef _DISSOLVEEDGE
 UNITY_DEFINE_INSTANCED_PROP(float, _DissolveThreshold)
#define _DissolveThreshold_arr Props
#endif

UNITY_DEFINE_INSTANCED_PROP(float, _KeepingTime)
#define _KeepingTime_arr Props

UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
#define _Color_arr Props

UNITY_INSTANCING_BUFFER_END(Props)

struct AnimatedVertexInfo
{
	float4 modelSpaceVertPos;
	float3 modelSpaceVertNormal;
};
int mod_int(int a, int b) {
    float af = (float)a;
    float modF = fmod(af,(float)b);
    return int(modF);
    //对于WebGl 1.0 来说，用的是GLSL ES 2.0，不能用整数取模
    // #if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES30)
    // 	#if SHADER_TARGET < 30
    // 			return int((float(a)%float(b)));
    // 	#else
    // 			return a % b;
    // 	#endif
    // #else
    // return a % b;
    // #endif
}
		
float4 GetUV(uint pixelIndex)
{
    float width =_AnimTex_TexelSize.z;
    float height =_AnimTex_TexelSize.w;
    uint row = floor(pixelIndex / width);
    // uint row = ceil(pixelIndex / width);
    // uint column = frac(pixelIndex / width) * width;
    uint column =  mod_int(pixelIndex,width);// index % texelSizeZ;
    return float4(column / width, row / height, 0, 0);
}

float DecodeFloatRGBA32(float4 color)
{
    float minValue = _BoundsRange.x;
    float maxValue = _BoundsRange.y;
    // color分量范围0~1，转uint
    uint r = (uint)(color.r * 255.0);
    uint g = (uint)(color.g * 255.0);
    uint b = (uint)(color.b * 255.0);
    uint a = (uint)(color.a * 255.0);
    //避免位运算，openGL ES 2.0不支持
    //uint enc = (r << 24) | (g << 16) | (b << 8) | a;
    float enc = r * 16777216.0 +  // 等效于 r << 24
        g * 65536.0 +     // 等效于 g << 16
        b * 256.0 +       // 等效于 b << 8
        a;                // 等效于 a
    float normalized = enc / 4294967295.0;
    return lerp(minValue, maxValue, normalized);
}

float DecodeFloatRGBA32NoBit(float4 color)
{
    float minValue = _BoundsRange.x;
    float maxValue = _BoundsRange.y;
    
    // 直接使用颜色分量进行插值，避免大整数运算
    // 原理：将四个8位通道视为一个32位整数的四个字节
    
    // 方法：将每个分量视为不同的"权重"位
    // R: 最高8位，权重 16777216/4294967295 ≈ 0.00390625
    // G: 次高8位，权重 65536/4294967295 ≈ 1.5259e-5
    // B: 次低8位，权重 256/4294967295 ≈ 5.9605e-8
    // A: 最低8位，权重 1/4294967295 ≈ 2.3283e-10
    
    // 预计算的归一化权重
    const float weightR = 0.00390625;        // 2^24 / 2^32
    const float weightG = 1.52587890625e-5;  // 2^16 / 2^32
    const float weightB = 5.96046447754e-8;  // 2^8 / 2^32
    const float weightA = 2.32830643654e-10; // 2^0 / 2^32
    
    // 直接计算归一化值（避免中间的大整数）
    float normalized = color.r * weightR + 
                      color.g * weightG + 
                      color.b * weightB + 
                      color.a * weightA;
    
    return lerp(minValue, maxValue, normalized);
}
			
float4x4 GetMatrixRGBHalf(uint startIndex, float boneIndex)
{
    //startIndex是这一帧的矩阵开始的索引
    //boneIndex * 3是得到第几个骨骼矩阵索引的开始索引
    uint matrixIndex = startIndex + boneIndex * 3;
    //matrixIndex是这一帧的矩阵索引的这个骨骼索引的开始的像素索引
			
    float4 row0 = tex2Dlod(_AnimTex, GetUV(matrixIndex));
    float4 row1 = tex2Dlod(_AnimTex, GetUV(matrixIndex + 1));
    float4 row2 = tex2Dlod(_AnimTex, GetUV(matrixIndex + 2));
    float4 row3 = float4(0, 0, 0, 1);

    return float4x4(row0, row1, row2, row3);
}

float4x4 GetMatrixRGBM(uint startIndex, float boneIndex)
{
    float4x4 m = float4x4(0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,1);

    //startIndex是这一帧的矩阵开始的索引
    //boneIndex * 12是得到第几个骨骼矩阵索引的开始索引
    uint matrixIndex = startIndex + boneIndex * 12;
    //matrixIndex是这一帧的矩阵索引的这个骨骼索引的开始的像素索引
			
    m[0][0] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex)));
    m[0][1] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+1)));
    m[0][2] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+2)));
    m[0][3] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+3)));


    m[1][0] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+4)));
    m[1][1] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+5)));
    m[1][2] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+6)));
    m[1][3] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+7)));

    m[2][0] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+8)));
    m[2][1] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+9)));
    m[2][2] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+10)));
    m[2][3] = DecodeFloatRGBA32(tex2Dlod(_AnimTex, GetUV(matrixIndex+11)));
    
    return m;
}

float4x4 GetMatrix(uint startIndex, float boneIndex)
{
    #ifdef _FORMAT_RGBM
    return  GetMatrixRGBM(startIndex, boneIndex);
    #elif _FORMAT_RGBAHALF 
    return  GetMatrixRGBHalf(startIndex, boneIndex);
    #endif
}

uint GetCurrentFramePixelBeginIndex()
{
    uint startFrame = UNITY_ACCESS_INSTANCED_PROP(_StartFrame_arr, _StartFrame);
    // int endFrame = UNITY_ACCESS_INSTANCED_PROP(_EndFrame_arr, _EndFrame);
    uint frameCount = UNITY_ACCESS_INSTANCED_PROP(_FrameCount_arr, _FrameCount);
    // float offsetSeconds = UNITY_ACCESS_INSTANCED_PROP(_OffsetSeconds_arr, _OffsetSeconds);
    float keepingTime = UNITY_ACCESS_INSTANCED_PROP(_KeepingTime_arr, _KeepingTime);
    uint offsetFrame = (uint)(keepingTime * 30);
    uint currentFrame = startFrame + clamp(offsetFrame,0,frameCount-1) ; // mod_int(offsetFrame,frameCount);// offsetFrame % frameCount;
    uint pixelBeginIndex = currentFrame * _PixelCountPerFrame;//这一帧的骨骼矩阵像素数据的开始的像素索引 
    return pixelBeginIndex;
}

AnimatedVertexInfo GetTargetFrameAnimationInfo(uint pixelBeginIndex,float4 originVert,float3 originNormal,half4 boneIndex,half4 boneWeight)
{
#ifdef _SKIN_BONE1
    float4x4 bone1Matrix = GetMatrix(pixelBeginIndex, boneIndex.x);
    
    float4 pos =
        mul(bone1Matrix, originVert) * boneWeight.x;
    
    float4 normal =
        mul(bone1Matrix, originNormal) * boneWeight.x;
#elif _SKIN_BONE2
    float4x4 bone1Matrix = GetMatrix(pixelBeginIndex, boneIndex.x);
    float4x4 bone2Matrix = GetMatrix(pixelBeginIndex, boneIndex.y);

    float4 pos =
        mul(bone1Matrix, originVert) * boneWeight.x
    +mul(bone2Matrix, originVert) * boneWeight.y;


    float4 normal =
        mul(bone1Matrix, originNormal) * boneWeight.x
    +mul(bone2Matrix, originNormal) * boneWeight.y;
#else
        float4x4 bone1Matrix = GetMatrix(pixelBeginIndex, boneIndex.x);
    float4x4 bone2Matrix = GetMatrix(pixelBeginIndex, boneIndex.y);
    float4x4 bone3Matrix = GetMatrix(pixelBeginIndex, boneIndex.z);
    float4x4 bone4Matrix = GetMatrix(pixelBeginIndex, boneIndex.w);

    float4 pos =
        mul(bone1Matrix, originVert) * boneWeight.x
    +mul(bone2Matrix, originVert) * boneWeight.y
    +mul(bone3Matrix, originVert) * boneWeight.z
    +mul(bone4Matrix, originVert) * boneWeight.w;

    float4 normal =
        mul(bone1Matrix, originNormal) * boneWeight.x
    +mul(bone2Matrix, originNormal) * boneWeight.y
    +mul(bone3Matrix, originNormal) * boneWeight.z
    +mul(bone4Matrix, originNormal) * boneWeight.w;
#endif

    AnimatedVertexInfo o;
    o.modelSpaceVertPos = pos;
    o.modelSpaceVertNormal = normal;
    return o;
}

AnimatedVertexInfo GetAnimationInfo(float4 originVert,float3 originNormal,half4 boneIndex,half4 boneWeight)
{
    AnimatedVertexInfo o;
#if _INTERPOLATION
    uint startFrame = UNITY_ACCESS_INSTANCED_PROP(_StartFrame_arr, _StartFrame);
    uint frameCount = UNITY_ACCESS_INSTANCED_PROP(_FrameCount_arr, _FrameCount);
    // float offsetSeconds = UNITY_ACCESS_INSTANCED_PROP(_OffsetSeconds_arr, _OffsetSeconds);
    float keepingTime = UNITY_ACCESS_INSTANCED_PROP(_KeepingTime_arr, _KeepingTime);
    float fFrame = (keepingTime * 30);
    uint offsetFrame = (uint)fFrame;
    uint offsetFrameNext = offsetFrame+1;

    uint currentFrame = startFrame + clamp(offsetFrame,0,frameCount-1) ; // mod_int(offsetFrame,frameCount);// offsetFrame % frameCount;
    uint nextFrame = startFrame + clamp(offsetFrameNext,0,frameCount-1) ; // mod_int(offsetFrame,frameCount);// offsetFrame % frameCount;
    
    uint currentFramePixelBeginIndex = currentFrame * _PixelCountPerFrame;//这一帧的骨骼矩阵像素数据的开始的像素索引
    uint nextFramePixelBeginIndex = nextFrame * _PixelCountPerFrame;//下一帧的骨骼矩阵像素数据的开始的像素索引
    AnimatedVertexInfo currentInfo = GetTargetFrameAnimationInfo(currentFramePixelBeginIndex,originVert,originNormal,boneIndex,boneWeight);
    AnimatedVertexInfo nextInfo = GetTargetFrameAnimationInfo(nextFramePixelBeginIndex,originVert,originNormal,boneIndex,boneWeight);
    float percent = frac(fFrame);
    o.modelSpaceVertPos = lerp(currentInfo.modelSpaceVertPos,nextInfo.modelSpaceVertPos,percent);
    o.modelSpaceVertNormal = lerp(currentInfo.modelSpaceVertNormal,nextInfo.modelSpaceVertNormal,percent);
#else
    uint pixelBeginIndex = GetCurrentFramePixelBeginIndex();
    o = GetTargetFrameAnimationInfo(pixelBeginIndex,originVert,originNormal,boneIndex,boneWeight);
#endif


    return o;
}





