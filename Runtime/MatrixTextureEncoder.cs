using UnityEngine;

namespace AnimatedKit
{
    public class MatrixTextureEncoder
    {
        // 编码参数
        private const float POSITION_RANGE = 10f;  // 位置范围±10米
        private const float ROTATION_RANGE = 1.414f; // 旋转范围(-√2, √2)，四元数分量范围
        private const float SCALE_RANGE = 5f;      // 缩放范围0-5倍
        
        /// <summary>
        /// 编码一个4x3矩阵到6个Color32中
        /// 每个Color32存储两个16位精度的浮点数（使用RG和BA通道）
        /// 矩阵按行主序存储
        /// </summary>
        public static Color32[] EncodeMatrix4x3(Matrix4x4 matrix)
        {
            Color32[] colors = new Color32[6];
            
            // 提取矩阵的前3列（第4列通常是0,0,0,1，可以忽略）
            Vector4 row0 = matrix.GetRow(0);
            Vector4 row1 = matrix.GetRow(1);
            Vector4 row2 = matrix.GetRow(2);
            // Vector4 row3 = matrix.GetRow(3);
            
            // 第1个像素：row0.x, row0.y
            colors[0] = EncodeTwoFloats(row0.x, row0.y, POSITION_RANGE, POSITION_RANGE);
            
            // 第2个像素：row0.z, row1.x
            colors[1] = EncodeTwoFloats(row0.z, row0.w, POSITION_RANGE, POSITION_RANGE);
            
            // 第3个像素：row1.y, row1.z
            colors[2] = EncodeTwoFloats(row1.x, row1.y, POSITION_RANGE, POSITION_RANGE);
            
            // 第4个像素：row2.x, row2.y
            colors[3] = EncodeTwoFloats(row1.z, row1.w, POSITION_RANGE, POSITION_RANGE);
            
            // 第5个像素：row2.z, row3.x
            colors[4] = EncodeTwoFloats(row2.x, row2.y, POSITION_RANGE, POSITION_RANGE);
            
            // 第6个像素：row3.y, row3.z
            colors[5] = EncodeTwoFloats(row2.z, row2.w, POSITION_RANGE, POSITION_RANGE);
            
            return colors;
        }
        
        /// <summary>
        /// 编码一个4x4矩阵到8个Color32中
        /// </summary>
        public static Color32[] EncodeMatrix4x4(Matrix4x4 matrix)
        {
            Color32[] colors = new Color32[8];
            
            Vector4 row0 = matrix.GetRow(0);
            Vector4 row1 = matrix.GetRow(1);
            Vector4 row2 = matrix.GetRow(2);
            Vector4 row3 = matrix.GetRow(3);
            
            // 每行用2个像素存储
            colors[0] = EncodeTwoFloats(row0.x, row0.y, POSITION_RANGE, POSITION_RANGE);
            colors[1] = EncodeTwoFloats(row0.z, row0.w, POSITION_RANGE, 1.0f); // w分量通常范围小
            
            colors[2] = EncodeTwoFloats(row1.x, row1.y, POSITION_RANGE, POSITION_RANGE);
            colors[3] = EncodeTwoFloats(row1.z, row1.w, POSITION_RANGE, 1.0f);
            
            colors[4] = EncodeTwoFloats(row2.x, row2.y, POSITION_RANGE, POSITION_RANGE);
            colors[5] = EncodeTwoFloats(row2.z, row2.w, POSITION_RANGE, 1.0f);
            
            colors[6] = EncodeTwoFloats(row3.x, row3.y, POSITION_RANGE, POSITION_RANGE);
            colors[7] = EncodeTwoFloats(row3.z, row3.w, 1.0f, 1.0f); // 平移的w分量通常为1
            
            return colors;
        }
        
        /// <summary>
        /// 编码两个浮点数到一个Color32中
        /// 每个浮点数使用16位定点数表示
        /// RG通道存储第一个浮点数（高8位在R，低8位在G）
        /// BA通道存储第二个浮点数（高8位在B，低8位在A）
        /// </summary>
        private static Color32 EncodeTwoFloats(float a, float b, float rangeA, float rangeB)
        {
            byte r, g, b1, a1;
            EncodeFloat16(a, rangeA, out r, out g);
            EncodeFloat16(b, rangeB, out b1, out a1);
            
            return new Color32(r, g, b1, a1);
        }
        
        /// <summary>
        /// 编码单个浮点数到两个字节（16位定点数）
        /// 不使用位运算，纯数学计算
        /// </summary>
        private static void EncodeFloat16(float value, float range, out byte highByte, out byte lowByte)
        {
            // 将值钳制到范围内并归一化到[0, 1]
            float clamped = Mathf.Clamp(value, -range, range);
            float normalized = (clamped + range) / (2.0f * range);
            
            // 转换为16位定点数（0-65535）
            // 使用65535而不是65536以避免溢出
            float fixed16Float = normalized * 65535.0f;
            
            // 分割为高8位和低8位
            // 不使用位运算，使用除法和取余
            int fixed16 = Mathf.RoundToInt(fixed16Float);
            
            // 高8位 = 值除以256
            highByte = (byte)(fixed16 / 256);
            
            // 低8位 = 值对256取余
            lowByte = (byte)(fixed16 % 256);
        }
        
        /// <summary>
        /// 解码Color32为两个浮点数
        /// </summary>
        public static void DecodeTwoFloats(Color32 color, float rangeA, float rangeB, out float a, out float b)
        {
            a = DecodeFloat16(color.r, color.g, rangeA);
            b = DecodeFloat16(color.b, color.a, rangeB);
        }
        
        /// <summary>
        /// 从两个字节解码浮点数
        /// </summary>
        private static float DecodeFloat16(byte highByte, byte lowByte, float range)
        {
            // 重建16位定点数
            int fixed16 = highByte * 256 + lowByte;
            
            // 转换为归一化值
            float normalized = fixed16 / 65535.0f;
            
            // 反归一化到原始范围
            return normalized * (2.0f * range) - range;
        }
        
        /// <summary>
        /// 从6个Color32解码4x3矩阵
        /// </summary>
        public static Matrix4x4 DecodeMatrix4x3(Color32[] colors)
        {
            if (colors.Length < 6) return Matrix4x4.identity;
            
            // 解码各分量
            float m00, m01, m02, m10, m11, m12, m20, m21, m22, m30, m31, m32;
            
            DecodeTwoFloats(colors[0], POSITION_RANGE, POSITION_RANGE, out m00, out m01);
            DecodeTwoFloats(colors[1], POSITION_RANGE, POSITION_RANGE, out m02, out m10);
            DecodeTwoFloats(colors[2], POSITION_RANGE, POSITION_RANGE, out m11, out m12);
            DecodeTwoFloats(colors[3], POSITION_RANGE, POSITION_RANGE, out m20, out m21);
            DecodeTwoFloats(colors[4], POSITION_RANGE, POSITION_RANGE, out m22, out m30);
            DecodeTwoFloats(colors[5], POSITION_RANGE, POSITION_RANGE, out m31, out m32);
            
            // 重建矩阵
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetRow(0, new Vector4(m00, m01, m02, 0));
            matrix.SetRow(1, new Vector4(m10, m11, m12, 0));
            matrix.SetRow(2, new Vector4(m20, m21, m22, 0));
            matrix.SetRow(3, new Vector4(m30, m31, m32, 1));
            
            return matrix;
        }
    }    
}
