using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace GameCore
{
    /// <summary>
    /// 投射物运行时数据。值类型结构体，每个投射物持有一份独立副本。
    ///
    /// ⚠️ 只在这里改，绝不修改 ProjectileDataSO 模板。
    /// ⚠️ 因为它是 struct，任何"修改"都必须通过 ref 传递（见 IProjectileModifier）。
    ///
    /// 字段来源：
    ///   ProjectileDataSO.GetRuntimeData() 生成初始副本
    ///   → ActionSpawnProjectile 写入卡牌数值
    ///   → IProjectileModifier 逐个修改
    ///   → ProjectileSpawner 生成实体
    /// </summary>
    public struct ProjectileRuntimeData
    {
        // ══════════════════════════════════════════════════════════
        //  一、行为剩余次数
        //      三个计数互相独立、互不消耗。
        //      哪个 > 0 就代表启用了哪个行为；判定优先级：反弹 → 分裂 → 穿透。
        // ══════════════════════════════════════════════════════════
        public int bounceCount;
        public int pierceCount;
        public int splitCount;

        /// <summary>
        /// 初始分裂次数。
        /// 用途：判定"分裂次数耗尽后，无论还剩多少穿透次数都直接消失"。
        /// </summary>
        public int maxSplitCount;

        // ══════════════════════════════════════════════════════════
        //  二、伤害
        // ══════════════════════════════════════════════════════════
        public int baseDamage;
        public float damageMultiplier;
        public ElementType damageElement;

        // ══════════════════════════════════════════════════════════
        //  三、暴击
        // ══════════════════════════════════════════════════════════

        /// <summary>强制禁止暴击（分裂产生的子投射物置 true）</summary>
        public bool isDisableCrit;

        /// <summary>本次投射物携带的暴击率点数（生成时已把角色与卡牌相加）</summary>
        public int critRatePoint;

        /// <summary>本次投射物携带的暴击伤害点数（生成时已把角色与卡牌相加）</summary>
        public int critDamagePoint;

        // ══════════════════════════════════════════════════════════
        //  四、击退（命中时触发）
        // ══════════════════════════════════════════════════════════

        /// <summary>击退触发概率（0~1 的小数，不是百分点）</summary>
        public float knockbackChance;

        /// <summary>击退格数（0 = 不击退）</summary>
        public int knockbackGrids;

        // ══════════════════════════════════════════════════════════
        //  五、视觉与移动
        // ══════════════════════════════════════════════════════════

        /// <summary>视觉缩放（分裂子投射物会改小，见 CombatConst.ProjectileSplitScale）</summary>
        public float visualScale;

        /// <summary>网格移动速度（格/秒），驱动投射物 Tick</summary>
        public float moveSpeed;

        // ══════════════════════════════════════════════════════════
        //  六、命中时引用的资源
        // ══════════════════════════════════════════════════════════
        public BuffDataSO applyBuffOnHit;
        public GameObject hitVFX;

        // ══════════════════════════════════════════════════════════
        //  七、数值修改工具（强制统一入口，禁止直接 += 字段）
        // ══════════════════════════════════════════════════════════

        public void AddBounce(int value)
        {
            bounceCount += value;
            if (bounceCount < 0) bounceCount = 0;
        }

        public void AddPierce(int value)
        {
            pierceCount += value;
            if (pierceCount < 0) pierceCount = 0;
        }

        public void AddSplit(int value)
        {
            splitCount += value;
            if (splitCount < 0) splitCount = 0;
        }
    }
}