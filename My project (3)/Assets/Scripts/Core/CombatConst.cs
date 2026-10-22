using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 战斗结算常量表。
    /// 只放"全局不变、且不属于任何角色/装备"的规则数值。
    /// ⚠️ 修改本文件任何常数都会影响全部战斗平衡，改前请先读注释里的验算过程。
    ///
    /// 【量纲速查】每个数值的单位都在注释里标明：
    ///   抗性类   → 单位是"点"
    ///   概率类   → 单位是"百分点"(0~100)
    ///   伤害类   → 单位是"点"或"百分点"(视具体类型)
    ///   比例类   → 单位是"倍率"(0.85 = 85%)
    /// </summary>
    public static class CombatConst
    {
        // ════════════════════════════════════════════════════════════
        //  一、即时伤害 · 分段减伤
        //      物理伤害走目标的 Defense，元素伤害走目标的 ElementResist，
        //      两者【共用】同一套分段公式与同一个上限。
        // ════════════════════════════════════════════════════════════
        //  分段规则（抗性每点提供的减伤百分比逐段递减）：
        //     0 ~  20 : 每点 1.00%
        //    20 ~  50 : 每点 0.80%
        //    50 ~ 100 : 每点 0.50%
        //   100 ~ 150 : 每点 0.20%
        //   150 ~ 230 : 每点 0.15%
        //
        //  峰值验算（抗性 = 230）：
        //     20 × 1.00% = 20%
        //     30 × 0.80% = 24%
        //     50 × 0.50% = 25%
        //     50 × 0.20% = 10%
        //     80 × 0.15% = 12%
        //     ─────────────────
        //     合计减伤   = 91%
        //     承伤乘子   = 1 − 0.91 = 0.09   ← 必须与 MinDamageMultiplier 相等
        //
        //  ⚠️ 改动任何一段系数后，都要同步修改 MinDamageMultiplier，
        //     否则乘子会"截断"公式，把某一段的减伤曲线压平。
        //     本文件末尾有编辑器自检会自动报错提醒。
        #region 防御&元素属性抗性分段减伤规则

        /// <summary>抗性 0~20 段：每点减伤 1.00%</summary>
        public const float ResistReduce_0_20 = 0.01f;
        /// <summary>抗性 20~50 段：每点减伤 0.80%</summary>
        public const float ResistReduce_20_50 = 0.008f;
        /// <summary>抗性 50~100 段：每点减伤 0.50%</summary>
        public const float ResistReduce_50_100 = 0.005f;
        /// <summary>抗性 100~150 段：每点减伤 0.20%</summary>
        public const float ResistReduce_100_150 = 0.002f;
        /// <summary>抗性 150~230 段：每点减伤 0.15%</summary>
        public const float ResistReduce_150_230 = 0.0015f;

        /// <summary>抗性（防御 / 元素抗性）的有效上限（点）。超出部分完全不计。</summary>
        public const int ResistMaxThreshold = 230;

        /// <summary>
        /// 最低承伤乘子（= 抗性满 230 时的承伤比例 9%）。
        /// 作用是保证"伤害永不归零"，避免高防单位变成打不死的僵局。
        /// </summary>
        
        public const float MinDamageMultiplier = 0.09f;
        #endregion
        // ════════════════════════════════════════════════════════════
        //  二、眩晕（减法公式）
        //     概率% = clamp( 眩晕系数% − 控制抗性点数 × 转换系数 , 0 , 100 )
        //     眩晕系数单位 = 百分点（0~100，100 = 基础必中）
        //     控制抗性单位 = 点
        // ════════════════════════════════════════════════════════════
        #region 控制抗性（眩晕和击退概率判定）
        /// <summary>眩晕概率的下限（百分点）</summary>
        public const float StunChanceMin = 0f;
        /// <summary>眩晕概率的上限（百分点，100 = 必定命中）</summary>
        public const float StunChanceMax = 100f;
        
        /// <summary>
        /// 每点控制抗性减免多少【百分点】的眩晕概率。
        /// 0.8 → 100 点抗性减免 80 个百分点；125 点抗性完全免疫眩晕。
        /// </summary>
        public const float ControlResistConvertPerPoint = 0.8f;
        /// <summary>
        /// 控制抗性点数上限（= 完全免疫眩晕所需的点数）。
        /// 必须满足：ControlResistMax × ControlResistConvertPerPoint = StunChanceMax
        /// </summary>
        public const int ControlResistMax = 125;

        /// <summary>眩晕固定持续回合数</summary>
        public const int StunDurationTurn = 1;

        /// <summary>眩晕结束后，目标获得的控制抗性点数（+50 点 = 后续减免 40 个百分点）</summary>
        public const int PostStunControlResistAdd = 50;

        /// <summary>撞墙眩晕使用的眩晕系数（100 = 先按满值判定，再扣目标抗性）</summary>
        public const float WallStunCoefficient = 100f;

        #endregion
        // ════════════════════════════════════════════════════════════
        //  三、延迟伤害（灼烧 / 中毒 / 破冰）
        //      三者【共用】同一份"延迟抗性点数"与同一个 20 点上限，
        //      但各自的转换系数与数学模型不同：
        //
        //        灼烧（固定点数伤害）→ 减法：每点抗性减 1 点灼烧伤害
        //        中毒（百分比伤害）  → 减法：每点抗性减 1 个百分点中毒伤害
        //        破冰（百分比爆发）  → 乘法：每点抗性减 2.5% 的破冰伤害
        //
        //      ⚠️ 延迟伤害【没有保底】。减免 ≥ 伤害时，扣 0 血。
        // ════════════════════════════════════════════════════════════
        #region 延迟伤害

        /// <summary>延迟抗性的点数上限（灼烧 / 中毒 / 破冰共用）</summary>

        public const int DelayResistMax = 20;
        /// <summary>
        /// 灼烧：每点延迟抗性减免多少【点】灼烧伤害。
        /// 例：抗性 20 → 减免 20 点灼烧伤害（因此每层灼烧伤害需 &gt; 20 才有意义）。
        /// </summary>
        public const float BurnResistConvertPerPoint = 1f;

        /// <summary>
        /// 中毒：每点延迟抗性减免多少【百分点】的中毒伤害。
        /// 例：中毒 10%、抗性 6 → 实际 4%；抗性 10 即可完全免疫 10% 的中毒。
        /// </summary>
        public const float PoisonResistConvertPercentPerPoint = 1f;

        /// <summary>
        /// 破冰：每点延迟抗性减免的【比例】（乘法模型，与上面两者不同）。
        /// 例：抗性 20 → 减免 50% → 破冰从 20% 降到 10%。
        /// </summary>
        public const float IceBreakResistReducePerPoint = 0.025f;

        /// <summary>破冰基础伤害 = 目标最大生命 × 该比例（20%）</summary>
        public const float IceBreakBasePercent = 0.2f;

        /// <summary>冰冻 + 灼烧触发破冰时的伤害倍数</summary>
        public const float IceBreakBurnTriggerMulti = 2f;
        #endregion

        // ════════════════════════════════════════════════════════════
        //  四、击退
        // ════════════════════════════════════════════════════════════
        #region 击退规则
        /// <summary>击退抗性超过该比例时，击退格数减半（向上取整）</summary>
        public const float KnockbackResistHalveThreshold = 0.5f;
        #endregion
        // ════════════════════════════════════════════════════════════
        //  五、闪电连锁
        // ════════════════════════════════════════════════════════════

        #region 闪电连锁
        /// <summary>连锁跳转的最大曼哈顿距离，超过即终止连锁</summary>
        public const int LightningChainMaxManhattanDistance = 3;
        #endregion
        // ════════════════════════════════════════════════════════════
        //  六、投射物
        // ════════════════════════════════════════════════════════════

        #region 投射物
        /// <summary>分裂产生的子投射物体积倍率</summary>
        public const float ProjectileSplitScale = 0.85f;

        /// <summary>投射物最大存活步数（安全阀，防止边界反复反弹导致永不消失）</summary>
        public const int ProjectileMaxSteps = 200;
        #endregion
        // ════════════════════════════════════════════════════════════
        //  七、暴击
        //      【设计意图】玩家侧一律用"点数"显示，战斗侧才转成实际倍率，
        //                 让升级加点的成长感更平滑可观。
        //
        //      暴击率（加法）：
        //        实际暴击率% = clamp(角色点数 × 转化系数 + 卡牌点数 × 转化系数, 0, 100)
        //
        //      暴击倍率（加法 + 软上限）：
        //        1. 总点数 = 角色点数 + 卡牌点数
        //        2. 前 CritDamageSoftCapPoint 点全额生效，
        //           超出部分按 CritDamageOverCapRatio 折算
        //        3. 实际倍率 = DefaultCritMultiplier + 折算后点数 × CritDamagePointToMultiplier
        //
        //      数据来源：
        //        角色 → UnitStat.CritRatePoint / CritDamagePoint
        //        卡牌 → CardDataSO.critRatePoint / critDamagePoint
        // ════════════════════════════════════════════════════════════
        #region 暴击

        /// <summary>暴击率下限（百分点）</summary>
        public const float CritRateMin = 0f;

        /// <summary>暴击率上限（百分点）。总和超过此值即必定暴击。</summary>
        public const float CritRateMax = 100f;

        /// <summary>暴击率：1 点 = 多少个百分点（当前 1 点 = 1%，保留常量以便将来调成更细的粒度）</summary>
        public const float CritRatePointToPercent = 1f;

        /// <summary>暴击伤害：1 点 = 多少暴击倍率（1 点 = 0.05 倍）</summary>
        public const float CritDamagePointToMultiplier = 0.05f;

        /// <summary>
        /// 暴击伤害点数的【软上限】（点）。
        /// 该值以内全额生效；超出的部分按 CritDamageOverCapRatio 折算。
        /// 建议值 30 → 对应 +1.5 倍率 → 叠上基础 1.5 后为 3.0 倍。
        /// </summary>
        public const int CritDamageSoftCapPoint = 30;

        /// <summary>超出软上限后的生效比例（0.5 = 只算一半）</summary>
        public const float CritDamageOverCapRatio = 0.5f;

        /// <summary>
        /// 暴击伤害倍率的【基础值】。
        /// ⚠️ 这不是"可被替换的默认值"，而是【永远参与计算的基础项】：
        ///     实际倍率 = 该值 + 角色加成 + 卡牌加成
        /// </summary>
        public const float DefaultCritMultiplier = 1.5f;

        #endregion

        // ════════════════════════════════════════════════════════════
        //  八、编辑器自检
        //      把"公式与常量必须一致"这条隐式约定变成显式报错。
        //      这类"改了 A 忘了改 B"的错误，如果不自检，
        //      往往会在几周后才发现伤害算错，且极难定位。
        // ════════════════════════════════════════════════════════════
#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelfCheck()
        {
            // ── 自检 1：分段减伤之和 是否等于 (1 − 最低承伤乘子) ──
            float reduceSum =
                20f * ResistReduce_0_20 +
                (50f - 20f) * ResistReduce_20_50 +
                (100f - 50f) * ResistReduce_50_100 +
                (150f - 100f) * ResistReduce_100_150 +
                (ResistMaxThreshold - 150f) * ResistReduce_150_230;

            float expectedMultiplier = 1f - reduceSum;

            if (UnityEngine.Mathf.Abs(expectedMultiplier - MinDamageMultiplier) > 0.0001f)
            {
                UnityEngine.Debug.LogError(
                    $"[CombatConst] 分段减伤与最低承伤乘子不一致！" +
                    $"分段公式算出 {expectedMultiplier:F4}，但 MinDamageMultiplier 是 {MinDamageMultiplier:F4}。" +
                    $"改动分段系数后必须同步修改 MinDamageMultiplier。");
            }

            // ── 自检 2：控制抗性上限 × 转换系数 是否等于 100 百分点 ──
            float fullImmunePercent = ControlResistMax * ControlResistConvertPerPoint;

            if (UnityEngine.Mathf.Abs(fullImmunePercent - StunChanceMax) > 0.0001f)
            {
                UnityEngine.Debug.LogError(
                    $"[CombatConst] 控制抗性无法完全免疫眩晕！" +
                    $"ControlResistMax × ControlResistConvertPerPoint = {fullImmunePercent:F2}，" +
                    $"但眩晕概率上限是 {StunChanceMax:F2}。" +
                    $"两者必须相等，否则眩晕永远无法被 100% 免疫。");
            }
        }
#endif

    }
}