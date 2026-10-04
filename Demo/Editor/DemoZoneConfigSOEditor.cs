using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cwcbb.Tools.CwcSceneDirector.Editor;

namespace Cwcbb.Tools.CwcSceneDirector.Demo.Editor
{
    /// <summary>
    /// DemoZoneConfigSO 及其所有派生类的卡片式自定义检视面板 (CustomEditor)
    /// 遵循 CwcFormula 紧凑卡片排版规范：
    /// 1. 顶部 Header Banner 提供资产摘要与一键打开脚本；
    /// 2. 交互物放置与动态敌群遭遇战各自封装为独立折叠卡片，采用对应重音色；
    /// 3. editorForChildClasses = true 且自动遍历未声明的派生字段，确保用户继承拓展 100% 无缝兼容；
    /// 4. 严格保持 Unity 原生 Undo/Redo 与 Prefab 序列化兼容。
    /// </summary>
    [CustomEditor(typeof(DemoZoneConfigSO), editorForChildClasses: true)]
    public class DemoZoneConfigSOEditor : UnityEditor.Editor
    {
        #region 私有非序列化字段 (Private Fields)

        private SerializedProperty _interactableConfigProp;
        private SerializedProperty _encounterConfigProp;

        private bool _interactablesExpanded = true;
        private bool _encountersExpanded = true;
        private bool _derivedExpanded = true;

        private readonly List<SerializedProperty> _derivedProps = new List<SerializedProperty>();

        #endregion

        #region Unity 编辑器生命周期 (Editor Lifecycle)

        protected virtual void OnEnable()
        {
            _interactableConfigProp = serializedObject.FindProperty("_interactableConfig");
            _encounterConfigProp = serializedObject.FindProperty("_encounterConfig");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Type targetType = target.GetType();

            // 1. 绘制顶部 Banner 卡片
            DrawBanner(targetType);

            EditorGUILayout.Space(4f);

            // 2. 绘制交互物放置卡片 (AccentCyan)
            if (_interactableConfigProp != null)
            {
                DrawInteractablesSection();
                EditorGUILayout.Space(6f);
            }

            // 3. 绘制敌群遭遇战动态导演卡片 (AccentOrange)
            if (_encounterConfigProp != null)
            {
                DrawEncountersSection();
                EditorGUILayout.Space(6f);
            }

            // 4. 自动捕获并绘制用户子类派生字段 (对扩展开放)
            DrawDerivedPropertiesSection();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region 内部排版绘制方法 (Section Drawing)

        /// <summary>
        /// 绘制顶部横幅卡片与快捷操作按钮
        /// </summary>
        protected virtual void DrawBanner(Type targetType)
        {
            Rect bannerRect = EditorGUILayout.GetControlRect(false, 38f);
            GUI.Box(bannerRect, GUIContent.none, EditorStyles.helpBox);

            // 蓝色装饰条
            Rect accentRect = new Rect(bannerRect.x + 1f, bannerRect.y + 1f, 4f, bannerRect.height - 2f);
            EditorGUI.DrawRect(accentRect, CwcCardDrawerUtil.AccentCyan);

            // 标题与说明
            float btnWidth = 85f;
            Rect textRect = new Rect(bannerRect.x + 10f, bannerRect.y + 3f, bannerRect.width - btnWidth - 20f, bannerRect.height - 6f);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.35f, 0.35f, 0.35f) }
            };

            string title = targetType.Name;
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 18f), title, titleStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 16f, textRect.width, 14f), "Data-driven procedural placement & encounter configuration", descStyle);

            // 打开脚本按钮
            Rect btnRect = new Rect(bannerRect.xMax - btnWidth - 6f, bannerRect.y + 8f, btnWidth, 22f);
            if (GUI.Button(btnRect, "Open Script", EditorStyles.miniButton))
            {
                CwcCardDrawerUtil.OpenScriptForType(targetType);
            }
        }

        /// <summary>
        /// 绘制交互物配置分块
        /// </summary>
        protected virtual void DrawInteractablesSection()
        {
            SerializedProperty itemsProp = _interactableConfigProp.FindPropertyRelative("_items");
            int count = itemsProp != null && itemsProp.isArray ? itemsProp.arraySize : 0;

            DrawSectionHeader("Interactables Placement", $"Items: {count}", CwcCardDrawerUtil.AccentCyan, ref _interactablesExpanded);

            if (_interactablesExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_interactableConfigProp, true);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 绘制敌群遭遇战配置分块
        /// </summary>
        protected virtual void DrawEncountersSection()
        {
            SerializedProperty itemsProp = _encounterConfigProp.FindPropertyRelative("_items");
            int count = itemsProp != null && itemsProp.isArray ? itemsProp.arraySize : 0;

            DrawSectionHeader("Enemy Encounter Director", $"Squads: {count}", CwcCardDrawerUtil.AccentOrange, ref _encountersExpanded);

            if (_encountersExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_encounterConfigProp, true);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 自动收集并绘制子类派生字段
        /// </summary>
        protected virtual void DrawDerivedPropertiesSection()
        {
            _derivedProps.Clear();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // 排除 Unity 默认脚本引用和基类已处理的核心字段
                if (prop.name == "m_Script" || prop.name == "_interactableConfig" || prop.name == "_encounterConfig")
                {
                    continue;
                }

                _derivedProps.Add(prop.Copy());
            }

            if (_derivedProps.Count == 0) return;

            DrawSectionHeader("Derived Custom Fields", $"Count: {_derivedProps.Count}", CwcCardDrawerUtil.AccentPurple, ref _derivedExpanded);

            if (_derivedExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < _derivedProps.Count; i++)
                {
                    EditorGUILayout.PropertyField(_derivedProps[i], true);
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 绘制卡片分块标题栏 (Header Bar)
        /// </summary>
        private static void DrawSectionHeader(string title, string badgeText, Color accentColor, ref bool isExpanded)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 24f);
            GUI.Box(headerRect, GUIContent.none, EditorStyles.helpBox);

            // 重音线
            Rect accentRect = new Rect(headerRect.x + 1f, headerRect.y + 1f, 3.5f, headerRect.height - 2f);
            EditorGUI.DrawRect(accentRect, accentColor);

            // 折叠控件与标题
            Rect foldoutRect = new Rect(headerRect.x + 6f, headerRect.y + 4f, 16f, 16f);
            isExpanded = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

            Rect titleRect = new Rect(foldoutRect.xMax + 4f, headerRect.y + 2f, headerRect.width - 150f, 20f);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            GUI.Label(titleRect, title, headerStyle);

            // 点击标题区域可切换折叠
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && titleRect.Contains(evt.mousePosition))
            {
                isExpanded = !isExpanded;
                evt.Use();
                GUI.changed = true;
            }

            // 右侧微型标签
            if (!string.IsNullOrEmpty(badgeText))
            {
                Rect badgeRect = new Rect(headerRect.xMax - 110f, headerRect.y + 2f, 100f, 20f);
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 10,
                    normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.35f, 0.35f, 0.35f) }
                };
                GUI.Label(badgeRect, badgeText, badgeStyle);
            }
        }

        #endregion
    }
}
