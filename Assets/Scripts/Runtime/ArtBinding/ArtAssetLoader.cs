// ArtAssetLoader.cs — B1/B2/B3 资产加载转接层（Runtime 层脚手架）
// 作者：林绘澄（art-director）｜Phase 6-A
// 职责：按 item_id / fragment_id / option_id 从 Resources 加载 Sprite 的接口 stub。
// 当前用 Resources.Load 占位；接入 Addressables 时仅需改 LoadSprite 实现（TODO 已标注）。
//
// 依赖：Weiguang.Core（无）、Weiguang.Runtime（同 asmdef）。本文件不注入 UnityEngine 到 Core 层。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 美术资产加载器：把 CSV 的 fragment_id / item_id 映射为 Sprite 资源。
    /// 约定资源路径（Resources 根下）：
    ///   Fragments/&lt;fragment_id&gt;    例：Resources/Fragments/fr_001
    ///   Items/&lt;item_id&gt;            例：Resources/Items/it_watch
    ///   Slots/&lt;item_id&gt;_board      例：Resources/Slots/it_ornament_board
    ///   Choices/choice_tab            例：Resources/Choices/choice_tab（双态在子目录 selected/）
    /// 命名必须与 production/art-spec.md §0.2 / §2.3 / §3 对齐（fr_001…fr_013 等）。
    /// </summary>
    public class ArtAssetLoader
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        /// <summary>按资产 key 加载 Sprite（带缓存）。Resources 缺失时返回 null 且不抛异常
        /// （无 Unity 资产时美术表现留 TODO，不崩核心循环）。</summary>
        public Sprite LoadSprite(string resourceKey)
        {
            if (string.IsNullOrEmpty(resourceKey)) return null;
            if (_cache.TryGetValue(resourceKey, out var hit)) return hit;

            // TODO(Addressables): 替换为 Addressables.LoadAssetAsync&lt;Sprite&gt; 并维护异步句柄；
            // 当前用同步 Resources.Load 占位，仅供 6-B 场景串联与编辑器验证。
            var sp = Resources.Load<Sprite>(resourceKey);
            if (sp != null) _cache[resourceKey] = sp;
            else
                Debug.LogWarning($"[ArtBinding] 资产缺失（占位）：{resourceKey}（美术需在 Unity 内产出 PNG）");
            return sp;
        }

        public Sprite LoadFragment(string fragmentId) => LoadSprite($"Fragments/{fragmentId}");
        public Sprite LoadItem(string itemId)         => LoadSprite($"Items/{itemId}");
        public Sprite LoadSlotBoard(string itemId)    => LoadSprite($"Slots/{itemId}_board");
        public Sprite LoadChoiceTab(bool selected)    => LoadSprite($"Choices/choice_tab_{(selected ? "selected" : "idle")}");

        /// <summary>清空缓存（切场景/换委托时调用）。</summary>
        public void ClearCache() => _cache.Clear();
    }
}
