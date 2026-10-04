using System;
using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Editor
{
    /// <summary>
    /// 卡片式 Inspector 绘制公共工具类
    /// 提供类似 CwcFormula 的卡片底板、重音线、紧凑 Header 与上下文菜单支持
    /// 保持完全原生兼容（支持 Undo/Redo、Prefab 覆盖、多选与字段拖拽）
    /// </summary>
    public static class CwcCardDrawerUtil
    {
        #region 常量与静态成员 (Constants & Static)

        public const float HeaderHeight = 20f;
        public const float CardPadding = 2f;
        public const float AccentBarWidth = 3f;

        public static readonly Color AccentCyan = new Color(0.25f, 0.75f, 0.95f, 0.95f);     // 交互物/空间
        public static readonly Color AccentOrange = new Color(0.95f, 0.45f, 0.25f, 0.95f);   // 战斗敌群/狂潮
        public static readonly Color AccentPurple = new Color(0.65f, 0.45f, 0.95f, 0.95f);   // 加权调度外壳
        public static readonly Color AccentGreen = new Color(0.35f, 0.85f, 0.45f, 0.95f);    // 兵种单位/实体

        private static Type _clipboardType;
        private static string _clipboardJson;

        #endregion

        #region 公开卡片绘制与计算方法 (Public Methods)

        /// <summary>
        /// 计算卡片折叠/展开的总高度
        /// </summary>
        public static float CalculateCardHeight(SerializedProperty property, Func<float> getInnerHeight = null)
        {
            if (!property.isExpanded)
            {
                return HeaderHeight + CardPadding;
            }

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float innerHeight = getInnerHeight != null ? getInnerHeight() : CalculateChildrenHeight(property);
            return HeaderHeight + innerHeight + CardPadding + spacing;
        }

        /// <summary>
        /// 计算属性内部所有直接子字段的展开高度总和
        /// </summary>
        public static float CalculateChildrenHeight(SerializedProperty property)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float total = 0f;

            SerializedProperty copy = property.Copy();
            SerializedProperty end = copy.GetEndProperty();
            bool enterChildren = true;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                total += EditorGUI.GetPropertyHeight(copy, true) + spacing;
            }

            return total;
        }

        /// <summary>
        /// 绘制卡片底板、重音线与卡片内部容器
        /// </summary>
        public static void DrawCard(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Color accentColor,
            Action<Rect> drawHeaderTags,
            Action<Rect> drawContent,
            Type targetType = null,
            float headerControlsWidth = 150f)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // 1. 卡片底板与左侧重音线
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);
            Rect accentRect = new Rect(position.x + 1f, position.y + 1f, AccentBarWidth, position.height - 2f);
            EditorGUI.DrawRect(accentRect, accentColor);

            Rect contentRect = new Rect(position.x + 5f, position.y + 1f, position.width - 10f, position.height - 2f);
            Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, HeaderHeight);

            // 2. 绘制卡片 Header
            DrawHeader(headerRect, property, label, drawHeaderTags, targetType, headerControlsWidth);

            if (!property.isExpanded)
            {
                return;
            }

            // 3. 绘制展开后的内容
            float currentY = headerRect.yMax + spacing;
            Rect bodyRect = new Rect(contentRect.x, currentY, contentRect.width, contentRect.height - HeaderHeight - spacing);

            if (drawContent != null)
            {
                drawContent(bodyRect);
            }
            else
            {
                DrawDefaultChildren(bodyRect, property);
            }
        }

        /// <summary>
        /// 计算调度外壳展开内容的高度（支持过滤固有字段，并将 _payload 内部子字段平铺展开）
        /// </summary>
        public static float CalculateEntryContentHeight(SerializedProperty property, Func<SerializedProperty, bool> filter = null)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float total = 0f;

            SerializedProperty copy = property.Copy();
            SerializedProperty end = copy.GetEndProperty();
            bool enterChildren = true;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;

                if (filter != null && !filter(copy))
                {
                    continue;
                }

                // 如果是 _payload 且具有内部复合结构，平铺计算其直接子字段
                if (copy.name == "_payload" && copy.hasVisibleChildren)
                {
                    SerializedProperty pCopy = copy.Copy();
                    SerializedProperty pEnd = pCopy.GetEndProperty();
                    bool pEnter = true;
                    while (pCopy.NextVisible(pEnter) && !SerializedProperty.EqualContents(pCopy, pEnd))
                    {
                        pEnter = false;
                        total += EditorGUI.GetPropertyHeight(pCopy, true) + spacing;
                    }
                    continue;
                }

                total += EditorGUI.GetPropertyHeight(copy, true) + spacing;
            }

            return total;
        }

        /// <summary>
        /// 绘制调度外壳展开内容（支持过滤固有字段，并将 _payload 内部子字段直接平铺展开）
        /// </summary>
        public static void DrawEntryContent(Rect position, SerializedProperty property, Func<SerializedProperty, bool> filter = null)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float currentY = position.y;

            SerializedProperty copy = property.Copy();
            SerializedProperty end = copy.GetEndProperty();
            bool enterChildren = true;

            EditorGUI.indentLevel++;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;

                if (filter != null && !filter(copy))
                {
                    continue;
                }

                // 如果是 _payload 且具有内部复合结构，直接平铺渲染其子字段
                if (copy.name == "_payload" && copy.hasVisibleChildren)
                {
                    SerializedProperty pCopy = copy.Copy();
                    SerializedProperty pEnd = pCopy.GetEndProperty();
                    bool pEnter = true;
                    while (pCopy.NextVisible(pEnter) && !SerializedProperty.EqualContents(pCopy, pEnd))
                    {
                        pEnter = false;
                        float childHeight = EditorGUI.GetPropertyHeight(pCopy, true);
                        Rect childRect = new Rect(position.x, currentY, position.width, childHeight);
                        EditorGUI.PropertyField(childRect, pCopy, true);
                        currentY += childHeight + spacing;
                    }
                    continue;
                }

                float h = EditorGUI.GetPropertyHeight(copy, true);
                Rect rect = new Rect(position.x, currentY, position.width, h);
                EditorGUI.PropertyField(rect, copy, true);
                currentY += h + spacing;
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 遍历并绘制属性的所有直接可见子字段（完全兼容原生 Undo/Prefab）
        /// </summary>
        public static void DrawDefaultChildren(Rect position, SerializedProperty property)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float currentY = position.y;

            SerializedProperty copy = property.Copy();
            SerializedProperty end = copy.GetEndProperty();
            bool enterChildren = true;

            EditorGUI.indentLevel++;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                float childHeight = EditorGUI.GetPropertyHeight(copy, true);
                Rect childRect = new Rect(position.x, currentY, position.width, childHeight);

                EditorGUI.PropertyField(childRect, copy, true);
                currentY += childHeight + spacing;
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 快捷查找并在代码编辑器中打开指定类型的 C# 脚本文件
        /// </summary>
        public static void OpenScriptForType(Type type)
        {
            if (type == null) return;

            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }

            EditorUtility.DisplayDialog("Notice", $"Could not find script file for {type.Name}.\nPlease ensure the script file name matches the class name ({type.Name}.cs).", "OK");
        }

        #endregion

        #region 私有绘制方法 (Private Methods)

        private static void DrawHeader(
            Rect headerRect,
            SerializedProperty property,
            GUIContent label,
            Action<Rect> drawHeaderTags,
            Type targetType,
            float headerControlsWidth = 150f)
        {
            Event evt = Event.current;

            // 1. 最右侧菜单按钮
            float menuBtnWidth = 16f;
            Rect menuBtnRect = new Rect(headerRect.xMax - menuBtnWidth, headerRect.y + 1f, menuBtnWidth, EditorGUIUtility.singleLineHeight);

            // 2. 右侧扩展控件/标签区 (按传入宽度动态排布)
            float tagsWidth = drawHeaderTags != null ? headerControlsWidth : 0f;
            Rect tagsRect = new Rect(menuBtnRect.x - 3f - tagsWidth, headerRect.y + 1f, tagsWidth, EditorGUIUtility.singleLineHeight);

            // 3. 左侧折叠箭头与标题
            Rect foldoutRect = new Rect(headerRect.x + 1f, headerRect.y + 1f, 14f, EditorGUIUtility.singleLineHeight);
            float titleWidth = Mathf.Max(20f, tagsRect.x - foldoutRect.xMax - 3f);
            Rect titleRect = new Rect(foldoutRect.xMax + 2f, headerRect.y + 1f, titleWidth, EditorGUIUtility.singleLineHeight);

            // 折叠切换
            property.isExpanded = GUI.Toggle(foldoutRect, property.isExpanded, GUIContent.none, EditorStyles.foldout);

            // 标题显示与点击整行切换折叠
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Ellipsis
            };
            GUI.Label(titleRect, label, titleStyle);

            if (evt.type == EventType.MouseDown && evt.button == 0 && titleRect.Contains(evt.mousePosition))
            {
                property.isExpanded = !property.isExpanded;
                evt.Use();
                GUI.changed = true;
            }

            // 4. 绘制自定义标签/徽章
            if (drawHeaderTags != null)
            {
                drawHeaderTags(tagsRect);
            }

            // 5. 更多选项菜单按钮
            GUIContent moreIcon = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_more" : "more");
            if (moreIcon == null || moreIcon.image == null)
            {
                moreIcon = new GUIContent("...", "Options");
            }
            else
            {
                moreIcon.tooltip = "Options";
            }

            if (GUI.Button(menuBtnRect, moreIcon, EditorStyles.iconButton))
            {
                ShowCardMenu(menuBtnRect, property, targetType, isContextMenu: false);
            }

            // 支持右键 Header 弹出上下文菜单
            if (evt.type == EventType.ContextClick && headerRect.Contains(evt.mousePosition))
            {
                ShowCardMenu(headerRect, property, targetType, isContextMenu: true);
                evt.Use();
            }
        }

        private static void ShowCardMenu(Rect positionRect, SerializedProperty property, Type targetType, bool isContextMenu)
        {
            var menu = new GenericMenu();
            var targetObject = property.serializedObject.targetObject;
            object targetObj = GetTargetObjectOfProperty(property);
            Type currentType = targetObj?.GetType() ?? targetType;

            // 1. 复制操作 (Copy)
            if (targetObj != null)
            {
                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    _clipboardType = targetObj.GetType();
                    _clipboardJson = EditorJsonUtility.ToJson(targetObj);
                    Debug.Log($"[CwcSceneDirector] 已复制 {_clipboardType.Name} 配置至剪贴板。");
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Copy"));
            }

            // 2. 粘贴操作 (Paste)
            bool canPaste = _clipboardType != null && !string.IsNullOrEmpty(_clipboardJson) && targetObj != null &&
                (currentType == null || currentType.IsAssignableFrom(_clipboardType) || _clipboardType.IsAssignableFrom(currentType));

            if (canPaste)
            {
                string pasteLabel = $"Paste ({GetShortTypeName(_clipboardType)})";
                menu.AddItem(new GUIContent(pasteLabel), false, () =>
                {
                    property.serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(targetObject, "Paste Card Settings");
                    EditorJsonUtility.FromJsonOverwrite(_clipboardJson, targetObj);
                    EditorUtility.SetDirty(targetObject);
                    property.serializedObject.Update();
                    GUI.changed = true;
                    Debug.Log($"[CwcSceneDirector] 已粘贴 {_clipboardType.Name} 配置。");
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            // 3. 恢复默认 (Reset to Default)
            menu.AddItem(new GUIContent("Reset to Default"), false, () =>
            {
                Undo.RecordObject(targetObject, "Reset Card Property");
                ResetPropertyValues(property);
                property.serializedObject.ApplyModifiedProperties();
            });

            // 4. 打开脚本 (Open Script)
            Type scriptType = currentType ?? targetType;
            if (scriptType != null)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Open Script"), false, () =>
                {
                    OpenScriptForType(scriptType);
                });
            }

            if (isContextMenu)
            {
                menu.ShowAsContext();
            }
            else
            {
                menu.DropDown(positionRect);
            }
        }

        private static string GetShortTypeName(Type type)
        {
            if (type == null) return "None";
            string name = type.Name;
            if (name.EndsWith("Entry")) name = name.Substring(0, name.Length - "Entry".Length);
            else if (name.EndsWith("Item")) name = name.Substring(0, name.Length - "Item".Length);
            else if (name.EndsWith("Payload")) name = name.Substring(0, name.Length - "Payload".Length);
            return name;
        }

        private static void ResetPropertyValues(SerializedProperty property)
        {
            SerializedProperty copy = property.Copy();
            SerializedProperty end = copy.GetEndProperty();
            bool enterChildren = true;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                switch (copy.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        copy.intValue = 0;
                        break;
                    case SerializedPropertyType.Boolean:
                        copy.boolValue = false;
                        break;
                    case SerializedPropertyType.Float:
                        copy.floatValue = 0f;
                        break;
                    case SerializedPropertyType.String:
                        copy.stringValue = string.Empty;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        copy.objectReferenceValue = null;
                        break;
                    case SerializedPropertyType.Enum:
                        copy.enumValueIndex = 0;
                        break;
                }
            }
        }

        #endregion

        #region 反射与多态目标对象提取 (Reflection Utilities)

        /// <summary>
        /// 提取 Payload 的紧凑信息预览文本
        /// 遵循开闭原则，优先调用目标对象实现的 SummaryText 属性或 GetSummaryText() 方法，
        /// 使调度外壳完全解耦具体的 Payload 内部字段与实现细节
        /// </summary>
        public static string GetPayloadSummary(SerializedProperty payloadProp, string fallback = "Empty")
        {
            if (payloadProp == null) return fallback;

            object targetObj = GetTargetObjectOfProperty(payloadProp);
            if (targetObj == null) return fallback;

            Type type = targetObj.GetType();

            // 1. 优先调用自描述的 SummaryText 属性
            var prop = type.GetProperty("SummaryText", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanRead)
            {
                object val = prop.GetValue(targetObj, null);
                if (val != null)
                {
                    string str = val.ToString();
                    if (!string.IsNullOrEmpty(str)) return str;
                }
            }

            // 2. 兼容 GetSummaryText() 方法
            var method = type.GetMethod("GetSummaryText", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(string))
            {
                object val = method.Invoke(targetObj, null);
                if (val != null)
                {
                    string str = val.ToString();
                    if (!string.IsNullOrEmpty(str)) return str;
                }
            }

            // 3. 通用启发式保底（若为首个可见 GameObject/资产引用）
            var copy = payloadProp.Copy();
            var end = copy.GetEndProperty();
            bool enter = true;
            while (copy.NextVisible(enter) && !SerializedProperty.EqualContents(copy, end))
            {
                enter = false;
                if (copy.propertyType == SerializedPropertyType.ObjectReference && copy.objectReferenceValue != null)
                {
                    return copy.objectReferenceValue.name;
                }
            }

            return fallback;
        }

        /// <summary>
        /// 从 SerializedProperty 解析并获取底层真实的 C# 目标对象实例
        /// </summary>
        public static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            string path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length; i++)
            {
                string element = elements[i];
                if (element.Contains("["))
                {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetFieldValue(obj, elementName, index);
                }
                else
                {
                    obj = GetFieldValue(obj, element);
                }

                if (obj == null) return null;
            }

            return obj;
        }

        private static object GetFieldValue(object source, string name)
        {
            if (source == null) return null;
            Type type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (f != null) return f.GetValue(source);

                var p = type.GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (p != null) return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetFieldValue(object source, string name, int index)
        {
            var enumerable = GetFieldValue(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        #endregion
    }
}
