using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单位对外暴露的上下文接口。
    ///
    /// Buff 系统、阵营行为、装备效果都【只依赖这个接口】，不认识 UnitBase。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么必须存在这个接口】—— 它是整个架构的承重墙
    /// ══════════════════════════════════════════════════════════════
    ///  如果没有它，Buff 系统会形成一个依赖环：
    ///
    ///      BuffBase ──▶ UnitBase           （Buff 要访问宿主属性）
    ///      UnitBase ──▶ BuffManager        （单位持有自己的 Buff 管理器）
    ///      BuffManager ──▶ BuffBase        （管理器要创建 / 遍历实例）
    ///      → 环！三个文件必须同时写完才能编译
    ///
    ///  引入接口后：
    ///
    ///      BuffBase    ──▶ IUnitContext ◀── UnitBase
    ///      UnitBase    ──▶ BuffManager ──▶ IUnitContext
    ///      → 依赖方向全部单向，每个文件都能单独编写、单独编译
    ///
    ///  同一个手法也用在阵营系统上：
    ///      FactionBehaviourBase ──▶ IUnitContext ◀── UnitBase
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public interface IUnitContext
    {
        // ══════════════════════════════════════════════════════════
        //  一、基础查询
        // ══════════════════════════════════════════════════════════

        /// <summary>是否存活。死亡单位不接受任何 Buff / 伤害 / 治疗</summary>
        bool IsAlive { get; }

        /// <summary>最大生命值（中毒百分比伤害、破冰百分比伤害都要用）</summary>
        int MaxHp { get; }

        /// <summary>当前生命值</summary>
        int CurrentHp { get; }

        /// <summary>防御（减免物理伤害，有效上限 230 点）</summary>
        int Defense { get; }

        /// <summary>元素抗性（减免元素伤害，有效上限 230 点）</summary>
        int ElementResist { get; }

        /// <summary>延迟伤害抗性（减免灼烧 / 中毒 / 破冰，有效上限 20 点）</summary>
        int DelayDamageResist { get; }

        /// <summary>
        /// 控制抗性点数（0~125，每点减免 0.8 个百分点的眩晕概率）。
        /// 【可读写】——因为眩晕结束时 BuffStun 会给宿主 +50 点。
        /// 实现方应在 setter 内做 clamp(ControlResistMin, ControlResistMax)。
        /// </summary>
        int ControlResistPoint { get; set; }

        // ══════════════════════════════════════════════════════════
        //  二、状态查询
        // ══════════════════════════════════════════════════════════

        /// <summary>是否处于冰冻状态。冰冻期间免疫眩晕与击退</summary>
        bool IsFrozen { get; }

        /// <summary>是否带有指定类型的 Buff（破冰判定、互斥检查都要用）</summary>
        bool HasBuff(BuffType type);

        /// <summary>
        /// 当前总护盾值（所有 Shield Buff 实例之和）。
        ///
        /// 【只读】—— 护盾由 Buff 管理，不能直接赋值。
        ///   加盾：施加 BuffType.Shield 的 Buff
        ///   扣盾：受击时由 TakeDamage 内部扣减最早到期的实例
        ///
        /// 受击顺序：先扣护盾，再扣血。
        /// </summary>
        int TotalShield { get; }

        // ══════════════════════════════════════════════════════════
        //  三、受伤接口
        // ══════════════════════════════════════════════════════════

        /// <summary>受到固定点数伤害（灼烧、直伤都走这里）</summary>
        void TakeDamage(int damage, ElementType element);

        /// <summary>受到最大生命百分比伤害（中毒专用，percent = 0.1 表示 10%）</summary>
        void TakePercentDamage(float percent, ElementType element);

        // ══════════════════════════════════════════════════════════
        //  四、状态设置
        // ══════════════════════════════════════════════════════════

        /// <summary>设置眩晕标记。冰冻期间无法获得眩晕，由实现方处理</summary>
        void SetStun(bool value);

        /// <summary>设置冰冻标记。会同时清除已有的眩晕标记</summary>
        void SetFreeze(bool value);

        // ══════════════════════════════════════════════════════════
        //  五、卡牌格挡（防御卡机制）
        // ══════════════════════════════════════════════════════════

        /// <summary>剩余的「挡伤害」次数。受到攻击时由结算方扣减；为 0 时不生效</summary>
        int BlockDamageCount { get; set; }

        /// <summary>剩余的「挡控制」次数。受到击退 / 眩晕时由结算方扣减</summary>
        int BlockControlCount { get; set; }
    }
}