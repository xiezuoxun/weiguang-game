# 《微光归处》Phase 6 用户本机执行手册（步骤①准备）

> 适用：你（或美术同学）在本机有正常网络的 Windows/Mac 电脑 + Unity 2022.3.20f1 + Hub（已激活个人版 license）。
> 目的：把沙箱产出的 Phase 6-A/6-B 代码与美术规格，落到你本机 Unity 工程并生产二进制资产。
> 创建：2026-08-25

---

## ⚠️ 关键前提：沙箱无法 push，请用 bundle 同步

本 WorkBuddy 沙箱**无法连接 GitHub（网络墙）**，所以 `b506c03`/`3702b06` 等 Phase 6 commit **不在你的 GitHub 远端**。你有两种方式把新文件拿回本机：

### 方式 A（推荐）：git bundle 离线同步
沙箱已生成 `weiguang-6ab.bundle`（含 `35a8695..master` 共 4 个 commit：9b791b2/009826a/打磨 + b506c03/6-A + 3702b06/6-B）。
1. 从沙箱取出 `weiguang-6ab.bundle`（通过 WorkBuddy 文件下载 / 云盘 / U 盘等任意通道传到你本机）。
2. 在你本机 `weiguang-game` 仓库根目录执行：
   ```bash
   git pull /path/to/weiguang-6ab.bundle master
   # 或：git fetch /path/to/weiguang-6ab.bundle master && git merge FETCH_HEAD
   ```
3. 验证：`git log --oneline -4` 应出现 `3702b06`（6-B 前置）与 `b506c03`（6-A 资产排期）。

### 方式 B：若你的 Unity 工程目录就是本 WorkBuddy 工作区
直接打开 `game/` 文件夹即是最新代码，跳过同步。

---

## 步骤①：本机环境确认
- [ ] Unity Hub 已装，Unity 2022.3.20f1 编辑器已装，个人版 license 已激活
- [ ] Package Manager 已装 **Test Framework**（`com.unity.test-framework`）
- [ ] `game/` 已同步到本机（方式 A 或 B）

## 步骤②：打开工程
1. Unity Hub → Open → 选 `game/` 文件夹（或 `weiguang-game` 仓库根，确保 `game/Assets` 被识别为项目资产）。
2. 首次打开 Unity 会生成 `ProjectSettings/`、`Library/`、manifest 解析（README 注明工程文件未入库，Unity 自动生成，**正常**）。
3. 确认 Console **无红色报错**（若有，优先看是否缺 `Weiguang.Runtime` asmdef 引用——见下方"已知坑"）。

## 步骤③：生产二进制资产（按 `production/phase6-asset-tasks.md`）
美术同学需在 Unity / 外部工具（Aseprite/Photoshop/Blender 导出 / 音频 DAW）生成以下 PNG/音频，命名**严格对齐 CSV**：

| 类别 | 资产 | 数量 | 放置路径（Resources/ 下） |
|---|---|---|---|
| B1 | 尘遮罩噪点 `dust_noise` + 微光光晕 `glow_halo` | 2 贴图 | `Resources/Shaders/` 或挂 DustReveal 材质 |
| B2 | 槽位底图 `it_*_board`（5 物件）+ 碎片 `fr_001`…`fr_013` | 5 + 13 | `Resources/Slots/` `Resources/Fragments/` |
| B3 | 纸签双态 `choice_tab` / `choice_tab_selected` | 2 | `Resources/Choices/` |
| 5缺口 | 物件立绘/缩略(10) + 低语笺(1) + 客户符号(4) + 手势笔触(1) + 承托底(1) | 17 | `Resources/Items/` `Resources/Whisper/` `Resources/Clients/` `Resources/Cursor/` `Resources/Backdrop/` |
| 音频 | SFX×5（`sfx_reveal`/`sfx_snap`/`sfx_paper`/`sfx_archive`/`sfx_ui`）+ BGM×3 | 8 | `Resources/Audio/` |

> 命名必须与 `production/art-spec.md` §0.2 / §2.3 / §3 对齐（`fr_001`…`fr_013`、`it_watch` 等）。`ArtAssetLoader` 默认从 `Resources/` 加载，接 Addressables 时改 `LoadSprite` 实现（TODO 已标注）。

## 步骤④：挂载场景骨架（6-B）
1. 新建空场景（如 `Assets/Scenes/Main.unity`），加一个空 GameObject 挂 `GameBootstrap`（Runtime 层已自带 CSV 槽 `commissionsCsv/clientsCsv/itemsCsv`）。
2. 把 `Assets/Data/*.csv` 拖入 `GameBootstrap` 的三个 TextAsset 槽。
3. 在每个物件 GameObject 上挂对应 `*VisualBridge`（如 DustGrid 容器挂 `RevealVisualBridge`，需带 `Renderer` 组件挂 DustReveal 材质）。
4. `GameBootstrap.Awake` 会自动 `FindObjectsOfType<ArtBridgeBase>()` 并 `Bind(_bus)`（已实现），无需手动连线。

## 步骤⑤：跑测试验收
```bash
# 层0 契约门（不需 Unity）
bash game/ci/run-ci.sh
# 预期：G1/G2/G3 PASS，Unity 层 SKIP（run-ci.sh 无 Unity 时 SKIP 文案）
```
- Unity 内：Window → General → Test Runner → **EditMode** → Run All
  - 预期：原有 + 打磨新增（FeedbackHooks 11 例 / Robustness 7 例）全绿
- Unity 内：**PlayMode** → Run All（需先建 `Assets/Tests/PlayMode/ArtAcceptanceTests.cs`，见 `phase6-acceptance.md`）
  - 预期：B1 Shader 编译无粉红 + 资产齐全断言通过

## 步骤⑥：验收后回报沙箱
把以下结果回传，沙箱据此做 Phase 6 收口评审：
- [ ] Shader 在 URP 2D 编译通过（无粉色材质）
- [ ] B1 跨 0.25/0.50/0.75 亮度差 ≥15%（脉冲可见）
- [ ] B2 吸附回弹、B3 选中高亮 ≤250ms
- [ ] EditMode + PlayMode 测试全绿截图/日志

---

## 已知坑（本机可能遇到）
1. **EditMode 测试编译失败**：若报 `Weiguang.Tests.EditMode` 找不到 `SessionRunner`/`GameBootstrap`/`RuntimeQuality`，说明 `Weiguang.Tests.EditMode.asmdef` 未引用 `Weiguang.Runtime`。**此问题已在沙箱修复**（本 bundle 含修复后的 asmdef，直接引用 `Weiguang.Runtime`），若你本机另有旧副本请同步为引用 Runtime 的版本。
2. **ProjectSettings 缺失**：首次打开 Unity 自动生成，无需手动补；但若 `.gitignore` 把它们排除导致协作者拉取后缺设置，按需 `git add ProjectSettings/` 入库。
3. **DustReveal.shader 粉色**：检查 `RenderPipeline=UniversalPipeline` 标签与 URP 2D Renderer 是否启用（Project Settings → Graphics → Scriptable Render Pipeline Settings 指向 URP 资产）。
