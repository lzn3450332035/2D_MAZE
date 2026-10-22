using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    // ══════════════════════════════════════════════════════════════
    //  动作专属枚举
    // ══════════════════════════════════════════════════════════════

    /// <summary>移动的相对方向（相对角色当前朝向）。</summary>
    public enum RelativeDir
    {
        Forward = 0,   // 沿朝向
        Back = 1,   // 朝向反方向
        Left = 2,   // 朝向左侧
        Right = 3    // 朝向右侧
    }

    /// <summary>驱散的目标类型。</summary>
    public enum PurgeTarget
    {
        DebuffOnTarget = 0,   // 驱散负面 Buff
        BuffOnTarget = 1,   // 驱散正面 Buff
        AllOnTarget = 2    // 驱散全部 Buff
    }

    // ══════════════════════════════════════════════════════════════
    //  一个动作的完整数据
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 一个动作的完整配置。⚠️ 内嵌在 CardDataSO 里，每张卡各一份，数值互相独立。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么 11 种动作共用这一个类】
    /// ══════════════════════════════════════════════════════════════
    ///   · 每张卡要能自由添加动作、数值各不相同 → 不能做成共享资产
    ///   · 动作的执行逻辑统一放在 CardActionExecutor（批次 7），
    ///     所以这里只承担"数据"，不需要虚方法 / 继承
    ///   · 代价：Inspector 会显示全部 31 个字段，
    ///     但已用 [Header] 分好组，配卡时只看自己那一组即可
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么是 class 而不是 struct】
    /// ══════════════════════════════════════════════════════════════
    ///   · Unity 序列化时，[Serializable] class 在数组里是"每个元素一个独立对象"
    ///   · struct 会出现"值拷贝"问题（改一处可能改不到原处）
    ///   · class 允许 null，便于"动作被删掉"的中间状态
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    [System.Serializable]
    public class CardActionData
    {
        // ══════════════════════════════════════════════════════════
        //  一、类型与属性
        // ══════════════════════════════════════════════════════════
        [Header("═══ ① 类型 ═══")]
        [Tooltip("动作类型。决定下面哪些字段有意义（无关字段会被忽略）")]
        public CardActionType type = CardActionType.Damage;

        [Tooltip("动作属性。物理 = 无属性层；一个动作只能有 1 个属性")]
        public ElementType element = ElementType.Physical;

        // ══════════════════════════════════════════════════════════
        //  二、目标
        // ══════════════════════════════════════════════════════════
        [Header("═══ ② 目标 · 打谁 ═══")]
        public TargetFaction faction = TargetFaction.Enemy;

        [Header("═══ ③ 目标 · 怎么选 ═══")]
        [Tooltip("None = 不需要目标（投射物 / 移动 / 自身 Buff）")]
        public TargetSelectMode selectMode = TargetSelectMode.ManualSingle;

        [Tooltip("ManualMulti / NearestN 时生效；其它模式忽略")]
        [Min(1)] public int selectCount = 1;

        // ══════════════════════════════════════════════════════════
        //  三、范围（仅 selectMode = Area 生效）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ④ 范围（仅 Area 生效）═══")]
        public AoEShape aoeShape = AoEShape.Line;
        public AoEDirection aoeDirection = AoEDirection.Forward;

        [Tooltip("Line = 延伸格数 / Cone = 行数 / Rectangle = 半径 N（实际边长 2N+1）")]
        [Min(1)] public int aoeSize = 1;

        // ══════════════════════════════════════════════════════════
        //  四、伤害（Damage / Projectile / ChainLightning）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑤ 伤害 ═══")]
        [Tooltip("实际伤害 = 施法者 PhysicalAtk × 该倍率")]
        [Min(0f)] public float damagePercent = 1f;

        [Tooltip("是否允许暴击")]
        public bool canCrit = true;

        [Tooltip("暴击率点数（1 点 = 1 个百分点）")]
        [Min(0)] public int critRatePoint;

        [Tooltip("暴击伤害点数（1 点 = 0.05 倍率）")]
        [Min(0)] public int critDamagePoint;

        // ══════════════════════════════════════════════════════════
        //  五、击退 / 眩晕（伤害类）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑥ 击退 ═══")]
        [Tooltip("击退触发概率（0~1 的小数，不是百分点）")]
        [Range(0f, 1f)] public float knockbackChance;

        [Tooltip("击退格数（0 = 不击退）")]
        [Min(0)] public int knockbackGrids;

        [Header("═══ ⑦ 眩晕 ═══")]
        [Tooltip("眩晕系数（百分点 0~100）。实际概率 = 该值 − 目标控制抗性点 × 0.8")]
        [Range(0f, 100f)] public float stunCoeff;

        // ══════════════════════════════════════════════════════════
        //  六、投射物（仅 Projectile）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑧ 投射物（仅 Projectile）═══")]
        [Tooltip("投射物外观模板（视觉 / 特效，不含战斗数值）")]
        public ProjectileDataSO projectileTemplate;

        [Tooltip("一次发射几枚")]
        [Min(1)] public int projectileCount = 1;

        [Tooltip("反弹次数（0 = 不反弹）")]
        [Min(0)] public int bounceCount;

        [Tooltip("穿透次数（0 = 不穿透）")]
        [Min(0)] public int pierceCount;

        [Tooltip("分裂次数（0 = 不分裂）。⚠️ 分裂耗尽后投射物直接消失")]
        [Min(0)] public int splitCount;

        // ══════════════════════════════════════════════════════════
        //  七、连锁（仅 ChainLightning）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑨ 连锁（仅 ChainLightning）═══")]
        [Tooltip("最大连锁次数（跳转次数）")]
        [Min(1)] public int chainCount = 3;

        [Tooltip("跳转范围（曼哈顿距离，格）")]
        [Min(1)] public int chainRange = 3;

        // ══════════════════════════════════════════════════════════
        //  八、Buff（伤害命中附带 / ApplyBuff / Shield）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑩ Buff ═══")]
        [Tooltip("伤害类 = 命中时附带；ApplyBuff 类 = 施加的 Buff")]
        public BuffDataSO applyBuff;

        [Tooltip("施加概率（0~1）")]
        [Range(0f, 1f)] public float applyBuffChance = 1f;

        [Tooltip("施加几层")]
        [Min(1)] public int applyBuffStacks = 1;

        [Tooltip("持续回合覆盖（0 = 用 Buff 资产的值）")]
        [Min(0)] public int buffDurationOverride;

        // ══════════════════════════════════════════════════════════
        //  九、防御（BlockDamage / BlockControl / Shield）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑪ 防御 ═══")]
        [Tooltip("BlockDamage / BlockControl：抵挡几次")]
        [Min(0)] public int blockCount = 1;

        [Tooltip("Shield：施加几层护盾 Buff（每层 = buff 资产的 shieldPerStack 点）")]
        [Min(1)] public int shieldStacks = 1;

        // ══════════════════════════════════════════════════════════
        //  十、移动（仅 Move）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑫ 移动（仅 Move）═══")]
        public RelativeDir moveDirection = RelativeDir.Forward;

        [Tooltip("沿该方向移动几格。⚠️ 每张卡可以各自设不同的值")]
        [Min(0)] public int moveGrids = 1;

        // ══════════════════════════════════════════════════════════
        //  十一、其他（Heal / Draw / Purge）
        // ══════════════════════════════════════════════════════════
        [Header("═══ ⑬ 其他 ═══")]
        [Tooltip("Heal：按最大生命的百分比回复（0.1 = 10%）")]
        [Range(0f, 1f)] public float healPercent = 0.1f;

        [Tooltip("Draw：抽几张")]
        [Min(1)] public int drawCount = 1;

        [Tooltip("Purge：驱散什么")]
        public PurgeTarget purgeTarget = PurgeTarget.DebuffOnTarget;

        // ══════════════════════════════════════════════════════════
        //  便利方法
        // ══════════════════════════════════════════════════════════

        /// <summary>该动作是否属于伤害大类（Damage / Projectile / ChainLightning）</summary>
        public bool IsDamageType
            => type == CardActionType.Damage
            || type == CardActionType.Projectile
            || type == CardActionType.ChainLightning;

        /// <summary>该动作是否是可升级属性（Buff 是否允许升级持续回合）</summary>
        public bool IsBuffDurationUpgradable
            => type == CardActionType.ApplyBuff
            && applyBuff != null
            && applyBuff.removeRule == BuffRemoveRule.TurnDuration;
    }
}