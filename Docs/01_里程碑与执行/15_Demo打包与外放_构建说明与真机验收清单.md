# Demo 打包与外放 · 构建说明与真机验收清单 v0

> 日期：**2026-09-15**。对应 13 §3.0/§3.3 的产出项「一键构建脚本雏形 + 真机验收清单 v0」。
> 本文 = **可执行的操作手册**：怎么出包（§3–§5）、出包后怎么验（§6）、坏了查哪里（§7）。
> 上游依据：`13_手游试玩Demo_规划.md`（P8 打包验收 / §3.3 T2）、`../03_专项设计与重构/14_真机资源与战斗装配_分阶段修复指南.md`（P0 退出条件）、
> `../05_开发日志/09_开发日志_会话交接.md`（关键坑 25–30）。凡与本文冲突，以代码与真机实测为准，并回来改本文。

---

## 1. 目的与产物

| 项 | 内容 |
|----|------|
| 目的 | 把「产出」跑通：一条命令出**可安装、可外放**的 Android 候选包，并用一份清单判定它能不能外放 |
| 工具 | `Assets/02_Scripts/Tools/Editor/AndroidDemoBuilder.cs`（`TeaGame.Tools.Editor`，菜单 `Thrud/Demo/Android/`） |
| 产物 | `Build/Android/ThrudDemo_<版本>_<versionCode>_<release\|dev>.apk` + `build_report_<版本>_<versionCode>_<mode>.txt`（`Build/`、`*.apk` 已 .gitignore，不入库） |
| 参考辅具 | `Thrud/Demo/Characters/`（角色编目 AB 标记/构建/校验，14 文档 T0-1/T0-2） |
| 验收 | §6《真机验收清单 v0》，逐项勾选后填 §8 交付记录 |

**一句话口令**：切平台（1）→ 前置检查（0）→ 一键出包（5）→ 装包 → 按 §6 逐项验收。

---

## 2. 现状基线（2026-09-15 核对）

| 事实 | 值 | 来源 |
|------|----|------|
| 最近 APK | `ThrudDemo_alpha.0.47_1_release.apk`，**189.2 MB**，2026-09-10 20:29 | `Build/Android/` |
| 最近 AB 构建 | 2026-09-12 15:54（含 `battle_characters` **44.7 MB**） | `Assets/StreamingAssets/AssetBundles/Android/` |
| AB 产物合计 | **131.1 MB**（9 个 bundle 文件 + 9 个 `.manifest` + `asset_bundle_config.bin`） | 同上 |
| **孤儿产物（浪费包体）** | `battleteamcatalog_asset`（09-12 旧默认名，**42.7 MiB / 44,740,328 B**）已不在 AB 清单里，但**仍会随 StreamingAssets 整体进 APK** | 同上 + `asset_bundle_config.bin` 字符串核对 |
| 包体目标 | < 200 MB（13 §3.3）→ **余量已不足 11 MB**；清掉上面那个孤儿可立刻省回约 45 MB | 13 §3.3 |
| Build Settings 启用场景 | `0ConfigScene` → `1LoginScene` → `2MainScene` → `BattleGraybox` | `ProjectSettings/EditorBuildSettings.asset` |

> ⚠️ **当前最大的一个坑**：P0 的 `battle_characters` AB 是 **09-12** 才构建的，而最近一次 APK 是 **09-10** ——
> **P0 修复（角色资源走显式 AB）从来没有进过包**。所以「真机进关没角色」这个问题在现有 APK 上必然复现，
> 必须**重新走一遍 §4 的一键构建**才算验证过 P0。出包后先看 §6-A3 的两条启动日志。
>
> 💡 **顺手能省 42.7 MiB**：AB 产物目录里的 `battleteamcatalog_asset` 是 09-12 改名前的旧产物，**已不在 AB 清单里**
> （核对方式：`asset_bundle_config.bin` 里只有 `battle_characters`），但 StreamingAssets 是**整体进 APK** 的，
> 所以它是实打实的死重。出包前跑一次 **`7. 清理 AB 产物目录中的孤儿文件`**，包体余量立刻从 <11 MiB 变成 ~53 MiB。
> `0. 前置检查` 会自动列出这类文件并给出体积。
>
> 📏 **单位口径**：本文体积一律按 **1 MB = 1024 KB（MiB）** 计。换算成十进制时 189.2 MiB = 198.4 MB ——
> 也就是说 **13 文档的「包体 < 200MB」按十进制算，当前包已经基本顶格**，清孤儿不是可选项而是必做项。

---

## 3. 一次性环境前置（只在换机器/换 Unity 时装）

1. **Unity**：`2022.3.60f1c1`（Unity 中国版，`c1` 后缀；本工程**不是 Unity 6**），路径 `D:\Program Files\Unity\Editor\2022.3.60f1c1\Editor\Unity.exe`。
2. **Android 模块**：Unity Hub → 该版本 → 添加模块 → `Android Build Support`（含 `SDK & NDK Tools` + `OpenJDK`）。缺模块时切平台会直接失败（09 关键坑 5 同源）。
3. **设备**：Android 真机开启「USB 调试」，`adb devices` 能看到设备；机器需支持 `arm64-v8a`（脚本固定 IL2CPP + ARM64）。
4. **机内设置**（脚本每次构建幂等写入，无需手改，仅供核对）：包名 `com.i000stea.thrudremark`、IL2CPP + ARM64、横屏 `LandscapeRight`、MinSdk 28、debug keystore。

---

## 4. 一键构建使用说明（`Thrud/Demo/Android/`）

| 菜单 | 做什么 | 何时用 | 预计耗时 |
|------|--------|--------|----------|
| **0. 构建前置检查 (只读)** | 只读门禁：编译状态 / 目标平台 / 关键设置 / Build Settings 场景 / `Assets/link.xml` / 编目 AB 标记 / AB 产物是否齐备与过期 | **每次出包前先跑**，Console 全 ✅ 再往下 | 秒级 |
| **1. Switch Platform to Android** | 切 Android 目标并（幂等）写入关键设置 | 只在新机器或从别的平台回来时跑一次 | 3–10 分钟（含 IL2CPP 编译） |
| **2. Build AssetBundles (Android)** | `ResKitEditorAPI.BuildAssetBundles()` + **回读产物校验**（每个已标记 bundle 都要在盘上，且 `asset_bundle_config.bin` 存在） | 改过任何被标 AB 的资产（面板 prefab、角色/武器 SO、`BattleTeamCatalog.asset`）后 | 1–3 分钟 |
| **3. Build APK (Release · debug keystore)** | 非 Development，debug 签名，用于外放 | 出真机包 | 首次 5–15 分钟；增量 2–5 分钟 |
| **4. Build APK (Development · 联调)** | Development 构建（可挂调试/看日志） | 需要 Unity 调试器或详细日志时 | 同上 |
| **5. Build All (One-Click · 校验 + AB + APK Release)** | **门禁 → 打 AB → 校验 AB → 再过一次门禁 → 出 Release APK → 写构建报告 → 打开产物目录** | 日常出包就用这条 | 3–18 分钟 |
| **6. 版本号 +1** | `bundleVersion` 末位数字 +1，并同步 `versionCode` +1 | 要把这一包留档/外放时（否则同名包会被覆盖） | 秒级 |
| **7. 清理 AB 产物目录中的孤儿文件** | 删掉「在盘上但不在 AB 清单里」的旧产物（连同 `.meta`） | 前置检查报出孤儿产物时（改过 bundle 名之后常见）；**出包前建议跑一次** | 秒级 |

**每次构建脚本自动做的事**：写入包名/IL2CPP+ARM64/横屏/MinSdk 28/关闭自定义 keystore（= debug 签名，13 §3.3「debug 签名 apk 直接外放」）；场景列表取 Build Settings 里 **enabled** 的条目；AB 文件名含版本号与 `release|dev`。

**为什么必须先切平台、不能「自动切完接着出包」**：`SwitchActiveBuildTarget` 会触发脚本重编译与域重载，同一菜单调用里的后续步骤随时会被打断；更危险的是 AB **按"当前活动目标"打包**（`ResKitEditorAPI` 内部取 `EditorUserBuildSettings.activeBuildTarget`），目标是 Windows 时会打出 Windows 的 AB，而 APK 是 Android → 真机面板全空。因此菜单 2–5 在目标非 Android 时**直接中止并提示**。

**Console 关键字（成功路径）**：
```
[T2] 构建前置检查：  …  门禁结果：通过
[T2] AB 产物齐备：8 个已标记 bundle + 清单，合计 131.x MB（Assets/StreamingAssets/AssetBundles/Android）
[T2] ⚠️ AB 产物目录有 1 个未被清单引用的文件（合计 44.7 MB）：battleteamcatalog_asset。…（跑菜单 7 清理）
[T2] ✅ APK 构建成功：Build/Android/ThrudDemo_alpha.0.48_2_release.apk（190.x MB，用时 3xx s）
[T2] 构建报告已写出：Build/Android/build_report_alpha.0.48_2_release.txt
```

---

## 5. 每次出包必过的门禁（脚本已自动化，这里是人工核对口径）

| # | 门禁 | 不通过的后果 | 处理 |
|---|------|--------------|------|
| G1 | **0 个编译错误**（Console 无 CS 报错） | IL2CPP 出包必失败 | 先修编译；本机无 dotnet SDK 时见 §10 的单文件编译校验 |
| G2 | **活动目标是 Android** | AB 打成错误平台 → 真机面板全空 | 菜单 1 切平台，等编译结束再出包 |
| G3 | **`Assets/Data/BattleTeamCatalog.asset` 已标 AB**（规范名 `battle_characters`） | 角色/武器资源不进包 → 真机进关无角色、退回主场景（09 关键坑 28/30） | `Thrud/Demo/Characters/1`（标记）→ `2`（构建并校验） |
| G4 | **`Assets/link.xml` 存在且保留 `MeshCollider`** | 真机 `Can't add component because class 'MeshCollider' doesn't exist!`（09 关键坑 27） | 补回 `Assets/link.xml`；不要关 Strip Engine Code |
| G5 | **Build Settings 场景齐备且文件存在** | 包内缺场景 → 启动即黑屏/流程断 | 核对 `0Config`/`1Login`/`2Main`/`BattleGraybox` 四个 enabled |
| G6 | **AB 产物齐备且不过期** | 包内是旧资源（改了模型/SO 但没重打） | 菜单 2 重打；`0. 前置检查` 会提示"编目依赖里有比 AB 产物更新的资产" |
| G7 | **包体 < 200 MB** | 外放分发不便（当前 189 MB，余量 <11 MB） | 先跑菜单 7 清孤儿产物（当前可省 ~45 MB）；仍超限再按 14 文档 P3 做 AB 划分（抽 `ui_shared`、去掉重复依赖闭包） |
| G8 | **AB 产物目录无孤儿文件** | 旧 bundle 残留会原样进 APK，白占包体 | 菜单 7 清理（`0. 前置检查` 会列出文件名与体积） |

---

## 6. 真机验收清单 v0（装包后逐项勾选）

> 用法：每项给「通过标准」和「不通过查哪里」；不通过就在 §8 记录现象 + 日志，再按 §7 定位。
> 分组字母（A–H、S）与 §5 的门禁编号（G1–G8）**互不相干**：门禁是出包前在编辑器里过的，清单是装包后在真机上过的。
> 建议采集日志：`adb logcat -s Unity:V`（或 Unity 的 Development 包 + `adb logcat | findstr`）。

### A. 安装与启动
- [ ] **A1 安装**：`adb install -r Build/Android/ThrudDemo_<版本>_<code>_release.apk` 成功，桌面出现应用（debug 签名，首次安装需允许「未知来源」）。
- [ ] **A2 冷启动**：点开应用 10 秒内进入 `1LoginScene` 界面，无黑屏、无闪退、无 ANR。
- [ ] **A3 启动日志（P0 关键，必看）**：logcat 同时出现下面**两条**——
  - `[ResKit] 初始化校验通过：…`（面板清单可用）
  - `[BattleAssets] 角色/武器资源已从 AB 加载并常驻：battle_characters/BattleTeamCatalog`（角色资源可用）
  且**不出现**：`Failed to Create Res`、`角色/武器编目校验失败`、`class 'MeshCollider' doesn't exist`。
  → 缺第二条：见 G3（编目没标 AB / 没重打 AB）；缺第一条：见 G6。
- [ ] **A4 横屏**：全程横屏，无中途旋屏、无左右半屏黑边。
- [ ] **A5 安全区**：刘海/挖孔/圆角处不遮挡按钮与文本（首版未做 `Screen.safeArea` 适配，13 §3.1 列为真机微调项——记录偏移量即可，不算失败）。

### B. 最小链路（T0 的核心验收）
- [ ] **B1** 登录 → 主菜单：可进入主菜单，**主菜单面板不空屏**（有按钮和文字）。
- [ ] **B2** 选关：选关面板可打开、可选 `level_01`（不空屏、不卡死）。
- [ ] **B3** 编队：编队面板可打开，4 槽可见，能点选角色/武器；**面板不空屏**（不出现只有底图没有内容的空白弹窗）。
- [ ] **B4** 进关：点「开始出战」→ 加载遮罩 → 进入战斗场景。
- [ ] **B5 出 rig（P0 关键）**：战斗里**角色模型出现**、材质正常**不发粉/不发白**（发粉 = shader/资源没进包）。
- [ ] **B6 退回**：战斗内暂停 → 退出 → 能回到 `2MainScene`，且主菜单可再次操作（不残留黑屏）。
- [ ] 记录：屏幕上出现任何 `Failed to Create Res …` / `无法装配出战编队` 即本条不通过。

### C. 触屏操作（T1）
- [ ] **C1 移动**：左下摇杆按住拖动，角色按方向移动（含侧向）；松手停。
- [ ] **C2 镜头**：屏幕中央区域单指拖拽，镜头旋转；不与按钮热区互相抢（当前已知风险：中央拖拽区与按钮边缘重叠，记录复现位置）。
- [ ] **C3 开火**：右下大键按住持续开火，松开停火；子弹可见、命中敌人有效。
- [ ] **C4 冲刺/翻滚**：冲刺键按住加速、按下瞬间翻滚等价于 Shift。
- [ ] **C5 技能**：E / Q 可释放，CD 与能量表现符合预期（Q 需能量）。
- [ ] **C6 切人**：◀ ▶ 循环切人，切人后模型/血量/弹匣随之变化，旧角色阵亡后自动切下一个存活成员。
- [ ] **C7 暂停**：右上暂停键可开/关暂停面板。
- [ ] **C8 多指**：摇杆 + 开火 + 切人三指同时按不串味（多指互斥为已知待微调项，记录现象）。
- [ ] **C9 防误触**：连点/滑动边缘不误触发退出或重开；Android 返回键行为符合预期（不直接丢到桌面/不卡死）。

### D. 面板不空屏（T0 的阻断风险项）
- [ ] **D1** 主菜单 / 选关 / 编队 / 触摸层 / 结算 / 暂停 **六个面板逐个打开一次，全部有内容**。
- [ ] **D2 重开本关后再进关**：战斗内「重开本关」→ 触摸层面板**仍然出现**（09 关键坑 25 的核心用例：AB 预检过早回收会导致面板反复加载或不出现）。
- [ ] **D3** 反复进出战斗 2 次，触摸层/结算面板仍可开（面板表无死条目 → 无 `MissingReferenceException`）。

### E. 战斗与数值（T3 基线，P7）
- [ ] **E1 时长**：`level_01` 三站打完约 90 s（教学 ~25 s → 压力 ~45 s → 收尾 ~20 s）。
- [ ] **E2 玩家血量**：MaxHp ≈ 400，普通弹幕单发 ≈ 40（约扛 8–10 发）。
- [ ] **E3 元素**：四种元素敌人/子弹表现正常（冰减速、火 DoT、电麻痹溅射、风易伤聚怪）。
- [ ] **E4 敌人**：巡弋者/炮台/导弹/地雷四类行为正常；地雷感应圈与爆炸圈可见。
- [ ] **E5 结算**：胜利 → 结算面板出现且可「重开/返回主菜单」；失败（全灭）→ 可立即「再来一次」。
- [ ] **E6 队伍 HUD**：队伍血条/成员阵亡表现与 PC 编辑器一致。
- [ ] 备注：涉及伤害数值的改动前先读 `../06_问题存档/12_伤害构成_怀疑存档.md`。

### F. 性能与发热（粗测，13 §3.3）
- [ ] **F1 帧率**：压力波（10+ 敌 + 弹幕）下主观流畅，无肉眼可见的长时间掉帧；有条件记录平均 FPS。
- [ ] **F2 发热/耗电**：连续玩 5 分钟不发烫到烫手，无温控降频导致的明显卡顿。
- [ ] **F3 内存**：5 分钟内不出现 OOM 闪退（尤其切场景 2 次以上之后）。

### S. 中断与恢复（Suspend：切后台 / 锁屏 / 来电）
- [ ] **S1** 中途切后台再回来：不闪退、画面恢复、可继续操作。
- [ ] **S2** 锁屏/来电打断后回来：同上。
- [ ] **S3** 旋转设备/插拔耳机不引发崩溃。

### H. 包体与分发
- [ ] **H1 包体**：APK < 200 MB（当前基线 189 MB，余量 <11 MB；出包前先跑菜单 7 清孤儿产物，超出按 §7 处理）。
- [ ] **H2 版本可区分**：文件名/包信息里有唯一 `版本 + versionCode`；本次出包前执行过菜单 6（或确认版本未被覆盖）。
- [ ] **H3 构建报告**：`Build/Android/build_report_*.txt` 已生成，门禁结论留档。
- [ ] **H4 分发**：debug 签名 apk 直接发给测试者，附 §6 的验收结果与已知问题（正式签名后续补，13 §3.3）。

---

## 7. 失败速查

| 症状 | 根因 | 处理 / 依据 |
|------|------|-------------|
| 真机进关**没有角色**、直接退回主场景 | 编目 AB 没标/没进包/名字与常量脱钩 | 看 A3 日志：缺 `[BattleAssets] 已从 AB 加载并常驻` → 跑 `Thrud/Demo/Characters/1` + `2` 再出包；09 关键坑 28/30、14 文档 D2/D3 |
| 面板**空屏**（主菜单/编队/结算/触摸层） | AB 未进包 / ResKit 未初始化 / 面板表死条目 | 看 A3：缺 `[ResKit] 初始化校验通过` → 重打 AB（菜单 2）；触摸层反复不出现见 09 关键坑 25；跨场景丢失见关键坑 19 |
| `Failed to Create Res. Not Find By ResSearchKeys` | (bundle, asset) 不在 AB 清单里 | 先跑 `0. 前置检查`；09 关键坑 22/23/29 |
| `class 'MeshCollider' doesn't exist!` | Strip Engine Code 剥离了运行时动态 AddComponent 的类型 | 补 `Assets/link.xml`（G4）；09 关键坑 27 |
| 角色材质**发粉/发白** | shader 变体或贴图没进包 | 检查 RealToon/URP shader 是否在被标 AB 的依赖闭包里；核对 AB 产物体积 |
| 包体超过 200 MB | ① AB 目录有孤儿产物（旧 bundle 名残留）② `main_menupanel`/`loadinggroup` 等面板包重复带依赖闭包 | ① 菜单 7 清理（当前可省 ~45 MB）② 按 14 文档 P3：抽 `ui_shared`、取消被场景引用的面板 prefab 的 AB 标记 |
| APK 装不上 | 签名/架构/`MinSdk` | 确认 debug 签名（脚本已强制 `useCustomKeystore=false`）、ARM64 设备、Android 9+ |
| 编辑器里一切正常、真机失败 | 编辑器专属回落（AssetDatabase / 序列化引用）掩盖了真机缺失环节 | 一律先怀疑回落：09 关键坑 30 的教训；把 `0. 前置检查` + A3 日志当第一手证据 |
| 编辑器里面板打不开（SimulationMode 关闭后） | 本工程只构建了 Android AB 目录 | 编辑器联调保持 ResKit 窗口 SimulationMode 勾选；`ResKitBootstrap` 注释已说明 |

---

## 7.5 真机日志怎么筛（正向匹配 + 本机脚本）

整机 logcat 里 Unity 的日志占比不到 3%（实测一份 1707 行的 ColorOS 日志里 `tag=Unity` 只有 31 行），
其余是 `gameoptHal` / `oplus_nfc` / `SnapAlloc` / `qdgralloc` / `BufferQueue*` / `CCodec*` 之类的系统噪声。
**优先用正向匹配，不要靠 ignore 列表——漏掉一个 tag 就漏掉一条错误。**

### 采集（保留可筛选的格式）

```powershell
# 先拿到应用 pid（进程活着时）
adb shell pidof com.i000stea.thrudremark        # 例：30712

# A. 只看 Unity 相关 tag（最省噪，日常首选）
adb logcat -v threadtime -s Unity:V UnityPlayer:V CRASH:V AndroidRuntime:V DEBUG:V mono:V il2cpp:V > Logs\unity.log
#   说明：-s 等价于「列出这些 tag + 末尾 *:S」，即"正向匹配，其余全静音"

# B. 只看本应用进程（任何 tag）——查原生渲染/编解码/音频问题时用
adb logcat -v threadtime --pid 30712 > Logs\pid.log

# C. 应用进程 + 只要 V/W/E/F（B 太吵时的折中）
adb logcat -v threadtime --pid 30712 | Select-String ' [VWEF] '
```

### 已经落盘的日志：用本仓脚本过滤

`Build\_check\filter-unity-log.ps1`（纯 ASCII，PS 5.1 可跑），三档 `-Mode`，实测效果对 `Logs/10191847.log`（1707 行）：

| 模式 | 保留 | 用途 |
|---|---|---|
| `unity`（默认） | **33** | 只有 `Unity` / `IL2CPP` tag —— 日常看这个 |
| `piderr` | 113 | 本进程的 V/W/E/F + 全部 Unity 行 —— "能跑但不对"时看这个 |
| `pid` | 472 | 本进程全量 —— 查渲染/编解码/音频时看这个 |

```powershell
# 直接把筛出来的行打屏
powershell -NoProfile -ExecutionPolicy Bypass -File Build\_check\filter-unity-log.ps1 -Path Logs\10191847.log

# 摘要模式：按 tag 计数 + 列出全部 ERROR/WARN + 被丢掉噪声的 top10（一眼看出漏了什么）
powershell -NoProfile -ExecutionPolicy Bypass -File Build\_check\filter-unity-log.ps1 -Path Logs\10191847.log -Mode piderr -Summary

# 落盘成小文件，再贴给他人/会话
powershell -NoProfile -ExecutionPolicy Bypass -File Build\_check\filter-unity-log.ps1 -Path Logs\10191847.log -Mode unity -Out Logs\10191847.unity.log
```

脚本的匹配逻辑（也是自己写过滤时的口径）：

1. **PID 自动识别**：这段日志里第一条 `tag=Unity` 的行的 PID 就是应用 pid（无需手工找）；
2. **PID 正向匹配**：`unity` 模式不看 pid，`pid`/`piderr` 模式只保留该 pid 的行——比 tag 白名单更不容易漏（Unity 的崩溃/日志也会以 `CRASH`/`DEBUG`/`AndroidRuntime`/`libc` 出现，且这些 tag 来自同一 pid）；
3. **tag 白名单**：`Unity` / `UnityPlayer` / `CRASH` / `DEBUG` / `AndroidRuntime` / `mono` / `il2cpp` / `MonoDroid` + 应用包名本身；
4. **格式**：`-v threadtime` 变体 `日期 时间 PID-TID tag 进程名 级别 消息`（部分 OEM ROM 多一列进程名，脚本的正则已兼容）。

### 如果确实想用 ignore 列表（排除法）

只在"已经把正向匹配跑过、确认还剩哪几类噪声"时才用，且**排除项要写在日志尾部**而不是日志头部：

```powershell
# logcat 侧：正向匹配 + 逐个静音（等价写法，*:S 必须在最后）
adb logcat -v threadtime Unity:V UnityPlayer:V CRASH:V *:S

# 文件侧：用实测噪声 tag 表排除（本机 ColorOS 实测 top）
$noise = 'gameoptHal|oplus_|Oplus[A-Z]|SnapAlloc|qdgralloc|\[UAH_CLIENT\]|COSA->|statsd|vendor\.qti|BufferQueue|BLASTBufferQueue|SurfaceView|Surface\b|CCodec|Codec2|AudioStream|AudioTrack|AudioSystem|Adreno|vulkan|ReflectedParamUpdater|InsetsController|ViewRootImpl|ResourcesManagerExtImpl'
Get-Content Logs\10191847.log | Select-String -Pattern $noise -NotMatch
```

⚠️ 排除法的固有风险：**新增一类噪声就永远看不见它**；而正向匹配的失败方式是"少看一条"，排除法的失败方式是"漏掉一条真错误"。真机排障一律用前者。

---

## 8. 交付记录（每出一版填一行）

| 版本 | versionCode | 构建时间 | APK 体积 | 门禁 | 真机验收 | 结论 / 已知问题 |
|------|-------------|----------|----------|------|----------|-----------------|
| alpha.0.47 | 1 | 2026-09-10 20:29 | 189.2 MB | 未跑（工具当时无门禁） | 部分（暴露 `14` 文档的 D1/D2/D4） | **P0 修复未进包**，进关无角色 |
| alpha.0.4x | | | | | | 待出包：跑菜单 5，按 §6 逐项验收 |

---

## 9. v0 的边界（明确没做的）

- **不做正式签名**：debug keystore 只适合内测外放；正式签名（keystore + 密码入 CI 变量）后续补。
- **不做自动化/CI**：全部是编辑器菜单；`unity` CLI 未在 PATH，`-batchmode` 出包链路（`unity build`）未接。
- **不做 AAB / Play 上架**：只出 APK。
- **不做 `Screen.safeArea` 适配 / 按钮触感反馈 / 升降键**：13 §3.1 的真机微调项，本清单只做记录。
- **不做安装自动化**：采用 `adb install -r`。
- **清单 v0 未覆盖**：热更链路（HybridCLR + YooAsset，属 M0/T7）、多机型矩阵、iOS。
- **门禁的"过期判定"是粗略的**：只比编目依赖的资产修改时间与 AB 产物时间，改脚本不会让它报警（脚本不进 AB，属预期）。

---

## 10. 附：无 Editor 时的单文件编译校验（本机可用）

本机没有 dotnet SDK，但**有 Unity 自带的 Roslyn**，可对单个 `.cs` 做真实的语法/API 校验（本文 §4 的脚本就是这么验的，实测 `exit=0`）：

```powershell
$data='D:\Program Files\Unity\Editor\2022.3.60f1c1\Editor\Data'
$csc="$data\MonoBleedingEdge\lib\mono\msbuild\Current\bin\Roslyn\csc.exe"
$refs=@("$data\Managed\UnityEditor.dll","$data\Managed\UnityEditor.CoreModule.dll")
$refs+=(Get-ChildItem "$data\Managed\UnityEngine" -Filter *.dll | % FullName)
foreach($n in 'mscorlib.dll','System.dll','System.Core.dll'){ $p="$data\MonoBleedingEdge\lib\mono\4.7.1-api\$n"; if(Test-Path $p){$refs+=$p} }
$refs+="$data\NetStandard\ref\2.1.0\netstandard.dll"                          # 少了它 → CS0012 netstandard 2.1
$refs+=(Get-ChildItem "$data\NetStandard\compat\2.1.0\shims\netfx" -Filter *.dll | % FullName)   # 少了它 → CS0518 预定义类型缺失
$refs=$refs | Where-Object { Test-Path $_ }                                  # 必须过滤：UnityEditor.CoreModule.dll 在 2022.3 并不存在
& "$data\MonoBleedingEdge\bin\mono.exe" $csc -nologo -target:library -langversion:9 `
  -out:"Build\_check\out.dll" -r:($refs -join ';') "Build\_check\Stub.cs" "Assets\...\你的文件.cs"
```

- 被工程内部类型（QFramework / TeaGame）引用时，用一份最小 stub 顶掉即可，例如：
  `namespace QFramework { public static class ResKitEditorAPI { public static void BuildAssetBundles() { } } }`。
- **三个必踩的坑**：① 不加 `netstandard 2.1` → `CS0012`；② 不加 `shims\netfx` → `CS0518`（预定义类型未定义）；
  ③ 不过滤不存在的引用路径 → `CS0006`。
- **局限**：只验证该文件本身 + Unity API 用法，**不等于整工程编译**；`Assets/` 内不要放临时文件，临时工程放 `Build/`（已 gitignore）。

---

## 11. 变更记录

| 日期 | 变更 |
|------|------|
| 2026-09-15 | 首版：构建说明 + 门禁 G1–G8 + 真机验收清单 v0（A–H、S）+ 失败速查 + 交付记录表 + 单文件编译校验方法；同步 `AndroidDemoBuilder.cs` 补门禁/AB 产物校验/孤儿产物清理/体积与报告/版本号菜单（*C# 已用 §10 方式验证 exit=0，编辑器内一键出包待用户执行*） |
