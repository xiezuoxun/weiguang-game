# Phase 7 · CI 层1 self-hosted runner 注册手册

> 目的：让 GitHub Actions 的 `unity-tests` job 在你已装 Unity 的本机真实运行，
> 彻底摆脱 docker 镜像 exit 133（SIGTRAP 崩溃）。本机 165/165 即 ground truth。

## 原理
`ci.yml` 的 `unity-tests` 现为 `runs-on: [self-hosted, unity]`。
在你本机注册一个带 `unity` label 的 self-hosted runner 后，GitHub 会把该 job
派到你本机跑；game-ci/unity-test-runner 在 **Windows 本机**上会自动改用本机 Unity
（不走 docker），exit 133 不再发生。

## 前置确认（本机已满足）
- [x] Windows 10/11 x64，已装 Unity 2022.3.20f1 + 编辑器（之前测试全绿已证）
- [x] 本机 `game/` 目录即仓库根（同 `.git`）
- [x] 有 GitHub 仓库 `xiezuoxun/weiguang-game` 的 push 权限（你本机有凭证）

## 步骤（PowerShell，本机一次性）

### 1. 下载 runner
GitHub 仓库网页 → `Settings` → `Actions` → `Runners` →
`New self-hosted runner` → 选 **Windows** / **x64** →
按页面给出的命令下载并解压（建议解压到 `C:\actions-runner\`）。

### 2. 配置（含关键 label）
在解压目录开 **PowerShell（管理员）**，逐行执行页面给的命令，其中
`./config.cmd` 交互时：
- 贴入 `Repository` 的 URL（https://github.com/xiezuoxun/weiguang-game.git）
- 粘贴页面生成的 **token**
- `Runner Name`：直接回车用默认，或填 `weiguang-pc`
- **`Labels` 提示时务必输入 `unity`**（逗号分隔可加 `self-hosted`）—— 这决定它
  能匹配 ci.yml 的 `runs-on: [self-hosted, unity]`
- `Work folder` 回车默认 `_work`
- 其余回车默认

### 3. 常驻运行
```
.\run.cmd
```
保持这个窗口开着（runner 常驻，等待 GitHub 派 job）。
可选：用 `./svc.cmd install` 装成 Windows 服务，免开窗口（开机自启）。

### 4. 推改动触发 CI
runner 跑着时，在本机 `game/` 执行：
```
git add -A && git commit -m "ci: unity-tests 改 self-hosted runner 真绿" && git push origin master
```
（沙箱这份已是同一 `.git`，改动已在；你 push 即可）
push 后在 GitHub `Actions` 标签页看 `unity-tests` 是否变绿、是否显示
`self-hosted` runner 承接。

## 验证（真绿判定）
- `unity-tests` job 状态 = 绿色对勾（不再 `continue-on-error` 软绿）
- 日志应能看到 game-ci 调用 **本机 Unity**（路径含 `C:\Program Files\...Unity\Editor\Unity.exe`，
  而非 `docker run ... game-ci/unity-builder`）
- EditMode 测试数 ≈ 本机 165/165

## 回滚
若本机不想常驻 runner：GitHub `Runners` 页面删掉该 runner，并把 ci.yml 的
`runs-on` 改回 `ubuntu-latest` + 恢复 `continue-on-error: true`（软绿兜底）。
（不推荐回滚——docker 路径 exit 133 未根治。）

## 备注
- runner 进程占用本机少量资源；跑 CI 时 Unity 会短暂占用 CPU/内存，属正常。
- `UNITY_LICENSE` secret 可保留（本机已激活时 game-ci 会复用本机 license，secret 空也不影响）。
- `.codely.packages/` 等 IDE 插件产物已在 `.gitignore` 排除，勿再误提交。
