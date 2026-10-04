using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Editor
{
    /// <summary>
    /// CwcSceneDirector 自定义 Inspector 检视面板
    /// 支持运行时实时监控各模块运行状态、优先级与调试交互
    /// </summary>
    [CustomEditor(typeof(CwcSceneDirector))]
    public class CwcSceneDirectorEditor : UnityEditor.Editor
    {
        #region 常量与静态 (Constants & Static)

        private static readonly Color HeaderBgColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        private static readonly Color RunningColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        private static readonly Color PausedColor = new Color(0.95f, 0.75f, 0.25f, 1f);
        private static readonly Color InactiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        #endregion

        #region 私有字段 (Private Fields)

        private SerializedProperty _isPausedProp;
        private SerializedProperty _enableLifecycleLogProp;

        #endregion

        #region Unity 生命周期 (Unity Lifecycle)

        private void OnEnable()
        {
            _isPausedProp = serializedObject.FindProperty("_isPaused");
            _enableLifecycleLogProp = serializedObject.FindProperty("_enableLifecycleLog");
        }

        #endregion

        #region 公开方法 (Public Methods)

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var director = (CwcSceneDirector)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_isPausedProp, new GUIContent("Pause Director"));
            EditorGUILayout.PropertyField(_enableLifecycleLogProp, new GUIContent("Enable Lifecycle Log"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawModulesOverview(director);
        }

        #endregion

        #region 私有绘制方法 (Private Methods)

        private void DrawModulesOverview(CwcSceneDirector director)
        {
            EditorGUILayout.LabelField("Scene Director Modules", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Modules will be registered and scheduled at runtime. Scene Director supports lazy creation out of the box.", MessageType.Info);
                return;
            }

            var modules = director.ActiveModules;
            int count = modules.Count;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Active Modules: {count}", EditorStyles.boldLabel);

            if (GUILayout.Button(director.IsPaused ? "Resume All" : "Pause All", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                director.IsPaused = !director.IsPaused;
            }
            EditorGUILayout.EndHorizontal();

            if (count == 0)
            {
                EditorGUILayout.LabelField("No active modules currently registered.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < count; i++)
                {
                    var module = modules[i];
                    if (module == null) continue;

                    DrawModuleRow(module, i);
                }
            }

            EditorGUILayout.EndVertical();

            // 运行时自动重绘以刷新模块状态
            Repaint();
        }

        private void DrawModuleRow(CwcSceneDirectorModule module, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.BeginHorizontal();

            // 状态指示色块
            Color originalColor = GUI.color;
            string statusText;
            if (module.IsCompleted)
            {
                GUI.color = InactiveColor;
                statusText = "Completed";
            }
            else if (module.IsPaused)
            {
                GUI.color = PausedColor;
                statusText = "Paused";
            }
            else if (module.IsRunning)
            {
                GUI.color = RunningColor;
                statusText = "Running";
            }
            else
            {
                GUI.color = InactiveColor;
                statusText = "Disabled";
            }

            EditorGUILayout.LabelField($"[{index}] {module.GetType().Name}", EditorStyles.boldLabel);
            GUI.color = originalColor;

            GUILayout.Label(statusText, EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label($"Order: {module.ExecutionOrder}", EditorStyles.miniLabel, GUILayout.Width(60));

            // 操作调试按钮
            if (GUILayout.Button(module.IsPaused ? "Resume" : "Pause", EditorStyles.miniButton, GUILayout.Width(55)))
            {
                module.SetPaused(!module.IsPaused);
            }

            if (GUILayout.Button("Complete", EditorStyles.miniButton, GUILayout.Width(65)))
            {
                module.Complete();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
