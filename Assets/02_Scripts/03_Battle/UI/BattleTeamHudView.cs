using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeaGame
{
   /// <summary>
   /// T3.1B 队伍血条列表（灰盒轻量 HUD，运行时生成、事件驱动刷新、不做每帧轮询）。
   /// 布局：战斗 Canvas 左上角，每名出战成员一行 = 槽号+名字 + 血条 + 数值；
   /// 满血绿条、掉血按比例收窄、阵亡行整体置灰显示「阵亡」。
   /// 纯运行时 UI（无 prefab/序列化依赖）；T3.3 正式战斗 HUD 实现后本类收编替换。
   /// </summary>
   public sealed class BattleTeamHudView
   {
      private const float RowHeight = 32f;
      private const float RootWidth = 340f;
      private const float NameWidth = 96f;
      private const float BarWidth = 178f;
      private const float BarHeight = 16f;

      private static Sprite sWhiteSprite;

      private sealed class Row
      {
         public Image BarBack;
         public Image Fill;
         public Text Name;
         public Text Value;
      }

      private readonly RectTransform mRoot;
      private readonly List<Row> mRows = new();

      private BattleTeamHudView(Canvas canvas, int memberCount)
      {
         var rootObject = new GameObject("TeamHud(T3.1B)", typeof(RectTransform));
         mRoot = (RectTransform)rootObject.transform;
         mRoot.SetParent(canvas.transform, false);

         // 左上角锚定（留边 30/40），按成员数向下排布
         mRoot.anchorMin = new Vector2(0f, 1f);
         mRoot.anchorMax = new Vector2(0f, 1f);
         mRoot.pivot = new Vector2(0f, 1f);
         mRoot.anchoredPosition = new Vector2(30f, -40f);
         mRoot.sizeDelta = new Vector2(RootWidth, memberCount * RowHeight);

         var bg = AddImage(mRoot, "Bg", null, new Color(0f, 0f, 0f, 0.32f));
         Stretch(bg.rectTransform);

         for (var i = 0; i < memberCount; i++)
         {
            mRows.Add(CreateRow(i));
         }
      }

      /// <summary>
      /// 创建队伍血条列表（挂到指定 Canvas）。
      /// </summary>
      /// <param name="canvas">战斗 Canvas（MainCanvas）。</param>
      /// <param name="memberCount">出战成员数（建行数，之后不变）。</param>
      /// <returns>视图实例；canvas 为空或成员数为 0 时返回 null。</returns>
      public static BattleTeamHudView Create(Canvas canvas, int memberCount)
      {
         if (canvas == null || memberCount <= 0) return null;
         return new BattleTeamHudView(canvas, memberCount);
      }

      /// <summary>
      /// 刷新全部行的血量/阵亡表现（事件驱动调用）。
      /// </summary>
      public void Refresh(IReadOnlyList<BattleRoleRuntimeData> datas)
      {
         if (mRoot == null || datas == null) return;

         var count = Mathf.Min(mRows.Count, datas.Count);
         for (var i = 0; i < count; i++)
         {
            var row = mRows[i];
            var data = datas[i];
            if (row == null) continue;

            var downed = data == null || data.IsDowned;
            var max = data != null ? Mathf.Max(1f, data.MaxHealth) : 1f;
            var current = data != null && !downed ? Mathf.Max(0f, data.CurrentHealth) : 0f;
            var ratio = Mathf.Clamp01(current / max);

            if (row.Name)
            {
               row.Name.text = data != null ? $"{i + 1}.{data.RoleName}" : $"{i + 1}.--";
               row.Name.color = downed ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }

            var fillColor = downed
               ? new Color(0.45f, 0.45f, 0.45f, 1f)
               : new Color(0.35f, 0.9f, 0.4f, 1f);

            if (row.BarBack)
            {
               row.BarBack.color = downed ? new Color(0.2f, 0.2f, 0.2f, 0.9f) : new Color(0.12f, 0.12f, 0.12f, 0.9f);
            }

            if (row.Fill)
            {
               row.Fill.color = fillColor;
               var size = row.Fill.rectTransform.sizeDelta;
               size.x = BarWidth * ratio;
               row.Fill.rectTransform.sizeDelta = size;
            }

            if (row.Value)
            {
               row.Value.text = downed ? "阵亡" : $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
               row.Value.color = downed ? new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f);
            }
         }
      }

      /// <summary>销毁 HUD 根对象（场景重建/系统销毁时调用，幂等）。</summary>
      public void Dispose()
      {
         mRows.Clear();
         if (mRoot != null)
         {
            Object.Destroy(mRoot.gameObject);
         }
      }

      // ---------------- 构建辅助 ----------------

      private Row CreateRow(int index)
      {
         var rowRoot = new GameObject($"Row_{index}", typeof(RectTransform), typeof(Image));
         var rowRect = (RectTransform)rowRoot.transform;
         rowRect.SetParent(mRoot, false);
         rowRect.anchorMin = new Vector2(0f, 1f);
         rowRect.anchorMax = new Vector2(1f, 1f);
         rowRect.pivot = new Vector2(0f, 1f);
         rowRect.anchoredPosition = new Vector2(0f, -RowHeight * index);
         rowRect.sizeDelta = new Vector2(0f, RowHeight);
         var rowImage = rowRoot.GetComponent<Image>();
         rowImage.color = new Color(0f, 0f, 0f, 0f); // 行透明，由整体 Bg 打底
         rowImage.raycastTarget = false;

         var row = new Row();

         row.Name = AddText(
            rowRect, "Name", 18, TextAnchor.MiddleLeft, Color.white,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(10f, 0f), new Vector2(NameWidth, 0f));

         // 血条底
         var barBack = AddImage(rowRect, "BarBack", null, new Color(0.12f, 0.12f, 0.12f, 0.9f));
         row.BarBack = barBack;
         AnchorLeftCenter(barBack.rectTransform, new Vector2(NameWidth + 14f, 0f), new Vector2(BarWidth, BarHeight));

         // 血条填充（左锚收窄）
         var fill = AddImage(barBack.rectTransform, "Fill", null, new Color(0.35f, 0.9f, 0.4f, 1f));
         row.Fill = fill;
         AnchorLeftCenter(fill.rectTransform, Vector2.zero, new Vector2(BarWidth, BarHeight));

         row.Value = AddText(
            rowRect, "Value", 16, TextAnchor.MiddleRight, new Color(0.92f, 0.92f, 0.92f, 1f),
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f), new Vector2(64f, 0f));

         return row;
      }

      private static Image AddImage(RectTransform parent, string name, Sprite sprite, Color color)
      {
         var go = new GameObject(name, typeof(RectTransform), typeof(Image));
         var rect = (RectTransform)go.transform;
         rect.SetParent(parent, false);
         var image = go.GetComponent<Image>();
         image.sprite = sprite != null ? sprite : WhiteSprite;
         image.type = Image.Type.Simple;
         image.color = color;
         image.raycastTarget = false;
         return image;
      }

      private static Text AddText(
         RectTransform parent,
         string name,
         int fontSize,
         TextAnchor alignment,
         Color color,
         Vector2 anchorMin,
         Vector2 anchorMax,
         Vector2 pivot,
         Vector2 anchoredPosition,
         Vector2 sizeDelta)
      {
         var go = new GameObject(name, typeof(RectTransform), typeof(Text));
         var rect = (RectTransform)go.transform;
         rect.SetParent(parent, false);
         rect.anchorMin = anchorMin;
         rect.anchorMax = anchorMax;
         rect.pivot = pivot;
         rect.anchoredPosition = anchoredPosition;
         rect.sizeDelta = sizeDelta;

         var text = go.GetComponent<Text>();
         text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
         text.fontSize = fontSize;
         text.alignment = alignment;
         text.color = color;
         text.horizontalOverflow = HorizontalWrapMode.Overflow;
         text.verticalOverflow = VerticalWrapMode.Overflow;
         text.raycastTarget = false;
         return text;
      }

      private static void AnchorLeftCenter(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
      {
         rect.anchorMin = new Vector2(0f, 0.5f);
         rect.anchorMax = new Vector2(0f, 0.5f);
         rect.pivot = new Vector2(0f, 0.5f);
         rect.anchoredPosition = anchoredPosition;
         rect.sizeDelta = size;
      }

      private static void Stretch(RectTransform rect)
      {
         rect.anchorMin = Vector2.zero;
         rect.anchorMax = Vector2.one;
         rect.pivot = new Vector2(0.5f, 0.5f);
         rect.offsetMin = Vector2.zero;
         rect.offsetMax = Vector2.zero;
      }

      private static Sprite WhiteSprite
      {
         get
         {
            if (sWhiteSprite == null)
            {
               sWhiteSprite = Sprite.Create(
                  Texture2D.whiteTexture,
                  new Rect(0f, 0f, 1f, 1f),
                  new Vector2(0.5f, 0.5f),
                  100f);
               sWhiteSprite.name = "TeamHudWhite";
            }

            return sWhiteSprite;
         }
      }
   }
}
