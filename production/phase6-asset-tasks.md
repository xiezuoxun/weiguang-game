# Phase 6-A 美术资产生产任务清单

> 作者：林绘澄（art-director）｜依据 `production/art-spec.md`
> 范围：B1 / B2 / B3 / 音频 / 5 项缺口。本清单供美术同学在 **Unity 内或外部工具** 生成二进制（PNG/音频）。
> 沙箱（art-director 环境）**无法生成二进制**，仅交付文本（.shader/.cs/.md），见交付报告。

## 总览（优先级）
| 接口 | 资产 | 数量 | 优先级 | 验收命令（见各节） |
|---|---|---|---|---|
| B1 | 尘遮罩 Shader + 噪点/光晕贴图 | 1 Shader(已交付) + 2 贴图 | P0 | `见 §B1` |
| B2 | 槽位底图 + 碎片 Sprite | 5 底图 + 13 碎片 | P0 | `见 §B2` |
| B3 | 纸签双态 + 选中 Shader | 1 模板×2态 + 1 Shader(stub) | P0 | `见 §B3` |
| 缺口1 | 物件立绘/图鉴缩略 | 5×512 + 5×256 | P1 | `见 §G1` |
| 缺口2 | 低语浮纸签底 | 1 | P1 | `见 §G2` |
| 缺口3 | 客户符号 | 4 | P2 | `见 §G3` |
| 缺口4 | 手势笔触 | 1(序列) | P1 | `见 §G4` |
| 缺口5 | DustGrid 承托底 | 1 | P1 | `见 §G5` |
| 音频 | SFX×5 + BGM×3 | 8 | P1/P2 | `见 §AUDIO` |

---

## B1 拂尘微光 Shader 配套贴图（P0）
| 资产 | 尺寸 | 格式 | 命名 | 对齐锚点 |
|---|---|---|---|---|
| 尘遮罩噪点（无缝平铺灰阶） | 256×256 或 512×512 | PNG(灰阶/透明) | `DustNoise_256` / `_512` | tiled，Shader `_DustTex` |
| 微光径向光晕（可选） | 256×256 | PNG(径向渐变) | `GlowRadial_256` | Shader `_GlowTex` |

- **验收命令**（Unity 内手测，非 CI 自动）：
  1. 材质挂 `Weiguang/DustReveal`，调 `_RevealProgress` 0→1，尘遮罩平滑消退、微光渐显。
  2. 在 `_RevealProgress` 跨 0.25/0.50/0.75 处，逐帧对比亮度差 ≥15%（顿挫脉冲）。
  3. `_GlowColor` 热更不影响遮罩逻辑；中端机单物件满帧。
- Shader 源码已交付：`game/Assets/Art/Shaders/DustReveal.shader`（手写 HLSL，URP 2D 兼容）。

---

## B2 拼合槽位底图 + 碎片 Sprite（P0）
锚点严格按 `fragments.csv`（归一化 0–1，原点左上），见 art-spec.md §0.2 表。
| 资产 | 尺寸 | 格式 | 命名 | 备注 |
|---|---|---|---|---|
| 槽位引导底图 it_watch | ≈512×512 | PNG(透明) | `Slots/it_watch_board` | 单片锚点 (0.50,0.50) |
| 槽位引导底图 it_photo | ≈512×512 | PNG | `Slots/it_photo_board` | 2 片锚点 (0.33,0.45)/(0.67,0.55) |
| 槽位引导底图 it_letter | ≈512×512 | PNG | `Slots/it_letter_board` | 2 片 (0.33,0.40)/(0.67,0.60) |
| 槽位引导底图 it_ornament | ≈768×768 | PNG | `Slots/it_ornament_board` | 4 片 (0.20,0.50)/(0.40,0.42)/(0.60,0.58)/(0.80,0.46) |
| 槽位引导底图 it_mirror | ≈768×768 | PNG | `Slots/it_mirror_board` | 4 片 (0.20,0.45)/(0.40,0.55)/(0.60,0.48)/(0.80,0.52) |
| 碎片 fr_001…fr_013 | 256×256（图集） | PNG(透明)+SpriteAtlas | `Fragments/fr_001`…`fr_013` | 同物件可拼合复原；边缘对齐无重缝 |

- 槽位视觉热区须覆盖归属带（Y∈[0.33,0.67]，X 容差 ±0.15），视觉槽比碎片略大容纳回弹。
- **验收命令**：13 碎片齐备且同物件可拼合；5 底图锚点偏差 <2% 屏宽；锁定态除颜色外有描边/亮度区分。

---

## B3 抉择纸签纹样（P0）
| 资产 | 尺寸 | 格式 | 命名 | 备注 |
|---|---|---|---|---|
| 纸签·未选中 | 480×200(横)或720×160(纵) | PNG(透明) | `Choices/choice_tab_idle` | 宣纸毛边；预留文字安全区(≤12中文字) |
| 纸签·选中 | 同尺寸 | PNG(透明) | `Choices/choice_tab_selected` | 浮起+描边加粗+微光；色弱友好三重区分 |
| 选中态 Shader（可选） | — | .shader | 复用/新增 | 暴露 `_Selected`(0/1) 或 `_Highlight`(Color) |

- 单模板复用 2/3 选项布局（com_002 为 3 选项）。
- **验收命令**：双态对比度清晰（亮度+描边差）；选中动效 ≤250ms（浮起 8–12px + 描边呼吸）。

---

## 5 项缺口（原 B1/B2/B3 外）
- **G1 物件立绘/缩略**（P1）：`Items/it_*`（完整态 512×512）×5；图鉴缩略 `Codex/thumb_it_*`（256×256）×5。对齐 `items.csv`。
- **G2 低语浮纸签底**（P1）：`UI/whisper_note`（约 600×240），承载 `whispers.csv` 5 条文案，与 B3 纸签区分（剧情低语）。
- **G3 客户符号**（P2）：`Clients/sym_cl_shen`/`cl_lin`/`cl_ah_ming`/`cl_su`（约 256×256 剪影/符号），非写实。
- **G4 手势笔触**（P1）：`FX/dust_brush`（序列帧/9-slice，约 128×128×N），指尖微光尘刷提示。
- **G5 DustGrid 承托底**（P1）：`BG/dust_stage`（1080×1920 内布局中性绒布/承托台），Shader 叠其上为尘遮罩。

---

## 音频清单（P1/P2）
母带 WAV(44.1k/16bit)，移动端交付 OGG(质量~0.5–0.6，≤300KB)。
| 编号 | 音效 | 触发事件 | 时长 | 命名 |
|---|---|---|---|---|
| SFX-01 | 拂尘沙沙 | EVT_REVEAL_WHISPER(叠加) | 0.3–0.8s循环 | `sfx_dust_loop` |
| SFX-02 | 拼合咔哒 | EVT_ASSEMBLE_COMPLETE | 0.1–0.2s | `sfx_snap`(按 Glass/Wood/Paper 三变体) |
| SFX-03 | 抉择纸响 | EVT_CHOICE_MADE 前 | 0.15–0.3s | `sfx_paper` |
| SFX-04 | 归档完成 | EVT_ARCHIVED | 0.4–0.8s | `sfx_archive` |
| SFX-05 | UI 微反馈 | 通用手势 | 0.05–0.15s | `sfx_ui_tick` |
| BGM-01 | 主氛围 | Examining→Assembling | 循环 | `bgm_ambient` |
| BGM-02 | 抉择悬停 | Choosing | 淡入淡出 | `bgm_choice` |
| BGM-03 | 归档/图鉴 | Archived/Codex | 循环 | `bgm_codex` |

- **验收命令**：导入 Unity AudioMixer，按事件挂 AudioSource；无音频不阻塞核心循环（P1/P2）。

---

## 命名守护（CI 已覆盖，勿漂移）
- fragment_id / item_id / client_id / commission_id 必须与 `game/Assets/Data/*.csv` 一致。
- 资源路径前缀：`Fragments/`、`Items/`、`Slots/`、`Choices/`、`Clients/`、`Codex/`、`UI/`、`BG/`、`FX/`。
- 事件常量只引用 `GameEvents.EVT_*`（C2 门），禁止字面量。
