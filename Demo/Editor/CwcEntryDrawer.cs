using System;
using UnityEditor;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;
using Cwcbb.Tools.CwcSceneDirector.Editor;

namespace Cwcbb.Tools.CwcSceneDirector.Demo.Editor
{
    /// <summary>
    /// 加权放置调度外壳 (CwcWeightedEntryBase) 通用卡片属性绘制器
    /// 遵循 CwcFormula 紧凑卡片排版规范：
    /// 1. Header 标题承担信息预览功能（序号 1. 2. + 预制体名称 + 放置模式）；
    /// 2. 固有字段（权重 W、保底 Min、上限 Max）在右侧统一右对齐紧凑编辑；
    /// 3. 展开后直接平铺 Payload 内部字段，消除多余中间层与重复字段。
    /// </summary>
    [CustomPropertyDrawer(typeof(CwcWeightedEntryBase), useForChildren: true)]
    public class CwcWeightedEntryDrawer : PropertyDrawer
    {
        #region 常量与静态 (Constants & Static)

        private const float HeaderControlsWidth = 152f;

        #endregion

        #region 公开重写方法 (Public Methods)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return CwcCardDrawerUtil.CalculateCardHeight(property, () =>
            {
                return CwcCardDrawerUtil.CalculateEntryContentHeight(
                    property,
                    prop => prop.name != "_weight" && prop.name != "_minLimit" && prop.name != "_maxLimit"
                );
            });
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Type targetType = fieldInfo != null ? fieldInfo.FieldType : typeof(CwcWeightedEntryBase);

            SerializedProperty weightProp = property.FindPropertyRelative("_weight");
            SerializedProperty minLimitProp = property.FindPropertyRelative("_minLimit");
            SerializedProperty maxLimitProp = property.FindPropertyRelative("_maxLimit");
            SerializedProperty payloadProp = property.FindPropertyRelative("_payload");

            // 1. 提取序号与多态 Payload 摘要信息（由 Payload 自描述，完全解耦具体实现）
            int itemIndex = GetItemIndex(property, label);
            string prefix = itemIndex > 0 ? $"{itemIndex}. " : string.Empty;
            string summary = CwcCardDrawerUtil.GetPayloadSummary(payloadProp, fallback: "Item");
            string headerTitle = $"{prefix}{summary}";

            GUIContent cardLabel = new GUIContent(headerTitle, $"Weighted placement item #{itemIndex}: {headerTitle}");

            // 2. 绘制卡片
            CwcCardDrawerUtil.DrawCard(
                position,
                property,
                cardLabel,
                CwcCardDrawerUtil.AccentCyan,
                drawHeaderTags: (tagsRect) =>
                {
                    DrawWeightedHeaderControls(tagsRect, weightProp, minLimitProp, maxLimitProp);
                },
                drawContent: (bodyRect) =>
                {
                    CwcCardDrawerUtil.DrawEntryContent(
                        bodyRect,
                        property,
                        prop => prop.name != "_weight" && prop.name != "_minLimit" && prop.name != "_maxLimit"
                    );
                },
                targetType: targetType,
                headerControlsWidth: HeaderControlsWidth
            );

            EditorGUI.EndProperty();
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private static void DrawWeightedHeaderControls(
            Rect rect,
            SerializedProperty weightProp,
            SerializedProperty minLimitProp,
            SerializedProperty maxLimitProp)
        {
            float spacing = 3f;
            float wFieldWidth = 42f;
            float minFieldWidth = 50f;
            float maxFieldWidth = 54f;

            Rect wRect = new Rect(rect.x, rect.y, wFieldWidth, EditorGUIUtility.singleLineHeight);
            Rect minRect = new Rect(wRect.xMax + spacing, rect.y, minFieldWidth, EditorGUIUtility.singleLineHeight);
            Rect maxRect = new Rect(minRect.xMax + spacing, rect.y, maxFieldWidth, EditorGUIUtility.singleLineHeight);

            float prevLabelWidth = EditorGUIUtility.labelWidth;

            // 1. 权重编辑框
            if (weightProp != null)
            {
                EditorGUIUtility.labelWidth = 13f;
                EditorGUI.PropertyField(wRect, weightProp, new GUIContent("W", "Quota weight ratio (Min: 1)"));
            }

            // 2. 最小保底数量编辑框
            if (minLimitProp != null)
            {
                EditorGUIUtility.labelWidth = 23f;
                EditorGUI.PropertyField(minRect, minLimitProp, new GUIContent("Min", "Minimum guaranteed count (0 for none)"));
            }

            // 3. 最大上限数量编辑框
            if (maxLimitProp != null)
            {
                EditorGUIUtility.labelWidth = 25f;
                EditorGUI.PropertyField(maxRect, maxLimitProp, new GUIContent("Max", "Maximum allowed count (0 for none)"));
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        private static int GetItemIndex(SerializedProperty property, GUIContent label)
        {
            if (property != null)
            {
                string path = property.propertyPath;
                int lastOpen = path.LastIndexOf('[');
                int lastClose = path.LastIndexOf(']');
                if (lastOpen >= 0 && lastClose > lastOpen)
                {
                    string numStr = path.Substring(lastOpen + 1, lastClose - lastOpen - 1);
                    if (int.TryParse(numStr, out int idx))
                    {
                        return idx + 1;
                    }
                }
            }

            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                var match = System.Text.RegularExpressions.Regex.Match(label.text, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int idx))
                {
                    return idx + 1;
                }
            }

            return -1;
        }

        #endregion
    }

    /// <summary>
    /// 动态信用点刷怪调度外壳 (CwcCreditEntryBase) 通用卡片属性绘制器
    /// 遵循 CwcFormula 紧凑卡片排版规范：
    /// 1. Header 标题承担多态信息预览（优先读取 Payload 自身自描述的 SummaryText 契约）；
    /// 2. 固有字段（权重 W）在右侧紧凑右对齐，整列垂直规整对齐；
    /// 3. 展开后直接平铺 Payload 内部字段，完全解耦具体 Payload 实现。
    /// </summary>
    [CustomPropertyDrawer(typeof(CwcCreditEntryBase), useForChildren: true)]
    public class CwcCreditEntryDrawer : PropertyDrawer
    {
        #region 常量与静态 (Constants & Static)

        private const float HeaderControlsWidth = 44f;

        #endregion

        #region 公开重写方法 (Public Methods)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return CwcCardDrawerUtil.CalculateCardHeight(property, () =>
            {
                return CwcCardDrawerUtil.CalculateEntryContentHeight(
                    property,
                    prop => prop.name != "_weight"
                );
            });
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Type targetType = fieldInfo != null ? fieldInfo.FieldType : typeof(CwcCreditEntryBase);

            SerializedProperty weightProp = property.FindPropertyRelative("_weight");
            SerializedProperty payloadProp = property.FindPropertyRelative("_payload");

            // 1. 提取序号与多态 Payload 摘要信息（由 Payload 自描述，完全解耦具体实现）
            int itemIndex = GetItemIndex(property, label);
            string prefix = itemIndex > 0 ? $"{itemIndex}. " : string.Empty;
            string summary = CwcCardDrawerUtil.GetPayloadSummary(payloadProp, fallback: "Cost: 0  (0)");
            string headerTitle = $"{prefix}{summary}";

            GUIContent cardLabel = new GUIContent(headerTitle, $"Encounter roster item #{itemIndex}: {headerTitle}");

            // 2. 绘制卡片
            CwcCardDrawerUtil.DrawCard(
                position,
                property,
                cardLabel,
                CwcCardDrawerUtil.AccentOrange,
                drawHeaderTags: (tagsRect) =>
                {
                    DrawCreditHeaderControls(tagsRect, weightProp);
                },
                drawContent: (bodyRect) =>
                {
                    CwcCardDrawerUtil.DrawEntryContent(
                        bodyRect,
                        property,
                        prop => prop.name != "_weight"
                    );
                },
                targetType: targetType,
                headerControlsWidth: HeaderControlsWidth
            );

            EditorGUI.EndProperty();
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private static void DrawCreditHeaderControls(Rect rect, SerializedProperty weightProp)
        {
            if (weightProp == null) return;

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 13f;
            EditorGUI.PropertyField(rect, weightProp, new GUIContent("W", "Selection weight ratio (Min: 1)"));
            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        private static int GetItemIndex(SerializedProperty property, GUIContent label)
        {
            if (property != null)
            {
                string path = property.propertyPath;
                int lastOpen = path.LastIndexOf('[');
                int lastClose = path.LastIndexOf(']');
                if (lastOpen >= 0 && lastClose > lastOpen)
                {
                    string numStr = path.Substring(lastOpen + 1, lastClose - lastOpen - 1);
                    if (int.TryParse(numStr, out int idx))
                    {
                        return idx + 1;
                    }
                }
            }

            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                var match = System.Text.RegularExpressions.Regex.Match(label.text, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int idx))
                {
                    return idx + 1;
                }
            }

            return -1;
        }

        #endregion
    }
}
