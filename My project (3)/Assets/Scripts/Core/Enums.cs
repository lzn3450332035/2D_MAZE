using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    // ══════════════════════════════════════════════════════════════
    //  一、元素与伤害
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 伤害元素类型。
    /// 物理伤害走目标的 Defense，其余元素伤害走目标的 ElementResist，
    /// 两者共用同一套分段减伤公式（见 CombatConst）。
    /// </summary>
    public enum ElementType
    {
        Physical = 0,
        Lightning = 1,
        Fire = 2,
        Poison = 3,
        Ice = 4
    }

    // ══════════════════════════════════════════════════════════════
    //  二、卡牌基础
    // ══════════════════════════════════════════════════════════════

    /// <summary>卡牌稀有度。决定可升级项数量（1/2/3）与等级上限（5/7/10）。</summary>
    public enum CardRarity
    {
        Common = 0,   // 普通
        Epic = 1,   // 史诗
        Legendary = 2    // 传说
    }

    /// <summary>
    /// 卡牌类别（纯 UI 分类）。
    /// ⚠️ 由卡牌的 actions 自动推导，不要在 Inspector 里手填。
    /// </summary>
    public enum CardType
    {
        Attack = 0,   // 攻击卡：只有伤害类动作
        Status = 1,   // 状态卡：只有辅助类动作（Buff / 回血 / 抽牌 / 驱散）
        Defense = 2,   // 防御卡：只有防御类动作
        Mix = 3    // 混合卡：占 2 类及以上
    }

    // ══════════════════════════════════════════════════════════════
    //  三、卡牌动作
    // ══════════════════════════════════════════════════════════════

    /// <summary>动作类型。决定 CardActionData 里哪些字段有意义。</summary>
    public enum CardActionType
    {
        Damage = 0,   // 直接伤害
        Projectile = 1,   // 发射投射物
        ChainLightning = 2,   // 闪电连锁
        Move = 3,   // 移动
        BlockDamage = 4,   // 挡伤害
        BlockControl = 5,   // 挡控制
        Shield = 6,   // 加护盾（施加 Shield Buff）
        ApplyBuff = 7,   // 施加 Buff
        Heal = 8,   // 回血
        Draw = 9,   // 抽牌
        Purge = 10   // 驱散
    }

    /// <summary>动作的功能大类。仅用于卡框 / 卡牌类别推导。</summary>
    public enum CardActionFunction
    {
        Damage = 0,
        Move = 1,
        Defense = 2,
        Support = 3
    }

    // ══════════════════════════════════════════════════════════════
    //  四、目标选择（两个正交维度）
    // ══════════════════════════════════════════════════════════════

    /// <summary>维度①：打谁。</summary>
    public enum TargetFaction
    {
        Enemy = 0,   // 敌军
        Ally = 1,   // 友方（不含自己）
        Self = 2,   // 自己
        All = 3    // 全体（不分敌我）
    }

    /// <summary>维度②：怎么选。</summary>
    public enum TargetSelectMode
    {
        None = 0,   // 不需要目标（投射物 / 移动 / 自身 Buff）
        ManualSingle = 1,   // 鼠标手动选 1 个（同阵营可合并）
        ManualMulti = 2,   // 鼠标手动选 N 个（每个动作各选一次）
        NearestN = 3,   // 自动取最近 N 个
        Area = 4    // 范围形状内的全部（自动）
    }

    // ══════════════════════════════════════════════════════════════
    //  五、范围形状（仅 TargetSelectMode.Area 生效）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 范围形状。均以【施法者朝向】为基准向前延伸。
    ///   Line      = 沿方向逐格延伸 aoeSize 格
    ///   Cone      = 第 i 行宽 2i-1，共 aoeSize 行（三角形，尖端在施法者）
    ///   Rectangle = 以"前方第 1 格"为中心，(2*aoeSize+1)²
    /// </summary>
    public enum AoEShape
    {
        None = 0,
        Line = 1,
        Cone = 2,
        Rectangle = 3
    }

    /// <summary>范围的延伸方向（相对角色朝向）。</summary>
    public enum AoEDirection
    {
        Forward = 0,   // 正前方
        ForwardRight = 1,   // 右斜向前（纯对角线）
        ForwardLeft = 2    // 左斜向前（纯对角线）
    }

    // ══════════════════════════════════════════════════════════════
    //  六、卡框
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 卡框风格。8 种 × 3 稀有度 = 24 张。
    /// ⚠️ 由动作自动推导（元素优先于功能；物理 = 无属性层）。
    /// </summary>
    public enum CardFrameStyle
    {
        PhysicalAttack = 0,   // 物理（全部动作无真元素）
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Poison = 4,
        Mixed = 5,   // 混合（多元素 / 多功能）
        Move = 6,
        Defense = 7
    }

    // ══════════════════════════════════════════════════════════════
    //  七、卡牌升级
    // ══════════════════════════════════════════════════════════════

    /// <summary>卡牌可升级属性。共 24 项；只能选卡牌现有动作支持的项。</summary>
    public enum CardUpgradeTarget
    {
        // ── 费用（卡牌级）──
        EnergyCost = 0,
        StaminaCost = 1,

        // ── 伤害 ──
        DamagePercent = 2,
        CritRatePoint = 3,
        CritDamagePoint = 4,

        // ── 击退 ──
        KnockbackGrids = 5,
        KnockbackChance = 6,

        // ── 眩晕 ──
        StunCoeff = 7,

        // ── 投射物 ──
        ProjectileCount = 8,
        BounceCount = 9,
        PierceCount = 10,
        SplitCount = 11,

        // ── 连锁 ──
        ChainCount = 12,
        ChainRange = 13,

        // ── 状态 ──
        BuffStacks = 14,
        BuffApplyChance = 15,
        BuffDuration = 16,
        HealPercent = 17,
        DrawCount = 18,   // 同时是"无数值可升"卡牌的保底升级项

        // ── 防御 ──
        BlockDamageCount = 19,
        BlockControlCount = 20,
        ShieldAmount = 21,

        // ── 位移 ──
        MoveGrids = 22,

        // ── 范围 ──
        AoESize = 23
    }

    // ══════════════════════════════════════════════════════════════
    //  八、Buff
    // ══════════════════════════════════════════════════════════════

    /// <summary>Buff 种类，与 BuffDataSO.buffType 一一对应。</summary>
    public enum BuffType
    {
        Stun = 0,   // 眩晕：固定 1 回合不能行动
        Burn = 1,   // 灼烧：多实例，每实例固定点数伤害 − 延迟抗性
        Poison = 2,   // 中毒：最大生命百分比 − 延迟抗性
        Freeze = 3,   // 冰冻：不能行动，免疫眩晕与击退
        ControlResist = 4,   // 控制抗性加成
        Invincible = 5,   // 无敌：复活后施加，免疫一切伤害
        Shield = 6    // 护盾：多实例，每实例独立倒计时，受击优先扣
    }

    /// <summary>Buff 的移除规则。</summary>
    public enum BuffRemoveRule
    {
        TurnDuration = 0,   // 回合倒计时，到 0 自动移除
        Permanent = 1    // 永久，只能被驱散
    }

    // ══════════════════════════════════════════════════════════════
    //  九、阵营
    // ══════════════════════════════════════════════════════════════

    /// <summary>阵营关系（对称：A 视 B 为 Ally，则 B 也视 A 为 Ally）。</summary>
    public enum FactionRelation
    {
        /// <summary>友军：可以使用友方技能，不可被选为敌方目标</summary>
        Ally = 0,
        /// <summary>合作：不可使用友方技能，也不可被选为敌方目标（但会自行攻击敌军）</summary>
        Cooperative = 1,
        /// <summary>敌军：可被选为敌方目标</summary>
        Enemy = 2
    }

    // ══════════════════════════════════════════════════════════════
    //  十、投射物
    // ══════════════════════════════════════════════════════════════

    /// <summary>投射物运行状态。</summary>
    public enum ProjectileState
    {
        Moving = 0,
        Destroyed = 1
    }

    // 注：反弹 / 穿透 / 分裂 三种行为由 ProjectileRuntimeData 的三个计数表达：
    //     bounceCount / pierceCount / splitCount，哪个 > 0 就启用了哪个。

    // ══════════════════════════════════════════════════════════════
    //  十一、网格与障碍物
    // ══════════════════════════════════════════════════════════════

    /// <summary>单位体型。枚举值 = 占用边长（可直接当数字用）。</summary>
    public enum UnitSize
    {
        Small = 1,   // 1×1
        Elite = 2,   // 2×2
        Boss = 3    // 3×3
    }

    /// <summary>障碍物大类。</summary>
    public enum ObstacleType
    {
        Wall = 0,   // 墙：绝对不可破坏
        Breakable = 1,   // 可破坏：有血量
        Cover = 2,   // 掩体：有朝向，正面挡投射物
        Trap = 3,   // 陷阱：不阻挡移动，踩上触发
        FieldSolid = 4,   // 实体功能障碍物：阻挡移动
        FieldVirtual = 5    // 虚体功能障碍物：不阻挡移动
    }

    /// <summary>可破坏障碍物的破坏层级。</summary>
    public enum BreakableTier
    {
        PhysicalOrMagic = 0,   // L1 物理或法术都能破
        MagicOnly = 1,   // L2 仅法术
        PhysicalOnly = 2,   // L3 仅物理
        BurnOnly = 3,   // L4 仅灼烧
        IceOnly = 4,   // L5 仅寒冰
        EliteOrBossOnly = 5,   // L6 仅精英或 BOSS
        BossOnly = 6    // L7 仅 BOSS
    }

    /// <summary>
    /// 四方向。单位朝向与掩体朝向共用。
    /// ⚠️ Up=(0,+1)、Down=(0,-1)、Left=(-1,0)、Right=(+1,0)
    /// </summary>
    public enum GridDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    // ══════════════════════════════════════════════════════════════
    //  十二、战斗结果
    // ══════════════════════════════════════════════════════════════

    /// <summary>战斗结束方式。</summary>
    public enum BattleOutcome
    {
        None = 0,   // 进行中
        VictoryAnnihilate = 1,   // 胜利：消灭全部敌人（奖励更优）
        VictoryEscape = 2,   // 胜利：走到出口（奖励较少）
        Defeat = 3    // 失败：主角死亡且复活次数耗尽
    }

    // ══════════════════════════════════════════════════════════════
    //  十三、通用结果
    // ══════════════════════════════════════════════════════════════

    /// <summary>效果施加结果。</summary>
    public enum EffectApplyResult
    {
        Success = 0,
        Fail = 1
    }
}