// GameBootstrap.cs — 空委托会话骨架驱动（Sprint 1 集成验证）。
// 组装：EventBus + SaveEngine + 状态机 + S1/S2/S3/S5 stub。
// 行为：自动把一条 Commission 从 RECEIVED 推到 ARCHIVED（stub 模拟玩家），
//       每个 phase 推进时落盘 SaveNode；支持模拟切后台（SimulateOnPause）与恢复。
// 真实系统（EPIC-03~06）落地后逐 stub 替换，本文件编排逻辑不变（ADR-005）。
using System;
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core;

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
            _bus.Subscribe(GameEvents.EVT_SAVE_FAILED, p => Debug.LogWarning($"[S6] 存档失败：{p}"));

            LoadData();
            _snapshot = _save.LoadLatest() ?? NewSnapshot();
            if (_snapshot.active_commission != null)
                Debug.Log($"[S6] 恢复存档：{_snapshot.active_commission.commission_id} @ {_snapshot.active_commission.phase}（node={_snapshot.last_node}）");

            _runner = new SessionRunner(_bus, _fsm, _save, () => _snapshot);
            _runner.WireStubs();
            if (_snapshot.active_commission == null || _snapshot.active_commission.phase == CommissionPhase.Archived)
                StartNextCommission();
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
                    material = (Material)Enum.Parse(typeof(Material), r["material"], true),
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
