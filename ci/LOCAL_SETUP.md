# 路径 A · 本机跑 Unity EditMode 测试（G4/G5/G7）

适用场景：你自己的电脑（有正常网络）装好 Unity 后，本地一键跑核心循环单测，
不依赖 GitHub / 云端 CI / `UNITY_LICENSE` secret。

> 当前沙箱（WorkBuddy 运行环境）网络拦截了 Unity 下载通道，无法在此装 Unity；
> 本文件供你在**自己本机**使用。

---

## 1. 安装 Unity 2022.3.20f1（个人版免费）

1. 下载安装 **Unity Hub**：https://unity.com/download
2. 打开 Hub → 用你的 Unity 账号登录（首次会要求激活许可证）。
3. `Installs` → `Add` → 选 **Unity 2022.3.20f1**（2022 LTS 线，精确到该补丁版以匹配 CI 锁版）。
   - 模块按需勾选；跑 EditMode 测试**不需要** Android/iOS 模块，但可以顺手装以防后续真机。
4. 安装位置：默认即可。
   - 管理员安装 → `C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe`
   - **非管理员（--scope user）安装 → `C:\Users\<你>\AppData\Local\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe`**
   - `run-ci.sh` 两种路径都已探测，无需手动指定。

## 2. 激活个人版 license（必须在 Hub GUI 里点，无法脚本绕过）

- Hub → `Preferences` → `Licenses` → `Activate New License` → 选 **Personal**（个人版）。
- 填一份简短的非营收声明，完成激活。
- 激活后 license 文件落在：`%APPDATA%\Unity\Unity_lic.ulf`
  （即 `C:\Users\<你>\AppData\Roaming\Unity\Unity_lic.ulf`）

## 3. 跑测试（两种入口）

### 入口一：CI 一键脚本（推荐）
在本仓库**根目录**（含 `game/` 的那层）执行：
```bash
bash game/ci/run-ci.sh
```
- 层 0（G1/G2/G3 契约门）秒级跑完。
- 层 1（G4/G5/G7）自动探测到本机 Unity，跑 EditMode 单测，结果在 `artifacts/editmode.xml`。
- 想强制指定 Unity 路径（探测不到时）：
  ```bash
  UNITY_PATH="C:/Users/<你>/AppData/Local/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe" bash game/ci/run-ci.sh
  ```

### 入口二：Unity 直接跑
```bash
"<Unity.exe 路径>" -batchmode -nographics -projectPath "<仓库>/game" -runTests -testPlatform EditMode -testResults artifacts/editmode.xml -logFile -
```

## 4. 预期结果

- 全绿：`✅ G4/G5 Unity 单测全绿` → 核心循环（拂尘/拼合/抉择/归档）真机编译+运行验证通过。
- 失败：看 `artifacts/editmode.xml` 与 Unity 日志，通常是编译期问题（R-3）。把报错贴回对话，主理人跟进修。

## 5. 与路径 B（云端）的关系

- 路径 A（本机）和路径 B（GitHub Actions + `UNITY_LICENSE`）二选一即可验证 G4/G5/G7。
- 本机验证通过后，仍建议把代码 push 到 GitHub 让云端 CI 常态化守护（路径 B 只需配 secret，不需本机装 Unity）。
- ci.yml 锁版 `unityVersion: 2022.3.20f1`，与路径 A 安装的版本保持一致，避免两端行为差异。
