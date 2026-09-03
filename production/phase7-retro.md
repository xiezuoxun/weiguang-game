# 《微光归处》Phase 7 复盘（Retrospective）— CI 真绿攻坚

> 编制：游承峰（Weiguang 工作室主理人）　|　日期：2026-09-03
> 关联：Phase 7 收口裁定 `production/phase7-closure-decision.md`（DONE 签收版，含 run #18 终验）
> 一句话：Phase 7 的最大风险不是"功能没做完"，而是 **CI 用"exit 0 假绿"骗过了所有人**。我们花了 #11→#18 共 8 次 run，才把"看似成功、实际没跑测试"的假绿彻底杀死。

---

## 一、结论（给未来 Phase 的硬规矩）

**绝不 self-signoff "绿"。** 一条 CI 绿，必须同时满足四重判据，缺一不可：

1. **layer1 `Run EditMode Tests` 步骤 `Success`**（不是 layer0 绿就当真）
2. **日志里出现本机 `Unity.exe` 绝对路径**（证明绕开 docker，真机本地 Unity 在跑）
3. **全文件 `continue-on-error` 出现次数 = 0**（没有"失败也继续"的软绿）
4. **`editmode.xml` 硬门实物数字**：`total>0` 且 `failed=0` 且 `passed==total`（Unity 万一软失败 exit 0 没跑测试，XML 缺失/数字不过直接判红）

这四条是从血泪里攒出来的——下面三条"假绿"每一种我们都中过招。

---

## 二、演进链（#11 → #18，root cause 标注）

| Run | commit | 时长 | 表现 | 真死因 / 修复 |
|---|---|---|---|---|
| #11 | `a4e5839` | 26m31s | layer1 `git checkout/clean` `exit 128`；Unity 没跑 | runner 继承**死代理** `127.0.0.1:29290`（端口未监听）→ `github.com:443` 不通、ambiguous HEAD |
| #12 | — | — | （未单独记录，与 #11 同源，同死因） | 同 #11 |
| #13 | `19f97b2` | 1m41s | 层0✅ + Checkout✅，但 `Run EditMode Tests` 被 **skip** | **≠ 代理**：`ci.yml` 含 CJK 注释，PS 5.1 把无 BOM UTF-8 当 **ANSI** 读 → YAML 解析破坏，`Check UNITY_LICENSE secret` 步抛异常 → 后续步骤全 skip |
| #14 | `575b070` | 1m32s | 触发即生效 | license-check 步骤改 **ASCII-only**（去 CJK 注释） |
| #15 | `ab99cff` | 1m57s | 仍卡在 Unity 启动 | `Run EditMode Tests` 改调**本机 `Unity.exe`**（弃 game-ci docker action —— self-hosted 未装 docker → `Unable to locate executable file: docker`） |
| #16 | `bcd27d1` | 1m37s | 绿了但心里没底 | 硬门加固：解析 `editmode.xml`（`total>0`/`failed=0`/`passed==total`）才放行，否则 `exit 5` |
| #17 | `6831f7a` | 3m59s | 双绿，层1 真跑 | 启 Unity 前**剥离继承的死 `HTTPS_PROXY` 等代理 env**（见下，#17 才是真收口） |
| #18 | `8ae960c` | — | **终验绿**：`editmode.xml` 183/183/0/0 | 合并提交（phase8 内容线 `d95ac8d` + CI 硬门线 `1954347`）；183=165 基线+18 Phase 8 新增（埋点/帧率探针）全过 |

> 注：#18 这一绿，不仅复验 Phase 7，还**顺带验证了 Phase 8 工程侧**（Runtime 层可编译 + `AnalyticsTrackerTests` 实跑过）——一份 CI run 收口两个 Phase。

---

## 三、三个真死因（按危害排序）

### 死因 A：`ci.yml` 的 CJK 注释被 PowerShell 5.1 当 ANSI 读，YAML 解析崩（#13）
- **现象**：层0 契约门绿、代码 checkout 绿，但 `Run EditMode Tests` 整段被 skip，run 却显示 Success。
- **根因**：self-hosted runner 用 **PowerShell 5.1** 执行 `run:` 步骤；PS 5.1 把**无 BOM 的 UTF-8** 文件按 **ANSI（系统代码页）** 解析，`ci.yml` 里的 CJK 注释字节错位 → YAML 结构破坏 → 该步骤抛异常、后续步骤被跳过。
- **大白话**：你以为 CI 跑完了，其实它读到一半就"看不懂"你的配置文件，默默跳过了测试，还报成功。
- **修复**：`ci.yml` **ASCII-only**（删 CJK 注释；或给文件加 BOM；或把步骤 shell 改成 `bash`）。我们选了最稳的：全 ASCII。

### 死因 B：死代理 env 被 runner 继承，堵死 git checkout（#11）
- **现象**：layer1 一上来 `git checkout/clean` 就 `exit 128`，报 `github.com:443` 不通 / `ambiguous argument 'HEAD'`。
- **根因**：本机全局挂着代理 `HTTPS_PROXY=http://127.0.0.1:29290`，但那个端口**根本没在监听**。runner 拉代码时走这个死代理 → 连不上 GitHub。
- **大白话**：你电脑里有个"过期的中转站"地址，GitHub 的快递全被它卡死。
- **修复**：移除/绕过该死代理后 checkout 正常。**注意**：这不是 #13 的死因（#13 是 YAML 解析，与代理无关）——这点我们一度误判，见第四节。

### 死因 C：死代理 env 被 runner 继承，破坏 Unity 授权 → exit 0 假绿（#17）
- **现象**：层0/层1 都绿，时长也正常，但**测试其实没跑**——Unity 启动后 `Unity.Licensing.Client` 拿不到 license token，静默退出 `0`，没进测试模式。
- **根因**：即便 checkout 修了代理，**listener 启动 shell 仍继承**了 `HTTP(S)_PROXY` 死代理。Unity 用的是 .NET Core，会读这些代理环境变量去更新 license token → 连死代理失败 → 授权客户端不工作 → Unity `exit 0` 但**一个测试都没执行**。
- **大白话**：这是最阴的一种假绿——它不报错、不红、时长还正常，只是"啥也没干就结束了"。没有硬门的话，永远发现不了。
- **修复**：在 `ci.yml` 启动 Unity 的步骤里，**先 `Remove-Item env:HTTPS_PROXY` 等剥离继承代理 env**，再用 `Start-Process` 拉起本机 `Unity.exe`。配合 #16 的 `editmode.xml` 硬门，XML 缺失立即判红，假绿无所遁形。

---

## 四、认知修正（我们踩过的判断坑）

- **误判 #13 = 代理问题**：最初我根据 #11 的经验，想当然把 #13 也归到代理头上，还给了"清全局代理"的建议。**用户用 GitHub API 拉逐步骤结论纠正**：#13 的 checkout 走 REST API、根本不经 git 代理，是 YAML 解析崩。我已致歉并更正。
- **教训**：**不要拿旧经验套新现象**。每次失败都要拉"步骤级"证据（哪一步 exit code 是什么、日志里到底有没有 `-runTests`），而不是靠"上次也是代理"拍脑袋。
- **"绿"的两种假脸**：
  - **跳过假绿**（#13）：步骤抛异常被 skip，run 仍 Success。
  - **软失败假绿**（#17）：Unity exit 0 但没跑测试，run 仍 Success。
  - 两者都靠**硬门（editmode.xml 实物数字）+ 日志里 `-runTests` 命令行**才能识破。

---

## 五、固化进流程的护栏（给未来 Phase / 其他项目）

| 护栏 | 做法 | 防住什么 |
|---|---|---|
| `ci.yml` ASCII-only | 删 CJK 注释 / 加 BOM / 改 bash shell | 死因 A（PS 5.1 ANSI 解析崩） |
| 弃 docker，直调本机 `Unity.exe` | `Run EditMode Tests` 用 `Start-Process` 拉本地 Unity | game-ci docker action 在 self-hosted 无 docker 时 `Unable to locate executable file: docker` |
| 启 Unity 前剥离代理 env | 步骤开头 `Remove-Item env:HTTPS_PROXY/HTTP_PROXY/ALL_PROXY` | 死因 C（授权客户端被死代理坑 → exit 0 假绿） |
| `editmode.xml` 硬门 + `exit 5` | 解析 XML，`total≤0`/`failed>0`/`passed<total` 任一即红 | 所有"exit 0 但没跑测试"的假绿 |
| 四重判据才签收 | 见第一节 | self-signoff 假绿 |

---

## 六、流程改进建议（可落地下一 Phase）

1. **提交前 lint `ci.yml`**：加一个本地/PR 检查，拒绝含 CJK 非 ASCII 字节或 `continue-on-error` 的 workflow 文件。把"人肉踩坑"变"机器拦门"。
2. **硬门脚本本地可复跑**：把 `editmode.xml` 解析+判门抽成独立脚本，开发者本机跑 Unity 后立刻自查，不必等 CI。
3. **文档化 self-hosted runner 注册 + 代理剥离配方**：`phase7-ci-selfhosted.md` 已立，建议作为团队模板保留；新机器接入直接照抄，避免再次踩死代理。
4. **统一"绿"的口径**：任何"CI 绿了"的汇报，必须附带四重判据证据（尤其 `editmode.xml` 数字），否则一律视为"未验证"。

---

## 七、Phase 8 顺带收获

- run #18 的 `183/183` 中，**18 个测试是 Phase 8 新增**（`AnalyticsTrackerTests` 数据埋点单测 + 帧率探针相关）。这一绿同时证明：
  - Runtime 层（`UnityAnalyticsSink` / `DeviceFpsProbe` / `GameBootstrap` 接线）**可编译**；
  - 埋点逻辑**实跑且过**。
- 即 Phase 7 的硬门，在 Phase 8 合并后**自动成了 Phase 8 的工程验证网**，无需额外 CI run。

---

## 八、开放项（不阻塞本次收口，发布前须闭合）

| # | 项 | 归属 | 备注 |
|---|----|------|------|
| A1 | 真机降级帧率 ≥30fps（低内存机 Profiler 实测） | 程基岩 + 真机 | 桌面端已绿；见 `release/phase8-release-checklist.md` |
| A2 | Canvas 真机 UI（首启四步引导真机渲染） | 程基岩 / 林绘澄 | `OnboardingCanvasView` 本机补全 |
| A3–A4 | 美术 / 音频终版 | 林绘澄 / 阮和声 | 命名对齐 `item_id`/`fragment_id`/`ending_id` |

---

**签署**：Phase 7 复盘完成，四重判据护栏已固化；Phase 8 工程侧经 run #18 同步验证。剩余 A1–A4 为真机/美术/音频本机项。
