using System;
using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Editor
{
    /// <summary>
    /// CwcPlacementSettings 宏观选点与物理空间探测配置的卡片式自定义属性绘制器
    /// 遵循 CwcFormula 紧凑卡片排版规范：
    /// 1. 左侧重音线使用天蓝色 (AccentCyan)；
    /// 2. Header 行集成折叠状态与 NavMesh/探测关键状态标签；
    /// 3. useForChildren = true 确保子类继承自动生效；
    /// 4. 支持上下文菜单一键定位并打开 C# 脚本文件与重置默认值。
    /// </summary>
    [CustomPropertyDrawer(typeof(CwcPlacementSettings), useForChildren: true)]
    public class CwcPlacementSettingsDrawer : PropertyDrawer
    {
        #region 公开重写方法 (Public Methods)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return CwcCardDrawerUtil.CalculateCardHeight(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Type targetType = fieldInfo != null ? fieldInfo.FieldType : typeof(CwcPlacementSettings);

            SerializedProperty navMeshProp = property.FindPropertyRelative("UseNavMesh");
            SerializedProperty rayHeightProp = property.FindPropertyRelative("RaycastOriginHeight");

            bool useNavMesh = navMeshProp != null && navMeshProp.boolValue;
            float rayHeight = rayHeightProp != null ? rayHeightProp.floatValue : 10f;

            string headerTitle = !string.IsNullOrEmpty(label.text) && !label.text.StartsWith("Element")
                ? label.text
                : "Placement Physical Settings";

            GUIContent cardLabel = new GUIContent(headerTitle, targetType.FullName);

            CwcCardDrawerUtil.DrawCard(
                position,
                property,
                cardLabel,
                CwcCardDrawerUtil.AccentCyan,
                drawHeaderTags: (tagsRect) =>
                {
                    DrawSettingsBadges(tagsRect, useNavMesh, rayHeight);
                },
                drawContent: (bodyRect) =>
                {
                    CwcCardDrawerUtil.DrawDefaultChildren(bodyRect, property);
                },
                targetType: targetType
            );

            EditorGUI.EndProperty();
        }

        #endregion

        #region 私有辅助方法 (Private Methods)

        private static void DrawSettingsBadges(Rect rect, bool useNavMesh, float rayHeight)
        {
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 10,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.35f, 0.35f, 0.35f) }
            };

            string navText = useNavMesh ? "NavMesh:ON" : "Physics Only";
            GUI.Label(rect, $"{navText}  Ray:{rayHeight:0.#}m", badgeStyle);
        }

        #endregion
    }
}
