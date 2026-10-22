using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 阵营加成表。记录"攻击方 A 打防守方 B 时"的额外数值修正。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【与关系表的区别】
    /// ══════════════════════════════════════════════════════════════
    ///   关系表：对称（A 视 B = B 视 A）→ 决定"能不能打 / 能不能辅助"
    ///   加成表：非对称（A→B 与 B→A 是两条独立规则）→ 决定"打了有多疼"
    ///
    ///   例：玩家克制怪物（+20% 伤害），但怪物打玩家没有加成
    ///       → 只配一条 (玩家 → 怪物, 1.2)
    ///
    /// 存放位置：Assets/ScriptableObjects/Factions/FactionBonusTable.asset
    /// </summary>
    [CreateAssetMenu(fileName = "FactionBonusTable", menuName = "GameCore/Faction/FactionBonusTable")]
    public class FactionBonusTableSO : ScriptableObject
    {
        /// <summary>一条非对称加成规则</summary>
        [System.Serializable]
        public struct BonusRule
        {
            [Tooltip("攻击方阵营")]
            public FactionDataSO attacker;

            [Tooltip("防守方阵营")]
            public FactionDataSO defender;

            [Tooltip("伤害倍率（1.2 = +20%，0.8 = -20%）。不加成填 1")]
            [Range(0f, 5f)] public float damageMultiplier;

            [Tooltip("命中时额外叠加的暴击率点数（可为负）")]
            public int critRatePointBonus;

            [Tooltip("命中时额外叠加的击退格数（可为负）")]
            public int knockbackGridsBonus;
        }

        [Header("加成规则（非对称，A→B 与 B→A 各配一条）")]
        public BonusRule[] rules = new BonusRule[0];

        // ══════════════════════════════════════════════════════════
        //  查询
        // ══════════════════════════════════════════════════════════

        /// <summary>查 (攻击方 → 防守方) 的伤害倍率。没配规则时返回 1（不变）</summary>
        public float GetDamageMultiplier(FactionDataSO attacker, FactionDataSO defender)
        {
            BonusRule r;
            return TryFind(attacker, defender, out r) ? r.damageMultiplier : 1f;
        }

        /// <summary>查暴击率点数加成</summary>
        public int GetCritRatePointBonus(FactionDataSO attacker, FactionDataSO defender)
        {
            BonusRule r;
            return TryFind(attacker, defender, out r) ? r.critRatePointBonus : 0;
        }

        /// <summary>查击退格数加成</summary>
        public int GetKnockbackGridsBonus(FactionDataSO attacker, FactionDataSO defender)
        {
            BonusRule r;
            return TryFind(attacker, defender, out r) ? r.knockbackGridsBonus : 0;
        }

        /// <summary>内部：按方向查找规则（不做对称匹配）</summary>
        private bool TryFind(FactionDataSO attacker, FactionDataSO defender, out BonusRule result)
        {
            result = default;

            if (attacker == null || defender == null) return false;
            if (rules == null) return false;

            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].attacker == attacker && rules[i].defender == defender)
                {
                    result = rules[i];
                    return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rules == null) return;

            for (int i = 0; i < rules.Length; i++)
            {
                BonusRule r = rules[i];

                if (r.attacker == null || r.defender == null)
                {
                    Debug.LogWarning($"[FactionBonusTable] 第 {i} 条规则的阵营为空", this);
                    continue;
                }

                if (r.damageMultiplier <= 0f)
                    Debug.LogWarning($"[FactionBonusTable] 第 {i} 条规则的伤害倍率是 {r.damageMultiplier}，会导致伤害归零", this);

                // 同方向重复检查
                for (int j = i + 1; j < rules.Length; j++)
                {
                    if (rules[i].attacker == rules[j].attacker &&
                        rules[i].defender == rules[j].defender)
                    {
                        Debug.LogError($"[FactionBonusTable] 第 {i} 条与第 {j} 条方向重复（{r.attacker.name} → {r.defender.name}）", this);
                    }
                }
            }
        }
#endif
    }
}