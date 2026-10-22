using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单位的「可消耗资源」：能量、体力、复活次数。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么必须是 class，不能是 struct】
    /// ══════════════════════════════════════════════════════════════
    ///  struct 赋值 = 整份复制。如果 UnitResource 是 struct，
    ///  那么 `unit.Resource.CurrentEnergy--` 这种写法会编译不过，
    ///  必须写成 `var r = unit.Resource; r.CurrentEnergy--; unit.Resource = r;`
    ///  —— 每个扣费点都要三行，必然漏写。
    ///
    ///  class 是引用类型，UnitBase 持有的是同一个对象的引用，
    ///  任何地方改都会立刻反映到本体。
    ///
    ///  【对比 UnitStat】
    ///      UnitStat      = struct  → 「算出来的结果」，每次要用时重算一份
    ///      UnitResource  = class   → 「要扣减的状态」，必须只有一份
    /// ══════════════════════════════════════════════════════════════
    ///  【三条独立的时间线】
    /// ══════════════════════════════════════════════════════════════
    ///  能量、体力、复活次数 的「重置时机」互不相同：
    ///
    ///    时机                能量/体力          复活次数
    ///    ────────────────────────────────────────────────
    ///    新一轮开始（开新档）  → initial          → initial
    ///    每场战斗开始          → initial          【不变】
    ///    每回合开始            +regen（钳到 max）  【不变】
    ///    该单位死亡后复活      → initial          -1
    ///
    ///  ⚠️ 最容易写错的一条：ResetForBattle() 里【绝对不能】动 reviveCount。
    ///     复活次数是「一整轮游戏」的资源，跨战斗累计消耗。
    ///     如果每场战斗都重置，那玩家可以无限送人头，复活机制就废了。
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    [System.Serializable]
    public class UnitResource
    {
        // ══════════════════════════════════════════════════════════
        //  一、配置（Inspector 里填，运行时只读）
        // ══════════════════════════════════════════════════════════

        [Header("能量（法术 / 技能卡消耗）")]
        [Tooltip("每场战斗开始时的能量。⚠️ 注意这【不是】上限")]
        [Min(0)] public int initialEnergy = 10;

        [Tooltip("能量上限。回合回能后不会超过这个值")]
        [Min(0)] public int maxEnergy = 10;

        [Tooltip("每回合开始时恢复的能量")]
        [Min(0)] public int energyRegenPerTurn = 2;

        [Header("体力（移动 / 防御 / 普攻消耗）")]
        [Tooltip("每场战斗开始时的体力。⚠️ 注意这【不是】上限")]
        [Min(0)] public int initialStamina = 5;

        [Tooltip("体力上限。回合回体后不会超过这个值")]
        [Min(0)] public int maxStamina = 10;

        [Tooltip("每回合开始时恢复的体力")]
        [Min(0)] public int staminaRegenPerTurn = 3;

        [Header("复活")]
        [Tooltip("每一轮游戏（从新开局算起）总共能复活几次。\n" +
                 "⚠️ 战斗结束不重置，只有 InitForRun() 才重置")]
        [Min(0)] public int initialReviveCount = 1;

        // ══════════════════════════════════════════════════════════
        //  二、运行时状态（外部只读）
        // ══════════════════════════════════════════════════════════

        [Header("运行时状态（只读，请勿手动修改）")]
        [SerializeField] private int currentEnergy;
        [SerializeField] private int currentStamina;
        [SerializeField] private int reviveCount;

        /// <summary>当前能量</summary>
        public int CurrentEnergy => currentEnergy;

        /// <summary>当前体力</summary>
        public int CurrentStamina => currentStamina;

        /// <summary>剩余复活次数</summary>
        public int ReviveCount => reviveCount;

        // ══════════════════════════════════════════════════════════
        //  三、生命周期
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 【新一轮游戏开始时调用】。
        /// 重置复活次数，并把能量 / 体力设到初始值。
        /// 只有「开新档 / 重开一局」才调它，每场战斗不要调。
        /// </summary>
        public void InitForRun()
        {
            reviveCount = initialReviveCount;
            ResetForBattle();
        }

        /// <summary>
        /// 【每场战斗开始时调用】。
        /// 能量 / 体力回到 initial（不是 max），复活次数【保持不变】。
        ///
        /// 为什么回 initial 而不是 max：
        ///     体力 5/10 的设计意图是「开场只有半管体力」，
        ///     玩家必须靠回合回体和卡牌补体来攒资源。
        ///     如果开场回满 10/10，体力管理就失去意义了。
        /// </summary>
        public void ResetForBattle()
        {
            currentEnergy = Clamp(initialEnergy, 0, maxEnergy);
            currentStamina = Clamp(initialStamina, 0, maxStamina);
            // ⚠️ 这里【故意】不碰 reviveCount —— 见类注释的表格
        }

        /// <summary>
        /// 【每回合开始时调用】。按配置恢复能量 / 体力，并钳制到上限。
        /// </summary>
        public void RegenAtTurnStart()
        {
            currentEnergy = Clamp(currentEnergy + energyRegenPerTurn, 0, maxEnergy);
            currentStamina = Clamp(currentStamina + staminaRegenPerTurn, 0, maxStamina);
        }

        /// <summary>
        /// 【该单位复活时调用】。能量 / 体力回到 initial（跟战斗开始时一致），
        /// 复活次数不在这里扣 —— 扣次数请先调 TryConsumeRevive()。
        /// </summary>
        public void RefillOnRevive()
        {
            currentEnergy = Clamp(initialEnergy, 0, maxEnergy);
            currentStamina = Clamp(initialStamina, 0, maxStamina);
        }

        // ══════════════════════════════════════════════════════════
        //  四、资源增减
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 检查是否付得起。staminaCost / energyCost 传正数。
        /// </summary>
        public bool CanAfford(int staminaCost, int energyCost)
        {
            return currentStamina >= staminaCost
                && currentEnergy >= energyCost;
        }

        /// <summary>
        /// 尝试支付。够则两个一起扣并返回 true；不够则【一个都不扣】并返回 false。
        ///
        /// 【为什么必须原子化】
        ///   如果先扣体力、再发现能量不够、直接 return false，
        ///   玩家的体力就白扣了。这是卡牌游戏最经典的资损 bug。
        ///   所以顺序永远是：先 CanAfford 全部检查 → 再全部扣减。
        /// </summary>
        public bool TrySpend(int staminaCost, int energyCost)
        {
            if (!CanAfford(staminaCost, energyCost))
                return false;

            currentStamina -= staminaCost;
            currentEnergy -= energyCost;
            return true;
        }

        /// <summary>恢复体力（卡牌效果 / 道具）。超过上限的部分丢弃。</summary>
        public void GainStamina(int amount)
        {
            if (amount <= 0) return;
            currentStamina = Clamp(currentStamina + amount, 0, maxStamina);
        }

        /// <summary>恢复能量（卡牌效果 / 道具）。超过上限的部分丢弃。</summary>
        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;
            currentEnergy = Clamp(currentEnergy + amount, 0, maxEnergy);
        }

        // ══════════════════════════════════════════════════════════
        //  五、复活
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 尝试消耗一次复活机会。有则扣 1 并返回 true；没有则返回 false。
        /// 调用方应在返回 true 后继续执行 RefillOnRevive() + 重置位置等。
        /// </summary>
        public bool TryConsumeRevive()
        {
            if (reviveCount <= 0) return false;

            reviveCount--;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  六、工具
        // ══════════════════════════════════════════════════════════

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 里改了 initial / max 之后自动纠正运行时值，
        /// 避免出现「max 改成 5，但 currentEnergy 还是 10」的脏数据。
        /// </summary>
        public void ClampRuntimeValuesInEditor()
        {
            currentEnergy = Clamp(currentEnergy, 0, maxEnergy);
            currentStamina = Clamp(currentStamina, 0, maxStamina);
            reviveCount = Clamp(reviveCount, 0, initialReviveCount);
        }
#endif
    }
}
