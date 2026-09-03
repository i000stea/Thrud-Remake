using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TeaGame.Battle.Editor
{
   /// <summary>
   /// 把带 <see cref="EnemyIdAttribute"/> 的字符串字段重绘为"敌人类型下拉"。
   /// 候选来自工程内所有 EnemyCatalog 资产（按目录顺序），选择后写回字符串（底层存 id，
   /// 便于热更新新增敌人类型——只需在 EnemyCatalog 增加条目即可出现在下拉）。
   /// </summary>
   [CustomPropertyDrawer(typeof(EnemyIdAttribute))]
   public class EnemyIdDrawer : PropertyDrawer
   {
      public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
      {
         EditorGUI.BeginProperty(position, label, property);

         var ids = new List<string>();
         var displayNames = new List<string>();
         CollectIds(ids, displayNames);

         if (ids.Count == 0)
         {
            // 尚无 EnemyCatalog 资产：退化为字符串输入并提示
            var fieldRect = new Rect(position.x, position.y, position.width - 90f, position.height);
            EditorGUI.PropertyField(fieldRect, property, label);
            EditorGUI.LabelField(new Rect(position.xMax - 86f, position.y, 86f, position.height),
               "需 EnemyCatalog", EditorStyles.miniLabel);
            EditorGUI.EndProperty();
            return;
         }

         // 选项：当前字符串不在目录时，把原值作为首位占位展示
         var optionValues = new List<string>();
         var optionLabels = new List<string>();
         var current = property.stringValue;

         if (string.IsNullOrEmpty(current))
         {
            optionValues.Add(string.Empty);
            optionLabels.Add("— 请选择敌人 —");
         }
         else if (!ids.Contains(current))
         {
            optionValues.Add(current);
            optionLabels.Add(current + "（未匹配）");
         }

         for (var i = 0; i < ids.Count; i++)
         {
            optionValues.Add(ids[i]);
            var name = string.IsNullOrEmpty(displayNames[i]) ? ids[i] : displayNames[i];
            optionLabels.Add(name);
         }

         var selectedIndex = optionValues.IndexOf(current);
         if (selectedIndex < 0) selectedIndex = 0;

         // 先绘制字段标签，再在剩余区域画下拉（EditorGUI.Popup 无 (Rect,GUIContent,int,string[]) 重载，
         // 需用 PrefixLabel + 无 label 的 Popup(Rect,int,string[])）
         var controlRect = EditorGUI.PrefixLabel(position, label);
         var picked = EditorGUI.Popup(controlRect, selectedIndex, optionLabels.ToArray());
         if (picked >= 0 && picked < optionValues.Count && picked != selectedIndex)
         {
            property.stringValue = optionValues[picked];
         }

         EditorGUI.EndProperty();
      }

      /// <summary>
      /// 汇总工程内所有 EnemyCatalog 资产的 id 与显示名（去重保序）。
      /// </summary>
      private static void CollectIds(List<string> ids, List<string> displayNames)
      {
         var guids = AssetDatabase.FindAssets("t:EnemyCatalog");
         for (var i = 0; i < guids.Length; i++)
         {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(path);
            if (catalog == null || catalog.Entries == null) continue;

            for (var j = 0; j < catalog.Entries.Length; j++)
            {
               var entry = catalog.Entries[j];
               if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
               if (ids.Contains(entry.Id)) continue;

               ids.Add(entry.Id);
               displayNames.Add(entry.DisplayName);
            }
         }
      }
   }
}
