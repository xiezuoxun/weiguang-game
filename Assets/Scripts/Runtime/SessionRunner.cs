// SessionRunner.cs + Stubs — Sprint 1 空会话骨架（S1/S2/S3/S5 stub 占位，Sprint 2 起逐 EPIC 替换真实实现）。
// 编排归 SessionRunner（S4 语义）；stub 只订阅事件/被调用，不互相直连（ADR-005）。
// 每个 phase 推进后调 SaveEngine 落 SaveNode 快照（S6 ②）。
using System;
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime
{
    public class SessionRunner
    {
        readonly EventBus _bus;
        readonly CommissionStateMachine _fsm;
        readonly SaveEngine _save;
        readonly Func<SaveSnapshot> _snap;
        readonly RuntimeQuality _quality;

        public SessionRunner(EventBus bus, CommissionStateMachine fsm, SaveEngine save, Func<SaveSnapshot> snap, RuntimeQuality quality = null)
        {
            _bus = bus; _fsm = fsm; _save = save; _snap = snap;
            _quality = quality ?? new RuntimeQuality(); // 打磨：降级配置（默认全开）
        }

        /// <summary>打磨：当前降级档位下的拂尘网格采样上限（供美术 Shader / 后续真机手势层读取）。
        /// 逻辑层仍按 CSV 全格揭示（保证 reveal_pct 到达阈值），此值仅约束表现层分辨率。</summary>
        public int EffectiveDustCellCap => Math.Min(_quality.maxDustCells, 64);

        /// <summary>打磨（Phase 7-C1）：把 CSV 拂尘网格 (csvW,csvH) 按当前降级档位封顶，
        /// 返回**表现层**采样分辨率 (resW,resH)。总格 ≤ EffectiveDustCellCap，保长宽比。
        /// 逻辑层 reveal 仍按 CSV 全格驱动（reveal_pct 阈值不受影响），此值仅供美术 Shader 降采样密度。</summary>
        public (int resW, int resH) VisualDustResolution(int csvW, int csvH)
            => DustBudget.CapGrid(EffectiveDustCellCap, csvW, csvH);

        public void WireStubs()
        {
            _bus.Subscribe(GameEvents.EVT_REVEAL_COMPLETE, p => OnRevealComplete());
            _bus.Subscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, p => OnAssembleComplete());
            _bus.Subscribe(GameEvents.EVT_CHOICE_MADE, p => OnChoiceMade((EndingTag)p));
        }

        /// <summary>从断点 phase 继续驱动（恢复语义：不跳过已到 phase，S6 ⑥-1）。</summary>
        public void DriveFrom(CommissionPhase phase)
        {
            var s = _snap();
            var c = s.active_commission;
            if (c == null) return;
            switch (phase)
            {
                case CommissionPhase.Received: Step(c, CommissionPhase.Examining); break;
                case CommissionPhase.Examining: StartReveal(c); break;
                case CommissionPhase.Revealing: StartReveal(c); break; // 恢复：重演拂尘（reveal_states 还原后继续）
                case CommissionPhase.Assembling: StartAssemble(c); break;
                case CommissionPhase.Choosing: StartChoose(c); break;
                case CommissionPhase.Delivering: Deliver(c, EndingTag.Omit); break; // 极端恢复兜底
                default: break;
            }
        }

        void Step(Commission c, CommissionPhase to)
        {
            if (!_fsm.AdvancePhase(c, to)) return;
            _save.Save(_snap()); // NODE_* 落盘（节流内自动合并）
            switch (to)
            {
                case CommissionPhase.Examining: StartReveal(c); break;
                case CommissionPhase.Revealing: StartReveal(c); break;
                case CommissionPhase.Assembling: StartAssemble(c); break;
                case CommissionPhase.Choosing: StartChoose(c); break;
                case CommissionPhase.Delivering: Deliver(c, _lastTag); break;
                case CommissionPhase.Archived: Archive(c); break;
            }
        }

        // ── S1 拂尘显影真实化（路径 C：纯 C# Core 层，不依赖 Unity 渲染）──
        // 替换 Sprint 1 stub：逐格 reveal 驱动 RevealThresholdTracker，经 EventBus 发浮纸签；
        // 完成后发 EVT_REVEAL_COMPLETE，由既有 OnRevealComplete 转出态（ASSEMBLING/CHOOSING）。
        void StartReveal(Commission c)
        {
            var s = _snap();
            var item = s.items.Find(i => i.item_id == c.item_id);
            if (item?.dust_grid == null) { Debug.LogWarning($"[S1] 物件/拂尘网格缺失 {c.item_id}"); return; }

            // 本次拂尘的推进量承载于 RevealState（契约已含 reveal_pct 字段）；
            // 注：reveal_pct 当前不在 MemoryItem 上，故本地持有，零侵入现有 DataContract。
            var tracker = new RevealThresholdTracker();
            var grid = item.dust_grid;

            // 确定性模拟驱动：按网格扫描顺序逐格 reveal（真实手势输入在 Unity 层后续接）。
            // 每拂一格推进 reveal_pct，并驱动阈值回调发浮纸签；同时发体验层"阈值跨越"事件供顿挫脉冲。
            int total = grid.TotalCells();
            for (int y = 0; y < grid.height; y++)
            {
                for (int x = 0; x < grid.width; x++)
                {
                    grid.RevealCell(x, y);
                    float pct = grid.RevealPct();
                    tracker.Update(pct, t => PublishWhisper(t, pct),
                        crossed => _bus.Publish(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED, crossed));
                }
            }

            float finalPct = grid.RevealPct();
            Debug.Log($"[S1] 拂尘完成 reveal_pct={finalPct:F2}（threshold={c.reveal_threshold}，格数={total}）");
            if (finalPct >= c.reveal_threshold) _bus.Publish(GameEvents.EVT_REVEAL_COMPLETE, finalPct);
        }

        /// <summary>S1-3 阈值回调：经 EventBus 广播浮纸签事件（文案允许为空，设计侧后续填）。</summary>
        void PublishWhisper(float threshold, float pct)
        {
            string key = RevealThresholdTracker.KeyOf(threshold);
            if (key == null) return;
            _bus.Publish(GameEvents.EVT_REVEAL_WHISPER, new RevealWhisperEvent(key, pct));
        }

        void OnRevealComplete()
        {
            var c = _snap().active_commission;
            Step(c, c.fragment_count == 0 ? CommissionPhase.Choosing : CommissionPhase.Assembling);
        }

        // ── S2 拼合真实化（路径 C：纯 C# Core 层，不依赖 Unity 渲染）──
        // S2-1 槽位模型 / S2-2 碎片实例化 / S2-3 拼合完成判定。
        // 替换 Sprint 1 stub：按 fragment_count 实例化碎片与归属槽位，归属带（中带 Y∈[0.33,0.67] +
        // X 接近锚点 |posX-anchor_x|≤0.15）命中才锁定；全锁经 EventBus 发 EVT_ASSEMBLE_COMPLETE。
        // 确定性模拟驱动（真实手势拖拽在 Unity 层后续接），与 S1 的确定性拂尘同构。
        void StartAssemble(Commission c)
        {
            var s = _snap();
            var board = new AssemblyBoard();

            // ── S2-2 实例化碎片 + 初始化归属槽位 ──────────────────────
            for (int i = 0; i < c.fragment_count; i++)
            {
                string fid = $"{c.item_id}_f{i}";
                string sid = $"slot{i}";

                // 槽位锚点：中带均匀均分 X（fragment_count>1 间距均分，单片落 0.5）
                float anchorX = c.fragment_count <= 1 ? 0.5f : (0.5f + ((i - (c.fragment_count - 1) / 2f)) / c.fragment_count);
                float anchorY = 0.5f; // 中带中心（[0.33,0.67] 由 FragmentSlot 常量约束语义）
                board.slots.Add(new FragmentSlot(sid, anchorX, anchorY, fid));

                // 碎片：兼容既有 fragment_states 缺失则新建；初始散落于下带（Y∈[0.67,0.96]）确定性点
                var f = s.fragment_states.Find(x => x.fragment_id == fid);
                if (f == null)
                {
                    f = new Fragment { fragment_id = fid, item_id = c.item_id, home_slot_id = sid, rotation = 0f };
                    s.fragment_states.Add(f);
                }
                f.home_slot_id = sid;
                f.is_locked = false;
                f.current_pos_x = anchorX;                       // 散落点 X 与槽位对齐（确定性）
                f.current_pos_y = 0.67f + 0.29f * ((float)(i + 1) / c.fragment_count); // 下带 [0.67,0.96)
                board.fragments.Add(f);
            }

            // ── S2-1/S2-3 确定性模拟驱动：每片落到其 home_slot 锚点（命中归属带）→ 锁定，逐步判全锁 ──
            for (int i = 0; i < board.fragments.Count; i++)
            {
                var f = board.fragments[i];
                var slot = board.slots[i];
                board.TryPlaceFragment(f, slot.anchor_x, slot.anchor_y); // 落到锚点必命中归属带
                if (board.AllLocked())
                {
                    Debug.Log($"[S2] 拼合完成（{board.fragments.Count} 片全锁）");
                    _bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(board.fragments.Count, board.fragments.Count));
                    return; // 全锁即止，不重复发事件
                }
            }
            // 未全锁：保持 Assembling 态，不自动转 Choosing（由玩家继续拼合触发后续 place）。
            Debug.Log($"[S2] 拼合进行中（{board.fragments.Count} 片未全锁）");
        }

        void OnAssembleComplete() => Step(_snap().active_commission, CommissionPhase.Choosing);

        // ── S3 抉择分支叙事真实化（路径 C：纯 C# Core 层，不依赖 Unity 渲染）──
        // S3-1 抉择点加载：从 SaveSession.choice_states 取该委托的 ChoiceNode；
        //   缺失则按兼容逻辑新建占位（wording 用合规占位且长度≤26，安全进包）。
        //   不自动选——等待玩家点选（真实输入在 Unity 层后续接，本 PR 用 SelectOption 确定性驱动）。
        // S3-2 点选落定：SelectOption 写玩家所选的 selected_option_id 与 _lastTag；
        //   非法 optionId 抛 ContractViolationException（fail-fast）。
        //   注：StartChoose 内仍按设计默认走"选第一个"，但路径走真实 SelectOption 而非直接赋值。
        public void StartChoose(Commission c)
        {
            var s = _snap();
            var node = s.choice_states.Find(n => n.commission_id == c.commission_id);
            if (node == null)
            {
                node = new ChoiceNode { node_id = $"cn_{c.commission_id}", commission_id = c.commission_id };
                // 兼容占位：wording 用合规占位文案（"待填措辞"4 字 < 26 上限，安全进包，不触发 CR-002 T3 ② 越界）
                for (int i = 0; i < c.choice_count; i++)
                    node.options.Add(new ChoiceOption
                    {
                        option_id = $"op{i}", wording = "（待填措辞）",
                        truth_level = 0.5f * i, ending_tag = (EndingTag)(i % 3), client_reaction = "…", sdt_autonomy_weight = 0.5f
                    });
                s.choice_states.Add(node);
            }
            // 不自动选：selected_option_id 保持 null，等待玩家点选（真实输入在 Unity 层后续接）。
            // 设计默认（确定性驱动）：按真实 SelectOption 路径落定第一个 option，而非直接赋值。
            if (string.IsNullOrEmpty(node.selected_option_id))
                SelectOption(c, node.options[0].option_id);
        }

        /// <summary>S3-2 点选落定：校验 optionId 存在于 node.options，写 selected_option_id 与 _lastTag，
        /// 并经 EventBus 广播 EVT_CHOICE_MADE（ADR-005：抉择结果经事件通讯）。</summary>
        /// <exception cref="ContractViolationException">optionId 不存在于该抉择点时抛出（fail-fast）。</exception>
        public void SelectOption(Commission c, string optionId)
        {
            var s = _snap();
            var node = s.choice_states.Find(n => n.commission_id == c.commission_id);
            if (node == null)
                throw new ContractViolationException(new[] { $"抉择点缺失：commission_id={c.commission_id}" });

            var opt = node.options.Find(o => o.option_id == optionId);
            if (opt == null)
                throw new ContractViolationException(new[]
                {
                    $"非法 optionId：{optionId}（commission_id={c.commission_id}，可选 {node.options.Count} 项）"
                });

            node.selected_option_id = opt.option_id;   // 玩家所选（非 stub 的"写第一个"）
            _lastTag = opt.ending_tag;                  // 单层 R3 语义：一个选项对应一个 ending_tag
            Debug.Log($"[S3] 抉择落定 selected={node.selected_option_id} tag={_lastTag}（单层，R3）");
            // 体验层钩子：发"选中"事件（供纸签选中高亮 Shader 触发点），与 EVT_CHOICE_MADE（tag 语义）分离。
            _bus.Publish(GameEvents.EVT_OPTION_SELECTED, new ChoiceOptionEvent(opt.option_id, ChoiceOptionEvent.TYPE_SELECTED));
            _bus.Publish(GameEvents.EVT_CHOICE_MADE, _lastTag); // 经 EventBus → OnChoiceMade → Delivering
        }

        /// <summary>S3 体验层钩子：玩家手指悬停/聚焦某选项时由上层（Unity 层触控）调用，
        /// 经 EventBus 广播 EVT_OPTION_HIGHLIGHTED（供纸签高亮 Shader 触发点）。
        /// 仅做事件广播，不改动 selected_option_id / _lastTag（选中落定仍走 SelectOption）。
        /// 非法 optionId 不抛异常——悬停是瞬态反馈，静默忽略比 fail-fast 更友好。</summary>
        public void HighlightOption(Commission c, string optionId)
        {
            var s = _snap();
            var node = s.choice_states.Find(n => n.commission_id == c.commission_id);
            if (node == null) return;
            if (node.options.Find(o => o.option_id == optionId) == null) return; // 悬停瞬态：非法 id 静默忽略
            _bus.Publish(GameEvents.EVT_OPTION_HIGHLIGHTED, new ChoiceOptionEvent(optionId, ChoiceOptionEvent.TYPE_HIGHLIGHTED));
        }

        EndingTag _lastTag = EndingTag.Omit;
        // S5-1：Ending 实体承载（SaveSnapshot 当前无 endings 列表字段，按任务约束零侵入 S6 契约，
        // 用私有字段持有最近一次交付构建的 Ending；设计后续若扩 DataContract 可直接嫁接到列表）。
        Ending _lastEnding;

        /// <summary>最近一次 Deliver 构建的 Ending（只读，供测试/上层查询归档结局文案映射）。</summary>
        public Ending LastEnding => _lastEnding;

        void OnChoiceMade(EndingTag tag) => Step(_snap().active_commission, CommissionPhase.Delivering);

        // ── 交付与归档（S4 语义 / S5 真实化）──────────────────────────
        // S5-1 Ending 实体构建：不再只存裸 _lastTag，构建 Ending 并持有。
        void Deliver(Commission c, EndingTag tag)
        {
            _lastTag = tag;
            _lastEnding = new Ending
            {
                ending_id = $"ed_{c.commission_id}",
                commission_id = c.commission_id,
                title = EndingTitleOf(tag),
                description = "（待填交付反馈）", // 合规占位，design 后续覆盖
                emotion_arc_stage = EmotionArcStageOf(tag)
            };
            Debug.Log($"[S5] 交付：{_lastEnding.title}（{tag} / {_lastEnding.emotion_arc_stage}）");
            Step(c, CommissionPhase.Archived); // 经状态机 Delivering→Archived 后自动调 Archive
        }

        /// <summary>S5-1 标题按 tag 给默认文案（允许后续 design 覆盖）。</summary>
        static string EndingTitleOf(EndingTag tag) => tag switch
        {
            EndingTag.Truth => "微光重燃",
            EndingTag.Omit => "尘封未启",
            EndingTag.Reframe => "另寻归处",
            _ => "尘封未启"
        };

        /// <summary>S5-1 情绪弧阶段按 tag 映射。</summary>
        static string EmotionArcStageOf(EndingTag tag) => tag switch
        {
            EndingTag.Truth => "释然",
            EndingTag.Omit => "怅惘",
            EndingTag.Reframe => "和解",
            _ => "怅惘"
        };

        // S5-2 CodexEntry 聚合 + S5-3 归档幂等 + 终态守卫。
        // 公共入口（测试可直接驱动）；内部校验 phase 合法性与幂等，fail-fast 防止乱序归档。
        public void Archive(Commission c)
        {
            var s = _snap();
            string entryId = $"cx_{c.commission_id}";
            // S5-3 幂等（权威判据：数据层 entry_id 去重）：同 entry_id 已存在则不重复 Add（覆盖恢复重演/二次调用）。
            // 注：Archive 总由 Step 在 phase 已转 Archived 后调用，故 phase==Archived 是正常收口态而非"跳过"信号；
            // 此处幂等以 codex 是否已含 entry_id 为准，避免正常收口被误跳过。
            if (s.codex.Exists(e => e.entry_id == entryId))
            {
                Debug.Log($"[S5] 归档幂等跳过：{entryId}（entry_id 已存在）");
                return;
            }
            // S5-3 终态守卫：仅允许 Delivering（测试直驱合法前置）或 Archived（Step 收口正常态）进入；
            // 其余 phase（Choosing/Assembling/…）即乱序归档，fail-fast 防破坏核心循环时序。
            if (c.phase != CommissionPhase.Delivering && c.phase != CommissionPhase.Archived)
                throw new ContractViolationException(new[]
                {
                    $"非法归档：commission_id={c.commission_id} phase={c.phase}，期望 Delivering/Archived（其余 phase 视为乱序）"
                });

            // 若仍处 Delivering（直接调用 Archive 而非经 Step 收口），先经状态机迁移到 Archived。
            if (c.phase == CommissionPhase.Delivering)
                _fsm.AdvancePhase(c, CommissionPhase.Archived);

            // S5-2 聚合 CodexEntry：client/item/commission 来自 c，ending_tag 来自 _lastTag，
            // 客户详情经 SaveSnapshot.clients 按 client_id 查（无则留默认，不依赖客户端表注入）。
            var cl = s.clients.Find(x => x.client_id == c.client_id);
            var entry = new CodexEntry
            {
                entry_id = entryId,
                commission_id = c.commission_id,
                item_id = c.item_id,
                client_id = c.client_id,
                lit_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ending_tag = _lastTag,
                timeline_order = s.codex.Count, // 递增序号
                is_mainplot = c.is_mainplot,
                quote = "（待填引语）" // 占位合规字符串，design 后续填，工程不依赖内容
            };
            s.codex.Add(entry);

            // 客户关系推进（沿用既有语义：visit++、relationship 封顶 5、主线 progress++）。
            if (cl != null)
            {
                cl.visit_count++;
                if (cl.relationship_level < 5) cl.relationship_level++;
                if (c.is_mainplot) cl.mainplot_progress++;
            }

            _save.Save(s, force: true); // ARCHIVED 强制写（会话收口）
            // S5-2 经 EventBus 广播归档事件（ADR-005：图鉴表现经事件通信）。
            _bus.Publish(GameEvents.EVT_ARCHIVED, new ArchivedEvent(entry.entry_id, entry.timeline_order, entry.is_mainplot));
            // 体验层钩子：发"图鉴解锁"独立事件（供解锁动画触发点），与归档收束表现解耦。
            _bus.Publish(GameEvents.EVT_CODEX_UNLOCKED, new CodexUnlockedEvent(entry.entry_id));
            Debug.Log($"[S5] 归档 CodexEntry={entry.entry_id}｜图鉴共 {s.codex.Count} 条");
        }
    }
}
