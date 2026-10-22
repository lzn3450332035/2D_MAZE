using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    // ══════════════════════════════════════════════════════════════
    //  辅助数据结构（与 DamageCalculator 放同一文件，减少文件数量）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 目标的战斗数据快照。
    ///
    /// 用途：让 DamageCalculator 的所有函数都【收快照、不碰 UnitBase】，
    ///       从而保持 L2 计算层的纯净（不反向依赖 L4 实体层）。
    ///
    /// 调用方在 L4+ 用 DamageCalculator.SnapshotFrom(unitContext) 生成。
    /// </summary>
    public struct TargetSnapshot
    {
        /// <summary>是否存活</summary>
        public bool isAlive;
        /// <summary>是否处于冰冻状态（冰冻免疫眩晕与击退）</summary>
        public bool isFrozen;
        /// <summary>最大生命值（百分比伤害的基数）</summary>
        public int maxHp;
        /// <summary>防御（减免物理伤害）</summary>
        public int defense;
        /// <summary>元素抗性（减免元素伤害）</summary>
        public int elementResist;
        /// <summary>延迟伤害抗性（减免灼烧 / 中毒 / 破冰，上限 20）</summary>
        public int delayDamageResist;
        /// <summary>控制抗性点数（减免眩晕概率，上限 125）</summary>
        public int controlResistPoint;
    }

    /// <summary>
    /// 暴击参数。打包传递，避免两个 int 点数传错位置。
    /// </summary>
    public struct CritParams
    {
        /// <summary>总暴击率点数（角色点数 + 卡牌点数，调用方已相加）</summary>
        public int ratePoint;
        /// <summary>总暴击伤害点数（角色点数 + 卡牌点数，调用方已相加）</summary>
        public int damagePoint;
        /// <summary>本次伤害是否允许暴击（分裂子投射物、canCrit=false 的卡牌都传 false）</summary>
        public bool canCrit;
    }

    /// <summary>
    /// 伤害与效果计算中心。全部是【纯函数】：收数字、吐数字。
    ///
    /// ⚠️ 本类属于 L2 计算层：
    ///    · 严禁访问任何单例（GridManager / BuffManager / BattleManager）
    ///    · 严禁接收 UnitBase —— 需要目标数据时收 TargetSnapshot
    ///    · 需要"格子能否站立"这类判断时，收 Func 委托由调用方注入
    ///
    /// 本类【不负责】：
    ///    · 扣血（由调用方拿返回值去调 TakeDamage）
    ///    · 施加 / 移除 Buff（跨 Buff 联动统一放在 BuffManager.AddBuff）
    ///    · 格挡判定（放在 CombatResolver 的结算流程里）
    /// </summary>
    public static class DamageCalculator
    {
        // ══════════════════════════════════════════════════════════
        //  零、从接口生成快照
        // ══════════════════════════════════════════════════════════

        /// <summary>从 IUnitContext（L1 接口）生成一份目标快照</summary>
        public static TargetSnapshot SnapshotFrom(IUnitContext ctx)
        {
            TargetSnapshot t = new TargetSnapshot();
            if (ctx == null) return t;

            t.isAlive = ctx.IsAlive;
            t.isFrozen = ctx.IsFrozen;
            t.maxHp = ctx.MaxHp;
            t.defense = ctx.Defense;
            t.elementResist = ctx.ElementResist;
            t.delayDamageResist = ctx.DelayDamageResist;
            t.controlResistPoint = ctx.ControlResistPoint;
            return t;
        }

        // ══════════════════════════════════════════════════════════
        //  一、即时伤害 · 分段减伤
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 分段减伤 → 承伤乘子。
        ///
        /// 分段规则（抗性每点提供的减伤百分比逐段递减）：
        ///     0 ~  20 : 1.00%/点
        ///    20 ~  50 : 0.80%/点
        ///    50 ~ 100 : 0.50%/点
        ///   100 ~ 150 : 0.20%/点
        ///   150 ~ 230 : 0.15%/点
        /// 上限 230；结果不低于 CombatConst.MinDamageMultiplier (0.09)
        /// </summary>
        public static float CalcSegmentDamageMultiplier(int resistStat)
        {
            int value = Mathf.Clamp(resistStat, 0, CombatConst.ResistMaxThreshold);
            float totalReduce = 0f;

            // 从最高段往下剥，剥一段就把 value 压到该段上界
            if (value > 150)
            {
                totalReduce += (value - 150) * CombatConst.ResistReduce_150_230;
                value = 150;
            }
            if (value > 100)
            {
                totalReduce += (value - 100) * CombatConst.ResistReduce_100_150;
                value = 100;
            }
            if (value > 50)
            {
                totalReduce += (value - 50) * CombatConst.ResistReduce_50_100;
                value = 50;
            }
            if (value > 20)
            {
                totalReduce += (value - 20) * CombatConst.ResistReduce_20_50;
                value = 20;
            }
            if (value > 0)
            {
                totalReduce += value * CombatConst.ResistReduce_0_20;
            }

            return Mathf.Max(1f - totalReduce, CombatConst.MinDamageMultiplier);
        }

        // ══════════════════════════════════════════════════════════
        //  二、暴击（点数制 + 软上限）
        // ══════════════════════════════════════════════════════════

        /// <summary>总暴击率（百分点），已 clamp 到 [0, 100]</summary>
        public static float CalcTotalCritRatePercent(int totalCritRatePoint)
            => Mathf.Clamp(
                   totalCritRatePoint * CombatConst.CritRatePointToPercent,
                   CombatConst.CritRateMin,
                   CombatConst.CritRateMax);

        /// <summary>
        /// 暴击伤害点数 → 倍率【加成】（含软上限折算）。
        /// 前 CritDamageSoftCapPoint 点全额生效，超出部分按 CritDamageOverCapRatio 折算。
        /// </summary>
        public static float CalcCritBonusMultiplier(int totalCritDamagePoint)
        {
            if (totalCritDamagePoint <= 0) return 0f;

            int full = Mathf.Min(totalCritDamagePoint, CombatConst.CritDamageSoftCapPoint);
            int over = Mathf.Max(0, totalCritDamagePoint - CombatConst.CritDamageSoftCapPoint);

            float effectivePoint = full + over * CombatConst.CritDamageOverCapRatio;
            return effectivePoint * CombatConst.CritDamagePointToMultiplier;
        }

        /// <summary>实际暴击倍率 = 基础 1.5 + 折算后的点数加成</summary>
        public static float CalcTotalCritMultiplier(int totalCritDamagePoint)
            => CombatConst.DefaultCritMultiplier + CalcCritBonusMultiplier(totalCritDamagePoint);

        // ══════════════════════════════════════════════════════════
        //  三、即时伤害入口
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 计算一次即时伤害。
        /// 物理走 target.defense，其余元素走 target.elementResist，共用同一套分段公式。
        /// </summary>
        /// <returns>最终伤害（至少 1 点）</returns>
        public static int CalculateDamage(
            int baseDamage,
            ElementType element,
            TargetSnapshot target,
            CritParams crit,
            out bool isCrit)
        {
            isCrit = false;
            if (baseDamage <= 0) return 0;

            int resistStat = (element == ElementType.Physical) ? target.defense : target.elementResist;
            float multiplier = CalcSegmentDamageMultiplier(resistStat);

            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));

            if (crit.canCrit)
            {
                float critChance = CalcTotalCritRatePercent(crit.ratePoint);

                if (critChance > 0f && UnityEngine.Random.value * 100f < critChance)
                {
                    isCrit = true;
                    damage = Mathf.RoundToInt(damage * CalcTotalCritMultiplier(crit.damagePoint));
                }
            }

            return damage;
        }

        // ══════════════════════════════════════════════════════════
        //  四、眩晕（减法公式）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 眩晕概率% = clamp(眩晕系数 − 控制抗性点 × 0.8 , 0 , 100)
        /// 眩晕系数单位 = 百分点；控制抗性单位 = 点。
        /// </summary>
        public static float CalcStunChancePercent(float stunCoefficient, int controlResistPoint)
        {
            float reduce = Mathf.Max(0, controlResistPoint) * CombatConst.ControlResistConvertPerPoint;
            return Mathf.Clamp(stunCoefficient - reduce, CombatConst.StunChanceMin, CombatConst.StunChanceMax);
        }

        /// <summary>眩晕判定。冰冻期间免疫眩晕</summary>
        public static bool RollStun(float stunCoefficient, TargetSnapshot target)
        {
            if (!target.isAlive || target.isFrozen) return false;

            float chance = CalcStunChancePercent(stunCoefficient, target.controlResistPoint);
            return chance > 0f && UnityEngine.Random.value * 100f < chance;
        }

        /// <summary>撞墙眩晕判定（眩晕系数固定 100，再扣目标抗性）</summary>
        public static bool RollWallStun(TargetSnapshot target)
            => RollStun(CombatConst.WallStunCoefficient, target);

        // ══════════════════════════════════════════════════════════
        //  五、击退
        // ══════════════════════════════════════════════════════════

        /// <summary>击退是否触发。冰冻期间免疫击退</summary>
        public static bool RollKnockback(float baseChance, TargetSnapshot target)
        {
            if (!target.isAlive || target.isFrozen) return false;
            if (baseChance <= 0f) return false;
            return UnityEngine.Random.value < baseChance;
        }

        /// <summary>击退格数：抗性 &gt; 0.5 时减半并向上取整</summary>
        public static int GetKnockbackGrids(int baseGrids, float knockbackResist)
        {
            if (baseGrids <= 0) return 0;

            if (knockbackResist > CombatConst.KnockbackResistHalveThreshold)
                return MathHelper.KnockbackHalveCeil(baseGrids);

            return baseGrids;
        }

        /// <summary>
        /// 求解击退移动（纯函数）。
        ///
        /// ⚠️ 关键：本方法【不认识 GridManager】。
        ///    "某个格子能否放下这个单位"由调用方通过 canPlace 委托注入 ——
        ///    这样 L2 计算层保持了纯净，同时天然支持多格单位
        ///    （调用方在委托里检查整个 2×2 / 3×3 投影范围即可）。
        /// </summary>
        /// <param name="from">起始锚点格</param>
        /// <param name="dir">击退方向（必须是四方向单位向量）</param>
        /// <param name="grids">请求击退的格数</param>
        /// <param name="canPlace">判断"单位锚点放在该格时，整个投影范围能否站立"</param>
        public static KnockbackResult ResolveKnockbackMove(
            Vector2Int from,
            Vector2Int dir,
            int grids,
            System.Func<Vector2Int, bool> canPlace)
        {
            if (grids <= 0 || canPlace == null || (dir.x == 0 && dir.y == 0))
                return KnockbackResult.Stay(from);

            Vector2Int cur = from;

            for (int i = 0; i < grids; i++)
            {
                Vector2Int next = GridMath.AddGrid(cur, dir);

                if (!canPlace(next))
                {
                    // 撞到障碍：停在障碍前一格，并记录撞击位置
                    KnockbackResult blocked = new KnockbackResult();
                    blocked.hitObstacle = true;
                    blocked.finalGrid = cur;
                    blocked.blockedGrid = next;
                    blocked.movedGrids = i;
                    return blocked;
                }

                cur = next;
            }

            KnockbackResult moved = new KnockbackResult();
            moved.hitObstacle = false;
            moved.finalGrid = cur;
            moved.blockedGrid = cur;
            moved.movedGrids = grids;
            return moved;
        }

        // ══════════════════════════════════════════════════════════
        //  六、延迟伤害（灼烧 / 中毒 / 破冰）
        //      ⚠️ 三者【没有保底】，减免 ≥ 伤害时返回 0
        // ══════════════════════════════════════════════════════════

        /// <summary>延迟抗性统一 clamp 到 [0, DelayResistMax]</summary>
        private static int ClampDelayResist(int value)
            => Mathf.Clamp(value, 0, CombatConst.DelayResistMax);

        /// <summary>
        /// 灼烧伤害（减法 · 单位是"点"）。
        /// 实际 = max(0, 总灼烧点伤 − 抗性 × 1)
        /// </summary>
        public static int CalculateBurnDamage(int totalBurnDamage, TargetSnapshot target)
        {
            if (totalBurnDamage <= 0) return 0;

            int resist = ClampDelayResist(target.delayDamageResist);
            int reduce = Mathf.RoundToInt(resist * CombatConst.BurnResistConvertPerPoint);

            return Mathf.Max(0, totalBurnDamage - reduce);
        }

        /// <summary>
        /// 中毒伤害（减法 · 单位是"百分点"）。
        /// 实际% = max(0, 每层百分比 × 层数 × 100 − 抗性 × 1)
        /// 实际伤害 = round(maxHp × 实际% / 100)
        /// </summary>
        /// <param name="percentPerStack">每层占最大生命的比例（0.05 = 5%）</param>
        public static int CalculatePoisonDamage(float percentPerStack, int stacks, TargetSnapshot target)
        {
            if (percentPerStack <= 0f || stacks <= 0) return 0;

            float poisonPercent = percentPerStack * stacks * 100f;      // 换成"百分点"
            int resist = ClampDelayResist(target.delayDamageResist);
            float reducePercent = resist * CombatConst.PoisonResistConvertPercentPerPoint;

            float actualPercent = Mathf.Max(0f, poisonPercent - reducePercent);
            return Mathf.Max(0, Mathf.RoundToInt(target.maxHp * actualPercent * 0.01f));
        }

        /// <summary>
        /// 破冰伤害（乘法 · 单位是"比例"）。
        /// 实际% = 20% × (1 − 抗性 × 2.5%)，带灼烧时 ×2
        /// </summary>
        public static int CalculateIceBreakDamage(TargetSnapshot target, bool withBurnBuff)
        {
            if (target.maxHp <= 0) return 0;

            int resist = ClampDelayResist(target.delayDamageResist);
            float reduceRatio = Mathf.Clamp01(resist * CombatConst.IceBreakResistReducePerPoint);

            float percent = CombatConst.IceBreakBasePercent * (1f - reduceRatio);
            if (withBurnBuff) percent *= CombatConst.IceBreakBurnTriggerMulti;

            return Mathf.Max(0, Mathf.RoundToInt(target.maxHp * percent));
        }
    }
}