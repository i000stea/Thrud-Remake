# Thrud-Remake — workspace instructions

Unity game project developed through DSH. This file is the project-local layer of
guidance: it applies to every session whose working directory is inside this repository.
It does not override system, developer, or direct user instructions.

## Project facts

Verify these before relying on them; they are the ones that most often change an answer.

- **Unity 2022.3.60f1c1c1** (Unity China build, `c1` suffix). This is **not Unity 6**:
  do not propose Unity 6-only APIs (Render Graph, newer UI Toolkit runtime binding,
  `Awaitable`-based async patterns, etc.) without checking availability first.
- Render pipeline: **URP 14.0.12**, with **RealToon** for character shading and
  **Amplify Shader Editor** for authored shaders. Post-processing goes through the
  Volume framework.
- Frameworks in use: **QFramework** (`Assets/QFramework`: CoreKit / ResKit / UIKit /
  Data / QFrameworkData), **DOTween**, **TextMeshPro**, **Cinemachine 2.10.3**,
  2D sprite + tilemap, `com.unity.ai.navigation` 1.1.5.
- Target platform: **Android**. `Thrud-Remake-a044.apk` at the repo root is a build
  artifact — never commit it, never edit it.
- Game code lives in **`Assets/02_Scripts`**, split by business domain into 9 assemblies
  (2026-09-18, P1A S2/S3). Dependency direction is one-way:
  `TeaGame.Tools` / `TeaGame.Data` → `TeaGame.App` → `TeaGame.Battle.Contracts` → { `TeaGame.Battle`, `TeaGame.Menu` }.
  - `TeaGame.App` = `00_App/` (boot, framework-level login, `SceneFlow`/`SceneRequest`, `ResKitBootstrap`, `UiPanelFlow`, `LoadingGroup`, `Tools/TeaBaseButton`) — **the AOT layer**
  - `TeaGame.Menu` = `01_Menu/` (main menu, level select, formation)
  - `TeaGame.Gacha` = `02_Gacha/` (placeholder domain: `GachaModule.cs` + `Legacy/`, the latter being a **read-only M2 reference implementation restored from git — never wire it into the main flow**, see `02_Gacha/README.md`)
  - `TeaGame.Battle` = `03_Battle/` (minus `Contracts/` and `Editor/`)
  - `TeaGame.Battle.Contracts` = `03_Battle/Contracts/` — the **only** meeting point between Menu and Battle. Add a type here **only if Menu needs to see it**, and bring its public surface along (field types and attribute types), otherwise the contract layer cycles back into `TeaGame.Battle`
  - `TeaGame.Data` = `Data/` (pure data leaf), `TeaGame.Tools` = `Tools/` (generic utilities; **hot update** as of 2026-09-18 — do not add AOT-only dependencies here)
  - `TeaGame.Battle.Editor` / `TeaGame.Tools.Editor` — Editor-only, never runtime
  - Do **not** reintroduce cross-domain references: `Menu → Battle` and `Battle → Menu` are both 0 today. Full map, measured dependency edges and the rollback table: `Docs/03_专项设计与重构/17_代码现状地图.md`
- **AOT / hot-update split (decided 2026-09-18, not yet implemented — HybridCLR lands in P1C)**:
  **`TeaGame.App` is the only AOT assembly; everything else is hot update** (`TeaGame.Data`,
  `TeaGame.Tools`, `TeaGame.Battle.Contracts`, `TeaGame.Battle`, `TeaGame.Menu`, `TeaGame.Gacha`).
  - Hard rule: **an AOT assembly must never reference a hot-update assembly.** Today `TeaGame.App`
    has **no** project references at all. When a type is needed by both sides, move *that type* down
    a layer — as was done for `TeaBaseButton`, moved from `Tools/` into `00_App/Tools/` with its
    `.meta` (GUID `a62d68a4f3d90a84980e72cab2e1bc88` preserved, all 6 asset references intact) —
    rather than pulling a whole assembly into AOT.
  - `TeaGame.App` also owns `00_App/Tools/TeaBaseButton.cs` (used by Login, Menu and Gacha).
  - Hot-update code must never touch `UnityEditor` except behind `#if UNITY_EDITOR`. Verified clean
    on 2026-09-18 (one guarded hit in `BattleSceneRuntimeHost`) — keep it that way.
- Editor-only code belongs in Editor assemblies/folders, never in runtime assemblies.
- Hot update plan (not necessarily implemented yet) is HybridCLR + YooAsset — see
  `Docs/02_系统与玩法设计/02_热更新方案.md`.

## Layout

| Path | Contents |
|---|---|
| `Assets/01_Scenes` | scenes |
| `Assets/02_Scripts` | `00_App` (App) / `01_Menu` (Menu) / `02_Gacha` (Gacha: placeholder + `Legacy/` archive) / `03_Battle` (Battle + `Contracts/` + `Editor/`) / `Data` / `Tools` / `Shaders` / `Other` — see the assembly list above. `Before/` and `UIPanel/` no longer exist (2026-09-18) |
| `Assets/03_Module` | **resources only — 0 `.cs` files** (verified 2026-09-18); not a code directory |
| `Assets/04_Prefabs`, `05_Textures`, `06_Animator`, `08_Fonts` | art and prefab assets |
| `Assets/Data`, `EffectConfig`, `Resources`, `StreamingAssets` | data tables and runtime resources |
| `Assets/Test`, `bin-check` | test scenes / verification helpers |
| `Docs/` | project documentation (Chinese) — see below |
| `Library/`, `Temp/`, `obj/`, `Logs/`, `.vs/`, `UserSettings/` | generated; never edit, never commit |

## Documentation workflow — read before starting

- `Docs/README.md` is the doc index and the map of where everything lives.
- **Read `Docs/05_开发日志/09_开发日志_会话交接.md` first in every session**:
  progress snapshot, code structure, key decisions, pitfalls, commit log.
- Current milestone work is tracked as task cards (`T0.x`–`T7.x`) in
  `Docs/01_里程碑与执行/07_M1战斗灰盒_开发追踪指南.md`.
- `Docs/03_专项设计与重构/17_代码现状地图.md` is the **code map**: domain/assembly layout,
  measured cross-assembly dependency edges, the contract-layer rule, and the
  move-rollback table. Read it before moving or adding files under `Assets/02_Scripts`.
- Pending actions with their sources are collected in `Docs/99_下一步行动/README.md`.
- When you finish a unit of work, update the tracking guide (07), the handoff log (09),
  and the pending list (99); refresh `Docs/README.md` status columns when a document's
  state changed.
- Write project docs in **Chinese**, matching existing tone, numbering and cross-references
  (`Docs/12`, `T3.1C`, `D01`–`D25`). Do not rename or renumber existing documents.

## Commit discipline

- Commit messages are **Chinese, conventional-commit style**:
  `feat(battle): ...`, `fix(ui): ...`, `docs(battle): ...`, `chore(unity): ...`.
- Keep unrelated churn out of business commits. Known churn to commit separately or
  exclude: Unity re-serialization of `ProjectSettings/*` (serialization version bumps),
  generated QFramework `QAssets` constants and panel renames, AssetBundle config refresh,
  and `Assets/08_Fonts/Before/Default.asset`.
- Never commit `Library/`, `Temp/`, `obj/`, `Logs/`, `*.csproj`, `*.sln`, `*.apk`, `*.aab`.

## Verification

- **Live Editor control is not available in this project right now**: the `unity` CLI is
  not on PATH, so `unity_status` / `unity_command` / `unity_eval` fail immediately, and
  the project also lacks `com.unity.pipeline`. Call `unity_status` at most once; when it
  fails, say so and fall back to reading and editing project files — never retry in a loop.
- Without a running Editor you cannot compile C#. Mark such changes **unverified** instead
  of claiming they build; when the user has the Editor open, ask for the console output or
  read fresh entries under `Logs/`.
- **Cheapest real verification (2026-09-18)**: when the user has the Editor open, the compile
  result is readable at `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Find the newest
  asmdef-import / `Csc` block and count `error CS` **only after that line number** — the same
  log keeps stale errors from earlier sessions (e.g. the pre-fix `Bullet_Base` / `PlayerControl`
  errors sit around L76735–L77873, while the clean 20:18 build is much later). Read with
  `Get-Content -Encoding UTF8`; the console mangles UTF-8 otherwise.
- Runtime/play-mode behaviour (battle arena, formation panel, damage, UI flow) is validated
  by the user in the Editor. State the exact scene or prefab path and the concrete steps to
  check, rather than asserting the behaviour works.
- Scene, prefab and `.asset` files are Unity YAML: editing them by hand is a last resort.
  When no Editor is reachable, prefer changing the C#/data source that generates them, and
  tell the user which Editor-side action is still required.
