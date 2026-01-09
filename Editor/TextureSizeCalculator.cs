namespace AnimatedKit
{
    using UnityEngine;

    public class TextureSizeCalculator
    {
        /// <summary>
        /// 计算最优纹理尺寸
        /// </summary>
        /// <param name="pixelCount">需要存储的总像素数</param>
        /// <param name="maxTextureSize">硬件支持的最大纹理尺寸（如4096）</param>
        /// <param name="requirePowerOfTwo">是否需要2的幂次方尺寸</param>
        /// <returns>最优的(width, height)</returns>
        public static (int width, int height) CalculateOptimalTextureSize(
            int pixelCount, 
            int maxTextureSize = 4096, 
            bool requirePowerOfTwo = true)
        {
            // 方法1：寻找最接近正方形的尺寸
            return FindClosestToSquare(pixelCount, maxTextureSize, requirePowerOfTwo);
            
            // 方法2：固定高度，优化宽度（根据内存布局）
            // return OptimizeForCache(pixelCount, maxTextureSize, requirePowerOfTwo);
        }
        
        private static (int width, int height) FindClosestToSquare(
            int pixelCount, 
            int maxTextureSize, 
            bool requirePowerOfTwo)
        {
            // 计算理论上的正方形边长
            int idealSide = Mathf.CeilToInt(Mathf.Sqrt(pixelCount));
            
            int bestWidth = 0;
            int bestHeight = 0;
            float bestRatio = float.MaxValue; // 宽高比越接近1越好
            float bestWaste = float.MaxValue; // 浪费的像素数
            
            // 遍历可能的尺寸
            for (int width = 1; width <= maxTextureSize; width++)
            {
                // 如果需要2的幂次方，跳过非2的幂次方
                if (requirePowerOfTwo && !IsPowerOfTwo(width))
                    continue;
                    
                // 计算所需高度
                int height = Mathf.CeilToInt(pixelCount / (float)width);
                
                // 如果需要2的幂次方，调整高度
                if (requirePowerOfTwo)
                    height = NextPowerOfTwo(height);
                
                // 检查是否超出限制
                if (height > maxTextureSize)
                    continue;
                    
                // 计算总像素数
                int totalPixels = width * height;
                if (totalPixels < pixelCount)
                    continue;
                    
                // 计算宽高比（越接近1越好）
                float aspectRatio = Mathf.Max(width, height) / (float)Mathf.Min(width, height);
                
                // 计算浪费的像素百分比
                float wastePercentage = (totalPixels - pixelCount) / (float)totalPixels;
                
                // 综合评估函数
                float score = aspectRatio * 0.5f + wastePercentage * 0.5f;
                
                if (score < bestRatio || 
                    (Mathf.Abs(score - bestRatio) < 0.01f && totalPixels < bestWidth * bestHeight))
                {
                    bestRatio = score;
                    bestWidth = width;
                    bestHeight = height;
                    bestWaste = wastePercentage;
                }
                
                // 如果找到非常接近的尺寸，可以提前退出
                if (wastePercentage < 0.05f && aspectRatio < 1.2f)
                    break;
            }
            
            Debug.Log($"最优纹理尺寸: {bestWidth}x{bestHeight}, " +
                     $"浪费像素: {(bestWidth * bestHeight - pixelCount)} " +
                     $"({bestWaste:P1}), 宽高比: {bestWidth/(float)bestHeight:F2}");
            
            return (bestWidth, bestHeight);
        }
        
        /// <summary>
        /// 针对GPU缓存优化（通常是宽度优先）
        /// </summary>
        private static (int width, int height) OptimizeForCache(
            int pixelCount, 
            int maxTextureSize, 
            bool requirePowerOfTwo)
        {
            // GPU通常对较宽的纹理更友好（缓存行）
            // 尝试不同的高度，计算最小宽度
            
            int bestWidth = 0;
            int bestHeight = 0;
            int bestWaste = int.MaxValue;
            
            // 高度从最小开始尝试
            for (int height = 4; height <= maxTextureSize; height *= 2)
            {
                if (requirePowerOfTwo && !IsPowerOfTwo(height))
                    continue;
                    
                int width = Mathf.CeilToInt(pixelCount / (float)height);
                
                if (requirePowerOfTwo)
                    width = NextPowerOfTwo(width);
                    
                if (width > maxTextureSize)
                    continue;
                    
                int waste = width * height - pixelCount;
                
                // 优先选择浪费少的，然后选择宽度更大的（缓存友好）
                if (waste < bestWaste || 
                    (waste == bestWaste && width > bestWidth))
                {
                    bestWaste = waste;
                    bestWidth = width;
                    bestHeight = height;
                }
            }
            
            return (bestWidth, bestHeight);
        }
        
        /// <summary>
        /// 检查是否为2的幂次方
        /// </summary>
        private static bool IsPowerOfTwo(int x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
        
        /// <summary>
        /// 下一个2的幂次方
        /// </summary>
        private static int NextPowerOfTwo(int x)
        {
            if (x <= 0) return 1;
            x--;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }
    }
}