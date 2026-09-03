# Phase 8 内容扩充 · 交付文档

- **游戏**：《微光归处》（移动端叙事 / 冒险）
- **阶段**：Phase 8 内容扩充
- **仓库根**：`C:/Users/lenovo/WorkBuddy/2026-08-23-19-32-18/game`
- **数据目录**：`Assets/Data/`
- **契约校验器**：`tools/validate_contract.py`
- **总 CSV 数**：8（既有）+ 12（新增）= **20 张**

---

## 一、契约安全策略（为什么不会破坏现有校验）

通读 `tools/validate_contract.py` 后确认其校验范围如下，本批新增内容据此设计：

1. **校验器只固定读取 6 张表**：
   - `REQUIRED_COLS` 强制列体检 + 逐行边界/布尔/枚举校验：`commissions.csv`、`clients.csv`、`items.csv`；
   - R4 碎片归属交叉校验：读 `fragments.csv` 并比对 `commissions.csv`；
   - R5 结局↔抉择一致性：读 `endings.csv` 与 `choices.csv`；
   - R3 单层锁死：仅检查 `choices.csv` 是否出现二级分支列。
   - **对 `whispers.csv` / `quotes.csv` 仅做（warn 级、不阻断的）BOM 探测，不做任何行/列校验。**
2. **校验器不对目录做 glob / 通配**：任何不在上述硬编码清单内的 CSV 文件（包括本批 12 张新表）**完全不被读取、不被校验**。因此新增表采用独立、非冲突文件名（不与 8 张既有表重名），其数据不要求满足任何契约边界，天然不会触发 FAIL。
3. **既有 8 表原样不动**：未删行、未改必需列、未改动任何会越界或丢失主线的值。
   - 委托仍为 `com_001..com_005`（5 条，落在 MVP 量 `[3,5]` 内）；
   - 主线 `com_003`（is_mainplot=true）保持不变；
   - `fragment_count` 与 `fragments.csv` 实有碎片数（watch=1/photo=2/letter=2/ornament=4/mirror=4，合计 13）仍严格一致（R4 通过）；
   - `choices.csv` 的 `(commission_id, ending_tag)` 与 `endings.csv` 仍一一对应（R5 通过）。
4. **编码**：所有新增 CSV 以 **UTF-8 无 BOM** 写入；已逐文件核验首 3 字节无 `EF BB BF`（见 §四）。

> 若未来要把新表纳入契约门，只需在 `validate_contract.py` 的 `REQUIRED_COLS` / R4 / R5 中**独立追加**新表名与规则即可，不影响既有 8 张表的校验逻辑。

---

## 二、新增 12 张表清单

> 命名均避开已校验表（`commissions/clients/items/fragments/choices/endings/whispers/quotes`），避免与校验器硬编码清单冲突。
> “预期 Runtime 消费者”为本批内容接入运行时各模块的建议落点；当前校验器不消费这些表。

### 1. `item_variants.csv` — 物件外观 / 状态变体
- **列**：`variant_id, item_id, variant_name, material_override, condition, unlock_condition, note`
- **说明**：现有 5 件物件的剧情态 / 外观变体（原样 / 微修 / 清洁 / 收纳），供渲染层按 `unlock_condition` 切换。
- **自洽**：`item_id` 全部引用现有 `it_watch/it_photo/it_letter/it_ornament/it_mirror`；`condition` 取值受控。
- **预期消费者**：`ItemView` / `DustGrid` 渲染器（按剧情进度选择 variant）。

### 2. `client_variants.csv` — 客户情绪 / 关系态
- **列**：`client_variant_id, client_id, state_label, relationship_req, dialogue_mood, note`
- **说明**：4 位客户（沈太太 / 林先生 / 阿明 / 苏姑娘）在关系等级提升前后的情绪与对话基调变化。
- **自洽**：`client_id` 全部引用现有 `cl_shen/cl_lin/cl_ah_ming/cl_su`；`relationship_req` 与 `clients.relationship_level` 语义对齐。
- **预期消费者**：`DialogueSystem` / `ClientProfile`（按关系等级选态）。

### 3. `whisper_themes.csv` — 低语主题归类
- **列**：`theme_id, theme_name, associated_ending_tag, tone, example_line, description`
- **说明**：将既有 `whispers.csv` 的低语按情绪主题归类，关联到 `ending_tag`。
- **自洽**：`associated_ending_tag` 取值 ∈ 既有枚举 `{Truth, Omit, Reframe}`；`tone` 沿用游戏基调词（释然 / 怅惘）。
- **预期消费者**：`WhisperSystem`（主题化混音 / 情绪配平）。

### 4. `ambient_lines.csv` — 环境旁白
- **列**：`line_id, context, trigger, text, mood`
- **说明**：开铺 / 无客 / 雨日 / 黄昏 / 闭铺等场景的氛围旁白。
- **自洽**：自由叙事，trigger 语义清晰，mood 用词与基调一致。
- **预期消费者**：`AmbientNarrator`（场景触发旁白）。

### 5. `memory_fragments.csv` — 碎片记忆文本
- **列**：`memory_id, item_id, fragment_index, memory_text, emotion, unlock_hint`
- **说明**：每一枚碎片对应的具体记忆片段，按物件与碎片序号组织（与 `fragments.csv` 的 `item_id`/`slot_index` 同构，便于运行时联动）。
- **自洽**：`item_id` 引用现有 5 件；`fragment_index` 取值与该物件的 `fragment_count` 范围对齐（watch 0 / photo 0-1 / letter 0-1 / ornament 0-3 / mirror 0-3）。
- **预期消费者**：`MemoryLog` / 图鉴（碎片解锁时展示对应记忆）。

### 6. `npc_dialogue.csv` — NPC / 掌柜对白
- **列**：`dialogue_id, speaker, listener, context, line, mood`
- **说明**：掌柜（叙事者“我”）与街坊对客人的引导性对白。
- **自洽**：speaker/listener 为剧情角色；自由叙事，不引入需契约校验的引用。
- **预期消费者**：`DialogueSystem`（过场 / 引导对白）。

### 7. `ending_epilogues.csv` — 结局后日谈
- **列**：`epilogue_id, ending_id, epilogue_text, days_later, tone`
- **说明**：11 个既有结局（`en_001..en_011`）各自的余韵尾声。
- **自洽**：`ending_id` 全部引用现有 `endings.csv` 主键；`tone` 与 `endings.emotion_arc_stage` 一致。
- **预期消费者**：`EndingScreen`（结局画面尾声文本）。

### 8. `seasonal_events.csv` — 季节氛围事件
- **列**：`event_id, season, event_name, description, whisper_modifier`
- **说明**：春夏秋冬对铺子氛围与低语的微调。
- **自洽**：自由叙事，`whisper_modifier` 为可读修饰标签。
- **预期消费者**：`SeasonSystem`（季节化氛围 / 低语变体）。

### 9. `tutorial_lines.csv` — 新手引导文本
- **列**：`step_id, step_name, trigger, line, next_step`
- **说明**：首入铺子 → 认领委托 → 拂尘 → 倾听低语 → 抉择 的引导文案，`next_step` 串成链路（`done` 收尾）。
- **自洽**：自由叙事，step 链路闭合。
- **预期消费者**：`TutorialManager`（引导步骤文本）。

### 10. `codex_blurbs.csv` — 图鉴词条
- **列**：`codex_id, category, title, body, related_item_id`
- **说明**：物件 / 处所 / 人物的图鉴短词条，`related_item_id` 可选关联现有物件。
- **自洽**：`related_item_id` 引用现有 `it_*`（处所 / 人物类留空）。
- **预期消费者**：`CodexUI`（图鉴面板）。

### 11. `sound_cues.csv` — 音效提示
- **列**：`cue_id, event, clip_name, volume, loop, description`
- **说明**：开铺 / 闲置 / 低语浮现 / 抉择真 / 抉择藏 / 闭铺 的音效映射，`volume∈[0,1]`、`loop∈{true,false}`。
- **自洽**：枚举受控，数值可读。
- **预期消费者**：`AudioManager`（事件 → 音效映射）。

### 12. `object_lore.csv` — 物件深层来历
- **列**：`lore_id, item_id, era, origin_story, keeper_note`
- **说明**：5 件物件的历史年代与来历短篇，附掌柜批注。
- **自洽**：`item_id` 引用现有 5 件，每件一条。
- **预期消费者**：`LorePanel`（物件来历弹窗）。

---

## 三、既有 8 表未改动确认

| 表 | 行数 | 关键不变量 | 状态 |
|----|------|-----------|------|
| commissions.csv | 5（com_001..005）| 5 委托 ∈ [3,5]；com_003 is_mainplot=true | 原样 |
| clients.csv | 4（cl_shen/cl_lin/cl_ah_ming/cl_su）| — | 原样 |
| items.csv | 5（it_watch..it_mirror）| 枚举 / grid 尺寸合规 | 原样 |
| fragments.csv | 13（fr_001..fr_013）| 各物件碎片数 == fragment_count（R4）| 原样 |
| choices.csv | 12 | (commission_id, ending_tag) ⊆ endings（R5）| 原样 |
| endings.csv | 11（en_001..en_011）| ending_id 唯一；commission_id 均存在 | 原样 |
| whispers.csv | 5 | 自由叙事 | 原样 |
| quotes.csv | 11 | 自由叙事 | 原样 |

---

## 四、编码与校验结果

- **BOM 检查**：对全部 20 张 CSV 首 3 字节做二进制探测，均无 `EF BB BF`（UTF-8 无 BOM）。
- **契约校验命令**（自仓库根执行）：
  ```bat
  python tools/validate_contract.py Assets/Data
  ```
  若 `python` 不在 PATH：
  ```bat
  C:/Users/lenovo/.workbuddy/binaries/python/versions/3.13.12/python.exe tools/validate_contract.py Assets/Data
  ```
- **期望结果**：`exit 0` / 输出 `PASS`。新增 12 张表不在校验器清单内，不影响既有 8 表的 PASS。

> 最终 PASS 输出见交付汇报（运行日志）。
