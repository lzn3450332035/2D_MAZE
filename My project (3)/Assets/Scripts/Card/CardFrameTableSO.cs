using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 卡框查询表：8 种风格 × 3 种稀有度 = 24 张卡框。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么是"行 × 列"的数组，而不是 24 个独立字段】
    /// ══════════════════════════════════════════════════════════════
    ///   24 个独立字段（physicalCommon / physicalEpic / ...）：
    ///     · Inspector 里是 24 行长列表，看不出哪一行属于哪个风格
    ///     · 漏配一个不会报错，只能靠肉眼找
    ///
    ///   8 行 × 3 列（每行一个 FrameRow）：
    ///     · Inspector 里 8 个可折叠块，每块 4 个字段
    ///     · 一眼看出哪一行缺，IsComplete() 还能自动报错
    ///
    /// ────────────────────────────────────────────────────────────────
    ///  【为什么不用 Dictionary】
    ///   Unity 不能序列化 Dictionary（Inspector 里无法编辑）。
    ///   8 行数据的线性查找 = 最多 8 次比较，性能完全可忽略。
    /// ────────────────────────────────────────────────────────────────
    ///
    /// 图片位置：Assets/Card/Frames/
    /// 命名规范：frame_{style}_{rarity}.png
    ///           例 frame_fire_epic.png
    ///
    /// 存放位置：Assets/ScriptableObjects/CardFrames/CardFrameTable.asset
    /// </summary>
    [CreateAssetMenu(fileName = "CardFrameTable", menuName = "GameCore/Card/CardFrameTable")]
    public class CardFrameTableSO : ScriptableObject
    {
        /// <summary>一行 = 一种卡框风格，三列 = 三种稀有度</summary>
        [System.Serializable]
        public struct FrameRow
        {
            [Tooltip("这一行属于哪种风格")]
            public CardFrameStyle style;

            [Tooltip("普通卡框")]
            public Sprite common;

            [Tooltip("史诗卡框")]
            public Sprite epic;

            [Tooltip("传说卡框")]
            public Sprite legendary;

            /// <summary>按稀有度取本行的卡框</summary>
            public Sprite Get(CardRarity rarity)
            {
                switch (rarity)
                {
                    case CardRarity.Epic: return epic;
                    case CardRarity.Legendary: return legendary;
                    default: return common;
                }
            }

            /// <summary>本行是否至少配了一张图</summary>
            public bool HasAny => common != null || epic != null || legendary != null;
        }

        [Header("8 行卡框（缺行会自动回落到 PhysicalAttack）")]
        [Tooltip("每行一种风格。⚠️ 建议 8 种风格全部配齐，用 IsComplete() 检查")]
        public FrameRow[] rows = new FrameRow[0];

        // ══════════════════════════════════════════════════════════
        //  查询
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 按 风格 × 稀有度 查卡框。
        ///
        /// 三层回落策略（保证永远能返回点什么，不会 null 崩 UI）：
        ///   ① 精确匹配 (style, rarity)
        ///   ② 该风格缺这一稀有度 → 回落到同行的 common
        ///   ③ 该风格整行缺失   → 回落到 PhysicalAttack 行的对应稀有度
        /// </summary>
        public Sprite GetFrame(CardFrameStyle style, CardRarity rarity)
        {
            if (rows == null) return null;

            // ① + ②：找到对应风格行
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].style != style) continue;

                Sprite s = rows[i].Get(rarity);
                if (s != null) return s;

                // 该稀有度没配 → 回落到本行普通框
                if (rows[i].common != null) return rows[i].common;
            }

            // ③：整行缺失 → 回落到物理攻击行
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].style != CardFrameStyle.PhysicalAttack) continue;

                Sprite s = rows[i].Get(rarity);
                if (s != null) return s;
                return rows[i].common;
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  编辑器自检
        // ══════════════════════════════════════════════════════════

        /// <summary>8 种风格是否都配了至少一行</summary>
        public bool IsComplete()
        {
            if (rows == null) return false;

            // 遍历 0~7 每一个 CardFrameStyle 值
            for (int s = 0; s <= (int)CardFrameStyle.Defense; s++)
            {
                bool found = false;
                for (int i = 0; i < rows.Length; i++)
                {
                    if ((int)rows[i].style == s && rows[i].HasAny)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在 Inspector 里改完就检查。
        /// ⚠️ 只在编辑器生效，打包时被剔除。
        /// </summary>
        private void OnValidate()
        {
            if (rows == null) return;

            // 重复风格检查
            for (int i = 0; i < rows.Length; i++)
            {
                for (int j = i + 1; j < rows.Length; j++)
                {
                    if (rows[i].style == rows[j].style)
                    {
                        Debug.LogError(
                            $"[CardFrameTable] 风格 {rows[i].style} 重复出现在第 {i} 行和第 {j} 行", this);
                    }
                }
            }

            // 缺行检查
            if (!IsComplete())
                Debug.LogWarning("[CardFrameTable] 有风格未配置（会回落成物理卡框）", this);
        }
#endif
    }
}