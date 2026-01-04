using System;
using System.Globalization;
using UnityEngine;

namespace ReunionMovement.Common.Util
{
    /// <summary>
    /// 颜色工具类，包含颜色与十六进制互转、颜色明暗调整等常用方法
    /// </summary>
    public static class ColorUtil
    {
        private const float lightOffset = 0.0625f;

        /// <summary>
        /// 将 Color 转换为十六进制字符串（包含 alpha 通道），格式为 RRGGBBAA
        /// </summary>
        /// <param name="target">待转换的颜色</param>
        /// <returns>十六进制表示的颜色字符串（大写）</returns>
        public static string ColorToHex(Color target)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(target.r) * 255.0f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(target.g) * 255.0f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(target.b) * 255.0f);
            int a = Mathf.RoundToInt(Mathf.Clamp01(target.a) * 255.0f);
            return $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }

        /// <summary>
        /// 将十六进制字符串转换为 Color
        /// 支持 "RRGGBB" 或 "RRGGBBAA" 两种格式，支持可选的前导字符 '#'
        /// </summary>
        /// <param name="hex">十六进制颜色字符串</param>
        /// <returns>对应的 Color（通道范围为 0-1）</returns>
        public static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) throw new ArgumentException("Hex为空或未定义", nameof(hex));

            if (hex.StartsWith("#")) hex = hex.Substring(1);

            if (hex.Length != 6 && hex.Length != 8)
                throw new ArgumentException("十六进制代码的长度必须为6个（RRGGBB）或8个（RRGGBBAA）字符", nameof(hex));

            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte a = 255;

            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
            }

            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        /// <summary>
        /// 返回比当前颜色更亮的颜色（各通道加上固定偏移并裁剪到 [0,1]）
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <returns>变亮后的颜色</returns>
        public static Color Lighter(this Color color)
        {
            return new Color(
                Mathf.Clamp(color.r + lightOffset, 0, 1),
                Mathf.Clamp(color.g + lightOffset, 0, 1),
                Mathf.Clamp(color.b + lightOffset, 0, 1),
                color.a);
        }

        /// <summary>
        /// 返回比当前颜色更暗的颜色（各通道减去固定偏移并裁剪到 [0,1]）
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <returns>变暗后的颜色</returns>
        public static Color Darker(this Color color)
        {
            return new Color(
                Mathf.Clamp(color.r - lightOffset, 0, 1),
                Mathf.Clamp(color.g - lightOffset, 0, 1),
                Mathf.Clamp(color.b - lightOffset, 0, 1),
                color.a);
        }

        /// <summary>
        /// 计算颜色的亮度，定义为 R、G、B 三个通道的算术平均值
        /// 值域为 0（黑）到 1（白）
        /// </summary>
        /// <param name="color">待计算的颜色</param>
        /// <returns>亮度值（0-1）</returns>
        public static float Brightness(this Color color)
        {
            return (color.r + color.g + color.b) / 3;
        }

        /// <summary>
        /// 根据指定亮度调整颜色（按比例缩放 RGB 通道），保持 alpha 不变
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <param name="brightness">目标亮度（0-1）</param>
        /// <returns>调整亮度后的颜色</returns>
        public static Color WithBrightness(this Color color, float brightness)
        {
            if (color.IsApproximatelyBlack())
            {
                return new Color(brightness, brightness, brightness, color.a);
            }

            float factor = brightness / color.Brightness();

            float r = color.r * factor;
            float g = color.g * factor;
            float b = color.b * factor;

            float a = color.a;

            // 确保值在 [0,1] 范围内
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), a);
        }

        /// <summary>
        /// 判断颜色是否为黑色或接近黑色（RGB通道之和接近0）
        /// </summary>
        /// <param name="color">要判断的颜色</param>
        /// <returns>若接近黑色则返回 true</returns>
        public static bool IsApproximatelyBlack(this Color color)
        {
            return color.r + color.g + color.b <= Mathf.Epsilon;
        }

        /// <summary>
        /// 判断颜色是否为白色或接近白色（RGB通道平均值接近1）
        /// </summary>
        /// <param name="color">要判断的颜色</param>
        /// <returns>若接近白色则返回 true</returns>
        public static bool IsApproximatelyWhite(this Color color)
        {
            // 使用通道平均值接近 1 的判断
            return (color.r + color.g + color.b) / 3 >= 1 - Mathf.Epsilon;
        }

        /// <summary>
        /// 返回颜色的反色（仅反转 RGB 通道，保留 alpha）
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <returns>反色</returns>
        public static Color Invert(this Color color)
        {
            return new Color(1 - color.r, 1 - color.g, 1 - color.b, color.a);
        }

        /// <summary>
        /// 返回不透明版本的颜色（alpha 设置为 1）
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <returns>不透明颜色</returns>
        public static Color Opaque(this Color color)
        {
            return new Color(color.r, color.g, color.b, 1f);
        }

        /// <summary>
        /// 返回指定 alpha 的颜色（保持 RGB 不变）
        /// </summary>
        /// <param name="color">原颜色</param>
        /// <param name="alpha">目标 alpha（0-1）</param>
        /// <returns>具有指定透明度的颜色</returns>
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}
