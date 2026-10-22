using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    // ══════════════════════════════════════════════════════════════
    //  升级项
    // ══════════════════════════════════════════════════════════════

    /// <summary>一条卡牌升级规则</summary>
    [System.Serializable]
    public struct CardUpgradeEntry
    {
        [Tooltip("升级哪个属性")]
        public CardUpgradeTarget target;

        [Tooltip("升级哪个动作（actions 数组的下标）。费用类填 -1")]
        public int actionIndex;

        [Tooltip("每级增加多少。可为负（例如降低费用）")]
        public float deltaPerLevel;
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时实例
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 一张具体的卡牌实例。同一张 CardDataSO 可以有多个实例，升级互相独立。
    ///
    /// ⚠️ 只存"变量"；所有常量（费用、动作、升级规则）都去 CardDataSO 查。
    /// ⚠️ 存档只存这个，不存卡牌内容 —— 所以改数值不需要迁移旧存档。
    /// </summary>
    [System.Serializable]
    public class CardInstance
    {
        [Tooltip("对应 CardDataSO.cardId")]
        public string cardId;

        [Tooltip("当前等级（1 = 未升级）")]
        [Min(1)] public int level = 1;

        [Tooltip("每条升级项各被选了几次（长度 = upgradeEntries.Length）")]
        public int[] upgradePickCounts = new int[0];

        public CardInstance() { }

        public CardInstance(string id, int entryCount)
        {
            cardId = id;
            level = 1;
            upgradePickCounts = new int[entryCount < 0 ? 0 : entryCount];
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  卡牌资产
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌静态配置。一个资产 = 一张做好的卡。
    ///
    /// ⚠️ 只在编辑器里配置，游戏运行时【绝不修改】。
    ///    运行时状态（等级 / 升级进度）记在 CardInstance 上。
    ///
    /// 存放位置：Assets/ScriptableObjects/CardDatas/
    /// </summary>
    [CreateAssetMenu(fileName = "CardData", menuName = "GameCore/Card/CardDataSO")]
    public class CardDataSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、身份
        // ══════════════════════════════════════════════════════════
        [Header("① 身份")]
        [Tooltip("唯一编号。⚠️ 存档与查表都用它，一旦定下永不修改")]
        public string cardId;

        [Tooltip("战斗系统内部名称")]
        public string cardName;

        [Tooltip("设计说明（给程序 / 策划看，不是 UI 文本）")]
        [Multiline(3)] public string description;

        // ══════════════════════════════════════════════════════════
        //  二、稀有度与等级
        // ══════════════════════════════════════════════════════════
        [Header("② 稀有度（决定可升级项数量与等级上限）")]
        public CardRarity rarity = CardRarity.Common;

        [Tooltip("该资产的默认等级（1 = 一阶卡，2 = 二阶卡……）")]
        [Min(1)] public int level = 1;

        // ══════════════════════════════════════════════════════════
        //  三、费用（卡牌本体判定）
        // ══════════════════════════════════════════════════════════
        [Header("③ 费用（任一项不足则整张卡无法打出）")]
        [Min(0)] public int staminaCost;
        [Min(0)] public int energyCost;

        // ══════════════════════════════════════════════════════════
        //  四、卡面
        // ══════════════════════════════════════════════════════════
        [Header("④ 卡面皮肤（卡框由 CardFrameTableSO 自动查）")]
        public CardSkin cardSkin;

        // ══════════════════════════════════════════════════════════
        //  五、动作列表（核心）
        // ══════════════════════════════════════════════════════════
        [Header("⑤ 动作列表（按顺序依次执行）")]
        [Tooltip("例：火球+毒球 = 2 个动作；突进 = 1 个 Move 动作")]
        public CardActionData[] actions = new CardActionData[0];

        // ══════════════════════════════════════════════════════════
        //  六、升级项
        // ══════════════════════════════════════════════════════════
        [Header("⑥ 升级项（普通 1 / 史诗 2 / 传说 3；允许重复选同一属性）")]
        public CardUpgradeEntry[] upgradeEntries = new CardUpgradeEntry[0];

        // ══════════════════════════════════════════════════════════
        //  七、保底抽牌 + 特殊标记
        // ══════════════════════════════════════════════════════════
        [Header("⑦ 保底抽牌（驱散这类无数值可升的卡，升级只能升这个）")]
        [Min(0)] public int baseDrawCount;

        [Header("⑧ 特殊标记")]
        [Tooltip("true = 打出后从本轮游戏中永久移除（不进弃牌堆）")]
        public bool isOneTime;

        // ══════════════════════════════════════════════════════════
        //  九、稀有度规则
        // ══════════════════════════════════════════════════════════

        /// <summary>该稀有度允许的升级项数量</summary>
        public int GetMaxUpgradeEntryCount()
        {
            switch (rarity)
            {
                case CardRarity.Epic: return 2;
                case CardRarity.Legendary: return 3;
                default: return 1;
            }
        }

        /// <summary>该稀有度的等级上限</summary>
        public int GetMaxLevel()
        {
            switch (rarity)
            {
                case CardRarity.Epic: return 7;
                case CardRarity.Legendary: return 10;
                default: return 5;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  十、自动推导（卡框 / 卡牌类别）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 自动推导卡框风格。
        ///   ① 收集所有动作的"真元素"（排除 Physical）并去重
        ///   ② ≥2 个 → Mixed
        ///   ③ 1 个  → 该元素（元素优先于功能）
        ///   ④ 0 个  → 按功能：单一功能 → Move / Defense / PhysicalAttack
        ///                      多功能   → Mixed
        /// </summary>
        public CardFrameStyle GetFrameStyle()
        {
            List<ElementType> elements = new List<ElementType>();
            List<CardActionFunction> funcs = new List<CardActionFunction>();

            if (actions != null)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    CardActionData a = actions[i];
                    if (a == null) continue;

                    if (a.element != ElementType.Physical && !elements.Contains(a.element))
                        elements.Add(a.element);

                    CardActionFunction f = MapTypeToFunction(a.type);
                    if (!funcs.Contains(f)) funcs.Add(f);
                }
            }

            if (elements.Count >= 2) return CardFrameStyle.Mixed;
            if (elements.Count == 1) return MapElementToFrame(elements[0]);

            if (funcs.Count == 1)
            {
                switch (funcs[0])
                {
                    case CardActionFunction.Move: return CardFrameStyle.Move;
                    case CardActionFunction.Defense: return CardFrameStyle.Defense;
                    default: return CardFrameStyle.PhysicalAttack;
                }
            }
            if (funcs.Count >= 2) return CardFrameStyle.Mixed;

            return CardFrameStyle.PhysicalAttack;
        }

        /// <summary>自动推导卡牌类别（UI 分类用）</summary>
        public CardType GetCardType()
        {
            bool hasDamage = false, hasDefense = false, hasSupport = false;

            if (actions != null)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    CardActionData a = actions[i];
                    if (a == null) continue;

                    switch (MapTypeToFunction(a.type))
                    {
                        case CardActionFunction.Damage: hasDamage = true; break;
                        case CardActionFunction.Defense: hasDefense = true; break;
                        default: hasSupport = true; break;
                    }
                }
            }

            int n = (hasDamage ? 1 : 0) + (hasDefense ? 1 : 0) + (hasSupport ? 1 : 0);
            if (n >= 2) return CardType.Mix;
            if (hasDamage) return CardType.Attack;
            if (hasDefense) return CardType.Defense;
            return CardType.Status;
        }

        private static CardFrameStyle MapElementToFrame(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return CardFrameStyle.Fire;
                case ElementType.Ice: return CardFrameStyle.Ice;
                case ElementType.Lightning: return CardFrameStyle.Lightning;
                case ElementType.Poison: return CardFrameStyle.Poison;
                default: return CardFrameStyle.PhysicalAttack;
            }
        }

        /// <summary>动作类型 → 功能大类</summary>
        public static CardActionFunction MapTypeToFunction(CardActionType t)
        {
            switch (t)
            {
                case CardActionType.Move: return CardActionFunction.Move;
                case CardActionType.BlockDamage:
                case CardActionType.BlockControl:
                case CardActionType.Shield: return CardActionFunction.Defense;
                case CardActionType.ApplyBuff:
                case CardActionType.Heal:
                case CardActionType.Draw:
                case CardActionType.Purge: return CardActionFunction.Support;
                default: return CardActionFunction.Damage;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  十一、升级项可选范围
        // ══════════════════════════════════════════════════════════

        /// <summary>某类动作支持的可升级属性（静态表）</summary>
        public static CardUpgradeTarget[] GetSupportedUpgrades(CardActionType t)
        {
            switch (t)
            {
                case CardActionType.Damage:
                    return new[]
                    {
                        CardUpgradeTarget.DamagePercent,
                        CardUpgradeTarget.CritRatePoint,
                        CardUpgradeTarget.CritDamagePoint,
                        CardUpgradeTarget.KnockbackGrids,
                        CardUpgradeTarget.KnockbackChance,
                        CardUpgradeTarget.StunCoeff,
                        CardUpgradeTarget.AoESize,
                    };

                case CardActionType.Projectile:
                    return new[]
                    {
                        CardUpgradeTarget.DamagePercent,
                        CardUpgradeTarget.CritRatePoint,
                        CardUpgradeTarget.CritDamagePoint,
                        CardUpgradeTarget.KnockbackGrids,
                        CardUpgradeTarget.KnockbackChance,
                        CardUpgradeTarget.StunCoeff,
                        CardUpgradeTarget.ProjectileCount,
                        CardUpgradeTarget.BounceCount,
                        CardUpgradeTarget.PierceCount,
                        CardUpgradeTarget.SplitCount,
                    };

                case CardActionType.ChainLightning:
                    return new[]
                    {
                        CardUpgradeTarget.DamagePercent,
                        CardUpgradeTarget.CritRatePoint,
                        CardUpgradeTarget.CritDamagePoint,
                        CardUpgradeTarget.KnockbackGrids,
                        CardUpgradeTarget.KnockbackChance,
                        CardUpgradeTarget.StunCoeff,
                        CardUpgradeTarget.ChainCount,
                        CardUpgradeTarget.ChainRange,
                    };

                case CardActionType.Move: return new[] { CardUpgradeTarget.MoveGrids };
                case CardActionType.BlockDamage: return new[] { CardUpgradeTarget.BlockDamageCount };
                case CardActionType.BlockControl: return new[] { CardUpgradeTarget.BlockControlCount };
                case CardActionType.Shield: return new[] { CardUpgradeTarget.ShieldAmount };

                case CardActionType.ApplyBuff:
                    return new[]
                    {
                        CardUpgradeTarget.BuffStacks,
                        CardUpgradeTarget.BuffApplyChance,
                    };

                case CardActionType.Heal: return new[] { CardUpgradeTarget.HealPercent };
                case CardActionType.Draw: return new[] { CardUpgradeTarget.DrawCount };

                default: return new CardUpgradeTarget[0];
            }
        }

        /// <summary>
        /// 该卡【实际可选】的全部升级属性。
        /// = 费用两项 + 保底 DrawCount + 所有动作支持项（+ 条件性的 BuffDuration）
        /// </summary>
        public List<CardUpgradeTarget> GetAvailableUpgradeTargets()
        {
            List<CardUpgradeTarget> list = new List<CardUpgradeTarget>();
            list.Add(CardUpgradeTarget.StaminaCost);
            list.Add(CardUpgradeTarget.EnergyCost);
            list.Add(CardUpgradeTarget.DrawCount);       // 保底项，永远可选

            if (actions != null)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    CardActionData a = actions[i];
                    if (a == null) continue;

                    CardUpgradeTarget[] arr = GetSupportedUpgrades(a.type);
                    for (int j = 0; j < arr.Length; j++)
                        if (!list.Contains(arr[j])) list.Add(arr[j]);

                    // 只有"有回合限制"的 Buff 才能升级持续回合
                    if (a.IsBuffDurationUpgradable && !list.Contains(CardUpgradeTarget.BuffDuration))
                        list.Add(CardUpgradeTarget.BuffDuration);
                }
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════
        //  十二、按实例算最终数值
        // ══════════════════════════════════════════════════════════

        /// <summary>某条升级项在实例上被选了几次带来的累计增量</summary>
        public float GetUpgradeDelta(CardInstance inst, int entryIndex)
        {
            if (inst == null || inst.upgradePickCounts == null) return 0f;
            if (entryIndex < 0 || entryIndex >= upgradeEntries.Length) return 0f;
            if (entryIndex >= inst.upgradePickCounts.Length) return 0f;

            return upgradeEntries[entryIndex].deltaPerLevel * inst.upgradePickCounts[entryIndex];
        }

        /// <summary>某个目标属性在实例上的总增量（actionIndex = -1 表示费用类）</summary>
        public float GetTotalDelta(CardInstance inst, CardUpgradeTarget target, int actionIndex)
        {
            float sum = 0f;
            if (upgradeEntries == null) return sum;

            for (int i = 0; i < upgradeEntries.Length; i++)
            {
                CardUpgradeEntry e = upgradeEntries[i];
                if (e.target != target) continue;
                if (e.actionIndex != actionIndex) continue;
                sum += GetUpgradeDelta(inst, i);
            }
            return sum;
        }

        /// <summary>实例的体力消耗（最低 0）</summary>
        public int GetStaminaCost(CardInstance inst)
            => Mathf.Max(0, Mathf.RoundToInt(staminaCost + GetTotalDelta(inst, CardUpgradeTarget.StaminaCost, -1)));

        /// <summary>实例的能量消耗（最低 0）</summary>
        public int GetEnergyCost(CardInstance inst)
            => Mathf.Max(0, Mathf.RoundToInt(energyCost + GetTotalDelta(inst, CardUpgradeTarget.EnergyCost, -1)));

        /// <summary>实例的额外抽牌数（保底升级项）</summary>
        public int GetDrawCount(CardInstance inst)
            => Mathf.Max(0, Mathf.RoundToInt(baseDrawCount + GetTotalDelta(inst, CardUpgradeTarget.DrawCount, -1)));

        /// <summary>读某个动作的某个可升级属性的【1 级基础值】</summary>
        public static float GetActionBaseValue(CardActionData a, CardUpgradeTarget target)
        {
            if (a == null) return 0f;

            switch (target)
            {
                case CardUpgradeTarget.DamagePercent: return a.damagePercent;
                case CardUpgradeTarget.CritRatePoint: return a.critRatePoint;
                case CardUpgradeTarget.CritDamagePoint: return a.critDamagePoint;
                case CardUpgradeTarget.KnockbackGrids: return a.knockbackGrids;
                case CardUpgradeTarget.KnockbackChance: return a.knockbackChance;
                case CardUpgradeTarget.StunCoeff: return a.stunCoeff;
                case CardUpgradeTarget.ProjectileCount: return a.projectileCount;
                case CardUpgradeTarget.BounceCount: return a.bounceCount;
                case CardUpgradeTarget.PierceCount: return a.pierceCount;
                case CardUpgradeTarget.SplitCount: return a.splitCount;
                case CardUpgradeTarget.ChainCount: return a.chainCount;
                case CardUpgradeTarget.ChainRange: return a.chainRange;
                case CardUpgradeTarget.BuffStacks: return a.applyBuffStacks;
                case CardUpgradeTarget.BuffApplyChance: return a.applyBuffChance;
                case CardUpgradeTarget.BuffDuration: return a.buffDurationOverride;
                case CardUpgradeTarget.HealPercent: return a.healPercent;
                case CardUpgradeTarget.DrawCount: return a.drawCount;
                case CardUpgradeTarget.BlockDamageCount: return a.blockCount;
                case CardUpgradeTarget.BlockControlCount: return a.blockCount;
                case CardUpgradeTarget.ShieldAmount: return a.shieldStacks;
                case CardUpgradeTarget.MoveGrids: return a.moveGrids;
                case CardUpgradeTarget.AoESize: return a.aoeSize;
                default: return 0f;
            }
        }

        /// <summary>读某个动作的某个属性在【指定实例】下的最终值</summary>
        public float GetActionValue(CardActionData action, int actionIndex,
                                    CardUpgradeTarget target, CardInstance inst)
            => GetActionBaseValue(action, target) + GetTotalDelta(inst, target, actionIndex);
    }
}