using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcSceneDirector.Editor
{
    /// <summary>
    /// CwcSceneEntityManager 自定义 Inspector 面板
    /// 实时显示当前场上实体活跃数量、总开销预算、空间网格统计及超距淘汰状态
    /// </summary>
    [CustomEditor(typeof(CwcSceneEntityManager))]
    public class CwcSceneEntityManagerEditor : UnityEditor.Editor
    {
        #region 公开重写方法 (Public Methods)

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (CwcSceneEntityManager)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Entity Runtime Overview", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime metrics and pool statistics will be displayed when the game is playing.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Active Entities: {manager.ActiveEntityCount}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Threat Cost: {manager.TotalThreatCost}", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Despawn All Entities", EditorStyles.miniButton))
            {
                var activeList = manager.ActiveEntities;
                for (int i = activeList.Count - 1; i >= 0; i--)
                {
                    if (i < activeList.Count && activeList[i] != null)
                    {
                        activeList[i].Despawn();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            Repaint();
        }

        #endregion
    }
}
