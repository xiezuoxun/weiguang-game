

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-09-02 18:36:42] CI 环境状态（2026-09-02 更新）：本机已装 Unity 2022.3.20f1（C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe）+ 个人版 license。本机 EditMode 165/165 绿、PlayMode 4/4 绿（ArtAcceptanceTests A1-A6 全过）。CI 已改 self-hosted runner（commit 437a1e6），CI #9 Success；GitHub 仓库 xiezuoxun/weiguang-game。Unity batchmode 后台跑测试时包装任务会提前返回，需另起等待 Unity 进程退出再读 XML。
- [2026-09-02 18:36:44] 本机 git 全局代理指向 127.0.0.1:29290 但该代理常不在线，push/pull 直连 github.com:443 可达——推送用 `git -c http.proxy= -c https.proxy= push origin master` 绕过（勿改全局配置）。Packages/manifest.json 本地改动引用 file:../.codely-cli/ 扩展包，属 Codely 本地工具状态，禁止提交（CI checkout 无此路径会炸）。

### Reference

