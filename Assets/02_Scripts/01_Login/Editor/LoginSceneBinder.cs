using TeaFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeaGame.Login.Editor
{
   /// <summary>
   /// 登录场景迁移工具。
   /// 菜单：Thrud/Bind LoginScene
   /// 作用：把场景中挂旧 <see cref="LoginControl"/> 的对象迁移为 <see cref="LoginViewController"/>：
   ///   1. 读取旧组件序列化字段（遮罩/音频源/演出参数）
   ///   2. 移除旧组件，挂载新组件
   ///   3. 回填字段（含目标场景 key，默认 2MainScene）
   /// 由编辑器执行，避免手改场景 YAML。
   /// </summary>
   public static class LoginSceneBinder
   {
      [MenuItem("Thrud/Bind LoginScene")]
      public static void BindLoginScene()
      {
         var scene = SceneManager.GetActiveScene();
         var legacyControls = Object.FindObjectsOfType<LoginControl>();
         if (legacyControls.Length == 0)
         {
            Debug.LogWarning($"[LoginSceneBinder] 当前场景 {scene.name} 未找到 LoginControl 组件，跳过。");
            return;
         }

         var buttons = Object.FindObjectsOfType<Button>();
         if (buttons.Length == 0)
         {
            Debug.LogWarning("[LoginSceneBinder] 场景中未找到 Button 组件，startButton 留空，请在 Inspector 手动绑定开始按钮。");
         }

         var boundCount = 0;
         foreach (var legacy in legacyControls)
         {
            var go = legacy.gameObject;

            // 1. 从旧组件读取序列化字段（私有字段经 SerializedObject 访问）
            var legacySo = new SerializedObject(legacy);
            var mask = legacySo.FindProperty("mask")?.objectReferenceValue;
            var audioSource = legacySo.FindProperty("audioSource")?.objectReferenceValue;
            var lerpTime = legacySo.FindProperty("lerpTime")?.floatValue ?? 1f;
            var volumeTarget = legacySo.FindProperty("alVolumeTarget")?.floatValue ?? 0.7f;
            var lerpCurve = legacySo.FindProperty("lerpCurve")?.animationCurveValue;

            // 2. 移除旧组件，挂载新组件
            Object.DestroyImmediate(legacy);
            var view = go.AddComponent<LoginViewController>();

            // 3. 回填字段
            var so = new SerializedObject(view);
            SetObject(so, "mask", mask);
            SetObject(so, "audioSource", audioSource);
            SetObject(so, "startButton", buttons.Length > 0 ? buttons[0] : null);
            so.FindProperty("lerpTime").floatValue = lerpTime;
            so.FindProperty("volumeTarget").floatValue = volumeTarget;
            if (lerpCurve != null)
            {
               so.FindProperty("lerpCurve").animationCurveValue = lerpCurve;
            }

            so.FindProperty("mainSceneKey").stringValue = "2MainScene";
            so.ApplyModifiedPropertiesWithoutUndo();

            boundCount++;
            Debug.Log($"[LoginSceneBinder] 已迁移 {go.name}：LoginControl → LoginViewController（目标 key=2MainScene）。");
         }

         EditorSceneManager.MarkSceneDirty(scene);
         EditorSceneManager.SaveOpenScenes();
         Debug.Log($"[LoginSceneBinder] 迁移完成：{boundCount} 个对象。若 startButton 未绑定请手动处理。");
      }

      private static void SetObject(SerializedObject so, string propertyName, Object value)
      {
         var property = so.FindProperty(propertyName);
         if (property != null)
         {
            property.objectReferenceValue = value;
         }
      }
   }
}
