// GameBootstrap.cs — 空委托会话骨架驱动（Sprint 1 集成验证）。
// 组装：EventBus + SaveEngine + 状态机 + S1/S2/S3/S5 stub。
// 行为：自动把一条 Commission 从 RECEIVED 推到 ARCHIVED（stub 模拟玩家），
//       每个 phase 推进时落盘 SaveNode；支持模拟切后台（SimulateOnPause）与恢复。
// 真实系统（EPIC-03~06）落地后逐 stub 替换，本文件编排逻辑不变（ADR-005）。
using System;
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core;
using Weiguang.Core.Analytics;
using Weiguang.Runtime.ArtBinding;
using Weiguang.Runtime.Analytics;

namespace Weiguang.Runtime
{
    public class GameBootstrap : MonoBehaviour
    {
        public TextAsset commissionsCsv;
        public TextAsset clientsCsv;
        public TextAsset itemsCsv;

        EventBus _bus;
        SaveEngine _save;
        CommissionStateMachine _fsm;
        SessionRunner _runner;
        List<Commission> _commissions;
        Dictionary<string, Client> _clients;
        Dictionary<string, MemoryItem> _items;
        SaveSnapshot _snapshot;

        // Phase 8-A/B：埋点与帧率探针（纯 C# 核心 + 设备 sink，生命周期随 GameBootstrap）
        AnalyticsTracker _analytics;
        IAnalyticsSink _analyticsSink;
        DeviceFpsProbe _fpsProbe;

        /// <summary>打磨：运行时画质/降级配置，Awake 按设备档位初始化，供 SessionRunner / 美术 Shader 读取。</summary>
        public RuntimeQuality quality = new RuntimeQuality();

        /// <summary>Phase 8-B：是否启用真机帧率探针。默认关（沙箱/Editor 不跑）；设备包经 #if 自动开启，
        /// 也可在 Inspector 手动勾选后再出包（见 production/phase8-device-fps.md）。</summary>
        public bool enableDeviceFpsProbe = false;

        /// <summary>打磨：存档失败的用户侧回调（Unity UI 可挂"进度未保存"提示）。
        /// 与 EVT_SAVE_FAILED 日志并存——日志供工程排查，此回调供玩家可见反馈。</summary>
        public System.Action<string> OnSaveFailed;

        void Awake()
        {
            _bus = new EventBus { LogError = m => Debug.LogError(m) };
            var storage = new UnitySaveStorage();
            string dir = Path0();
            _save = new SaveEngine(storage, dir, _bus)
            {
                Serialize = s => JsonUtility.ToJson(s),
                Deserialize = j => JsonUtility.FromJson<SaveSnapshot>(j)
            };
            _save.RegisterMigration(0, s => { s.version = 1; return s; });
            _fsm = new CommissionStateMachine(_bus);
            _fsm.OnWarn += m => Debug.LogWarning($"[S4] {m}");
            _bus.Subscribe(GameEvents.EVT_SAVE_FAILED, p =>
            {
                var msg = p as string ?? "未知存档错误";
                Debug.LogWarning($"[S6] 存档失败：{msg}");
                OnSaveFailed?.Invoke(msg); // 打磨：玩家可见反馈钩子
            });

            // 打磨：按设备档位初始化降级配置（移动端/低内存机自动降级）
            quality = RuntimeQuality.ForDevice(Application.isMobilePlatform, SystemInfo.systemMemorySize);

            WireAnalytics(); // Phase 8-A/B：埋点 + 真机帧率探针（须在首个 Publish 之前接线）

            LoadData();
            _snapshot = _save.LoadLatest();
            bool firstLaunch = _snapshot == null;
            if (firstLaunch)
            {
                _snapshot = NewSnapshot();
                // 打磨：首启动广播引导事件（供 UI 弹首见引导），携带四动词引导文案
                _bus.Publish(GameEvents.EVT_FIRST_LAUNCH, new FirstLaunchEvent(
                    OnboardingHints.Of("reveal"), OnboardingHints.Of("assemble"),
                    OnboardingHints.Of("choose"), OnboardingHints.Of("archive")));
                Debug.Log("[S6] 首次启动：广播 EVT_FIRST_LAUNCH");
            }
            else if (_snapshot.active_commission != null)
                Debug.Log($"[S6] 恢复存档：{_snapshot.active_commission.commission_id} @ {_snapshot.active_commission.phase}（node={_snapshot.last_node}）");

            _runner = new SessionRunner(_bus, _fsm, _save, () => _snapshot, quality);
            _runner.WireStubs();
            if (_snapshot.active_commission == null || _snapshot.active_commission.phase == CommissionPhase.Archived)
                StartNextCommission();

            BindArtBridges();
        }

        /// <summary>6-B 骨架：将场景内所有 ArtBridgeBase 绑定到本实例的 EventBus（art-director 接口约定）。
        /// 桥为 MonoBehaviour，挂在特定 GameObject 上；此处统一注入，避免静态单例（ADR-005）。
        /// 打磨（Phase 7-C1）：一并注入运行时降级配置 quality，供需读取档位的桥（如 RevealVisualBridge）使用。</summary>
        void BindArtBridges()
        {
            var bridges = FindObjectsOfType<ArtBridgeBase>();
            foreach (var b in bridges) b.Bind(_bus, quality);
            if (bridges.Length > 0) Debug.Log($"[6-B] 已绑定 {bridges.Length} 个 ArtBridge（quality.maxDustCells={quality.maxDustCells}）");
        }

        // ── Phase 8-A/B：埋点 + 真机帧率探针接线 ──────────────────
        /// <summary>埋点接线。默认用 LogAnalyticsSink（Editor/CI 可读可测）；设备包（Android/iOS）换 UnityAnalyticsSink
        /// 转发到 Unity Analytics。订阅须在 Awake 内、首个 Publish（EVT_FIRST_LAUNCH / EVT_COMMISSION_START 均在
        /// Awake 中发出）之前完成，故此处于 bus + quality 就绪后接线，而非 OnEnable，否则会漏掉首启动与首委托事件。</summary>
        void WireAnalytics()
        {
            _analyticsSink = new LogAnalyticsSink(); // 默认：日志出口
#if UNITY_ANDROID || UNITY_IOS
            // 设备上：改用 Unity Analytics 出口（MonoBehaviour，挂到本 GameObject）
            var ua = gameObject.AddComponent<UnityAnalyticsSink>();
            _analyticsSink = ua;
#endif
            _analytics = new AnalyticsTracker(_bus, _analyticsSink);
            _analytics.Subscribe();

            // 真机帧率探针：仅设备包默认启用（沙箱/Editor 不跑，避免无 GPU 下刷屏日志）。
            bool runFpsProbe = enableDeviceFpsProbe;
#if UNITY_ANDROID || UNITY_IOS
            runFpsProbe = true;
#endif
            if (runFpsProbe)
            {
                _fpsProbe = gameObject.AddComponent<DeviceFpsProbe>();
                _fpsProbe.sink = _analyticsSink;
                _fpsProbe.quality = quality;
                _fpsProbe.StartSampling();
            }
        }

        /// <summary>生命周期清理：精准退订埋点 handler，并补报最后一次 FPS 窗口。</summary>
        void OnDestroy()
        {
            _analytics?.Unsubscribe();
            if (_fpsProbe != null) _fpsProbe.StopSampling();
        }

        string Path0() => System.IO.Path.Combine(Application.persistentDataPath, "saves");

        void LoadData()
        {
            _commissions = new List<Commission>();
            _clients = new Dictionary<string, Client>();
            _items = new Dictionary<string, MemoryItem>();

            foreach (var r in SimpleCsv.Parse(commissionsCsv.text))
            {
                var c = new Commission
                {
                    commission_id = r["commission_id"], client_id = r["client_id"], item_id = r["item_id"],
                    chapter_index = int.Parse(r["chapter_index"]), phase = CommissionPhase.Idle,
                    session_soft_budget_min = float.Parse(r["session_soft_budget_min"]), is_daily = bool.Parse(r["is_daily"]),
                    reveal_threshold = float.Parse(r["reveal_threshold"]), fragment_count = int.Parse(r["fragment_count"]),
                    choice_count = int.Parse(r["choice_count"]), ending_variants = int.Parse(r["ending_variants"]),
                    is_mainplot = bool.Parse(r["is_mainplot"])
                };
                foreach (var e in ContractGuard.Validate(c)) Debug.LogWarning($"[契约] {e}");
                _commissions.Add(c);
            }
            foreach (var r in SimpleCsv.Parse(clientsCsv.text))
                _clients[r["client_id"]] = new Client
                {
                    client_id = r["client_id"], display_name = r["display_name"],
                    relationship_level = int.Parse(r["relationship_level"]), visit_count = int.Parse(r["visit_count"]),
                    mainplot_progress = int.Parse(r["mainplot_progress"])
                };
            foreach (var r in SimpleCsv.Parse(itemsCsv.text))
            {
                var grid = new DustGrid { width = int.Parse(r["grid_w"]), height = int.Parse(r["grid_h"]), revealed = new bool[int.Parse(r["grid_w"]) * int.Parse(r["grid_h"])] };
                _items[r["item_id"]] = new MemoryItem
                {
                    item_id = r["item_id"], display_name = r["display_name"],
                    item_type = (ItemType)Enum.Parse(typeof(ItemType), r["item_type"], true),
                    material = (Weiguang.Core.Material)Enum.Parse(typeof(Weiguang.Core.Material), r["material"], true),
                    client_id = r["client_id"], dust_grid = grid, detail_unlocked = false,
                    is_mainplot = bool.Parse(r["is_mainplot"])
                };
            }
            _snapshot0();
        }

        void _snapshot0() { } // 占位（保留编号命名一致性）

        SaveSnapshot NewSnapshot()
        {
            var s = new SaveSnapshot { version = SaveEngine.SAVE_VERSION };
            foreach (var cl in _clients.Values) s.clients.Add(cl);
            foreach (var it in _items.Values) s.items.Add(it);
            return s;
        }

        void StartNextCommission()
        {
            var next = _commissions.Find(c => c.phase == CommissionPhase.Idle);
            if (next == null) { Debug.Log("[S4] 全部委托完成"); return; }
            next.phase = CommissionPhase.Idle;
            _snapshot.active_commission = next;
            _fsm.AdvancePhase(next, CommissionPhase.Received);
            _bus.Publish(GameEvents.EVT_COMMISSION_START, next);
            _runner.DriveFrom(next.phase);
        }

        // Unity 生命周期：切后台/锁屏强制写（S6 ②，<500ms 不节流）
        void OnApplicationPause(bool paused)
        {
            if (paused) _save.Save(_snapshot, force: true);
        }
        void OnApplicationQuit() => _save.Save(_snapshot, force: true);

        // 编辑器手动验证入口：模拟切后台再回前台
        [ContextMenu("模拟切后台")]
        public void SimulateOnPause() => OnApplicationPause(true);
    }
}
