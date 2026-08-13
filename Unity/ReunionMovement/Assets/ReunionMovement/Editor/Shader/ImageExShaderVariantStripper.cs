using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using ReunionMovement.UI.ImageExtensions;

namespace ReunionMovement.EditorTools.ImageExtensions
{
    /// <summary>
    /// 在 WebGL 构建阶段裁剪 ImageEx 变体：
    /// 1) 形状关键词白名单
    /// 2) 过渡模式白名单
    /// 3) 关闭高成本 Blur/Outline 组合
    /// </summary>
    public sealed class ImageExShaderVariantStripper : IPreprocessShaders
    {
        private const string TargetShaderName = "ReunionMovement/UI/ImageEx";

        private static readonly HashSet<string> ShapeKeywords = new HashSet<string>
        {
            "CIRCLE",
            "TRIANGLE",
            "RECTANGLE",
            "PENTAGON",
            "HEXAGON",
            "CHAMFERBOX",
            "QUADRILATERAL",
            "NSTAR_POLYGON",
            "HEART",
            "BLOBBYCROSS",
            "SQUIRCLE",
            "NTRIANGLE_ROUNDED"
        };

        // WebGL 白名单：形状在运行时由 Inspector 任意配置（DrawShape 枚举支持 12 种形状），
        // 静态白名单无法穷举实际使用集合；剥离未列出的形状会导致 WebGL 渲染损坏
        // （如四边形 QUADRILATERAL 被剥掉后 ImageEx 四边形 UI 直接消失）。
        // 因此保留全部形状变体，体积优化仅作用于过渡模式与高成本模糊/描边组合。
        private static readonly HashSet<string> AllowedShapeKeywords = new HashSet<string>
        {
            "RECTANGLE",
            "CIRCLE",
            "TRIANGLE",
            "PENTAGON",
            "HEXAGON",
            "CHAMFERBOX",
            "QUADRILATERAL",
            "NSTAR_POLYGON",
            "HEART",
            "BLOBBYCROSS",
            "SQUIRCLE",
            "NTRIANGLE_ROUNDED"
        };

        private static readonly HashSet<string> TransitionKeywords = new HashSet<string>
        {
            "TRANSITION_FADE",
            "TRANSITION_CUTOFF",
            "TRANSITION_DISSOLVE",
            "TRANSITION_SHINY",
            "TRANSITION_MASK",
            "TRANSITION_MELT",
            "TRANSITION_BURN",
            "TRANSITION_PATTERN",
            "TRANSITION_BLAZE"
        };

        // WebGL 白名单：只保留常用过渡模式，未列出的过渡变体会被剥离。
        private static readonly HashSet<string> AllowedTransitionKeywords = new HashSet<string>
        {
            "TRANSITION_FADE",
            "TRANSITION_CUTOFF",
            "TRANSITION_DISSOLVE",
            "TRANSITION_PATTERN"
        };

        private static readonly HashSet<string> BlurKeywords = new HashSet<string>
        {
            "BLUR_FAST",
            "BLUR_MEDIUM",
            "BLUR_DETAIL"
        };

        private static readonly HashSet<string> MediumOrDetailBlurKeywords = new HashSet<string>
        {
            "BLUR_MEDIUM",
            "BLUR_DETAIL"
        };

        private static readonly HashSet<string> OutlineKeywords = new HashSet<string>
        {
            "STROKE",
            "OUTLINED",
            "OUTLINED_STROKE"
        };

        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != TargetShaderName)
            {
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                return;
            }

            for (int i = data.Count - 1; i >= 0; i--)
            {
                ShaderCompilerData variant = data[i];
                HashSet<string> activeKeywords = GetActiveKeywordNames(variant);

                bool hasShapeKeyword = ContainsAny(activeKeywords, ShapeKeywords);
                bool hasAllowedShapeKeyword = ContainsAny(activeKeywords, AllowedShapeKeywords);
                bool hasTransition = ContainsAny(activeKeywords, TransitionKeywords);
                bool hasAllowedTransition = ContainsAny(activeKeywords, AllowedTransitionKeywords);
                bool hasBlurDetail = activeKeywords.Contains("BLUR_DETAIL");
                bool hasBlur = ContainsAny(activeKeywords, BlurKeywords);
                bool hasMediumOrDetailBlur = ContainsAny(activeKeywords, MediumOrDetailBlurKeywords);
                bool hasOutline = ContainsAny(activeKeywords, OutlineKeywords);

                bool stripByShapeWhitelist = hasShapeKeyword && !hasAllowedShapeKeyword;
                bool stripByTransitionWhitelist = hasTransition && !hasAllowedTransition;

                // 组合剥离：优先剥离 WebGL 高成本组合。
                bool stripByCombination =
                    hasBlurDetail ||
                    (hasTransition && hasMediumOrDetailBlur) ||
                    (hasOutline && hasMediumOrDetailBlur) ||
                    (hasTransition && hasOutline && hasBlur);

                if (stripByShapeWhitelist || stripByTransitionWhitelist || stripByCombination)
                {
                    data.RemoveAt(i);
                }
            }
        }

        private static HashSet<string> GetActiveKeywordNames(ShaderCompilerData variant)
        {
            var result = new HashSet<string>();
            foreach (var item in variant.shaderKeywordSet.GetShaderKeywords())
            {
                result.Add(item.ToString());
            }

            return result;
        }

        private static bool ContainsAny(HashSet<string> source, HashSet<string> candidates)
        {
            foreach (string item in candidates)
            {
                if (source.Contains(item))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
