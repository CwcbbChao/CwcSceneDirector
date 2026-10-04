using System;
using UnityEditor;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector;
using Cwcbb.Tools.CwcSceneDirector.Editor;

namespace Cwcbb.Tools.CwcSceneDirector.Demo.Editor
{
    /// <summary>
    /// 敌群单兵种编制项 (SquadMemberEntryBase) 紧凑单行自定义绘制器
    /// 遵循 CwcFormula 紧凑单行排版：
    /// 左侧兵种资产选择框 + 中间单体战力消耗微型徽章 + 右侧兵种生成数量输入框
    /// </summary>
    [CustomPropertyDrawer(typeof(SquadMemberEntryBase), useForChildren: true)]
    public class SquadMemberEntryDrawer : PropertyDrawer
    {
        #region 公开重写方法 (Public Methods)

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty unitProp = property.FindPropertyRelative("_unit");
            SerializedProperty countProp = property.FindPropertyRelative("_count");

            float spacing = 4f;
            float countWidth = 65f;
            float costWidth = 60f;
            float unitWidth = Mathf.Max(60f, position.width - countWidth - costWidth - spacing * 2);

            Rect unitRect = new Rect(position.x, position.y + 1f, unitWidth, EditorGUIUtility.singleLineHeight);
            Rect costRect = new Rect(unitRect.xMax + spacing, position.y + 1f, costWidth, EditorGUIUtility.singleLineHeight);
            Rect countRect = new Rect(costRect.xMax + spacing, position.y + 1f, countWidth, EditorGUIUtility.singleLineHeight);

            // 1. 单位资产选择框
            if (unitProp != null)
            {
                EditorGUI.PropertyField(unitRect, unitProp, GUIContent.none);
            }

            // 2. 单体强度开销徽章 (从 ISpawnableUnit 提取)
            int unitCost = 0;
            bool hasUnit = false;
            if (unitProp != null && unitProp.objectReferenceValue != null)
            {
                hasUnit = true;
                if (unitProp.objectReferenceValue is ISpawnableUnit spawnable)
                {
                    unitCost = spawnable.BaseCost;
                }
            }

            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = EditorGUIUtility.singleLineHeight
            };

            string costText = hasUnit ? $"Cost: {unitCost}" : "Cost: -";
            Color prevColor = GUI.color;
            GUI.color = hasUnit ? new Color(1f, 0.85f, 0.6f, 1f) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
            GUI.Label(costRect, costText, badgeStyle);
            GUI.color = prevColor;

            // 3. 数量输入框
            if (countProp != null)
            {
                float prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 14f;
                EditorGUI.PropertyField(countRect, countProp, new GUIContent("x", "Unit count in squad"));
                EditorGUIUtility.labelWidth = prevLabelWidth;
            }

            EditorGUI.EndProperty();
        }

        #endregion
    }
}
