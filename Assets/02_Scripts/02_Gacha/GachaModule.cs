namespace TeaGame
{
   /// <summary>
   /// 抽卡域（<c>TeaGame.Gacha</c>）占位框架。
   ///
   /// 存在理由（P1A 2026-09-18）：
   /// 1. `02_Gacha/` 原本是空目录，其 asmdef 因此每次都让 Unity 输出
   ///    「will not be compiled, because it has no scripts associated with it」——本类让该程序集非空、消除噪音；
   /// 2. 作为 **M2 抽卡全量重构** 的落点标记：该域当前的真实内容只有 `Legacy/` 下的存档参考实现（不参与运行时）。
   ///
   /// 纪律：**本类只做占位，不要在这里预先发明抽象**（架构/事件/接口/基类一律等 M2 设计定稿后再落）。
   /// 设计文档入口见 `Docs/04_模块设计/` 的 Gacha 模块；决策背景见 16 文档 §5A.1b。
   /// </summary>
   public static class GachaModule
   {
      /// <summary>域标识（日志/排查用）。</summary>
      public const string DomainName = "Gacha";

      /// <summary>该域是否已具备可运行的抽卡实现（M1 阶段恒为 false，M2 落地后置 true）。</summary>
      public const bool IsImplemented = false;
   }
}
