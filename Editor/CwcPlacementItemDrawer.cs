using System;
using UnityEditor;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;

namespace Cwcbb.Tools.CwcSceneDirector.Editor
{
    /// <summary>
    /// CwcPlacementItemBase 及其所有派生类的通用卡片自定义绘制器
    /// 遵循 CwcFormula 紧凑卡片排版规范：
    /// 1. 左侧重音线按类型智能着色（交互物为天蓝，信用点敌群为红橙，通用为蓝紫）；
    /// 2. Header 行整合折叠状态、类型名称、放置模式与权重标签；
    /// 3. useForChildren = true 确保用户拓展的任何自定义子类无缝继承此卡片界面；
    /// 4. 支持上下文菜单一键定位并打开子类 C# 脚本文件。
    /// </summary>
    [CustomPropertyDrawer(typeof(CwcPlacementItemBase), useForChildren: true)]
    public class CwcPlacementItemDrawer : PropertyDrawer
    {
        #region 公开重写方法 (Public Methods)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return CwcCardDrawerUtil.CalculateCardHeight(property, () =>
            {
                return CwcCardDrawerUtil.CalculateEntryContentHeight(property, prop => prop.name != "_weight");
            });
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 1. 提取当前属性字段的实际运行时类型
            Type targetType = fieldInfo != null ? fieldInfo.FieldType : typeof(CwcPlacementItemBase);
            string typeName = targetType.Name;
            if (typeName.StartsWith("Cwc")) typeName = typeName.Substring(3);
            if (typeName.EndsWith("PlacementItem")) typeName = typeName.Substring(0, typeName.Length - "PlacementItem".Length);

            // 2. 根据类型分类匹配强调色
            Color accentColor = CwcCardDrawerUtil.AccentPurple;
            bool isInteractable = targetType.Name.Contains("Interactable");
            bool isCredit = typeof(CwcCreditPlacementItemBase).IsAssignableFrom(targetType) || targetType.Name.Contains("Credit");

            if (isInteractable)
            {
                accentColor = CwcCardDrawerUtil.AccentCyan;
            }
            else if (isCredit)
            {
                accentColor = CwcCardDrawerUtil.AccentOrange;
            }

            // 3. 提取 Header 关键状态并构建信息预览标题
            SerializedProperty weightProp = property.FindPropertyRelative("_weight");
            SerializedProperty modeProp = property.FindPropertyRelative("_mode");
            SerializedProperty countProp = property.FindPropertyRelative("_count");
            SerializedProperty costProp = property.FindPropertyRelative("_threatCost") ?? property.FindPropertyRelative("_runtimeThreatCost");

            string modeName = modeProp != null ? ((PlacementMode)modeProp.enumValueIndex).ToString() : "Free";
            int count = countProp != null ? countProp.intValue : 1;
            int cost = costProp != null ? costProp.intValue : 0;

            int itemIndex = GetItemIndex(property, label);
            string prefix = itemIndex > 0 ? $"{itemIndex}. " : string.Empty;

            string extraInfo = string.Empty;
            if (isCredit && cost > 0)
            {
                extraInfo = $" (Cost:{cost})";
            }
            else if (!isInteractable && count > 1)
            {
                extraInfo = $" (x{count})";
            }

            string headerTitle = $"{prefix}{typeName} Item{extraInfo}  [{modeName}]";
            GUIContent cardLabel = new GUIContent(headerTitle, targetType.FullName);

            // 4. 执行卡片绘制 (右侧仅保留 W 编辑框，右对齐)
            CwcCardDrawerUtil.DrawCard(
                position,
                property,
                cardLabel,
                accentColor,
                drawHeaderTags: (tagsRect) =>
                {
                    DrawHeaderControls(tagsRect, weightProp);
                },
                drawContent: (bodyRect) =>
                {
                    CwcCardDrawerUtil.DrawEntryContent(bodyRect, property, prop => prop.name != "_weight");
                },
                targetType: targetType,
                headerControlsWidth: 54f
            );

            EditorGUI.EndProperty();
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private static void DrawHeaderControls(Rect rect, SerializedProperty weightProp)
        {
            if (weightProp == null) return;

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 14f;
            EditorGUI.PropertyField(rect, weightProp, new GUIContent("W", "Quota weight ratio (Min: 1)"));
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
