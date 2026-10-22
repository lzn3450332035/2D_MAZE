using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 阵营行为基类。每个阵营一个子类，负责该阵营独有的数值与行为加成。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么用 abstract class 而不是 interface】
    /// ══════════════════════════════════════════════════════════════
    ///   ① 需要继承 ScriptableObject（每个阵营是一个资产，可在 Inspector 配参数）
    ///      → 接口无法继承类，所以只能用抽象类
    ///   ② 所有方法都要有"默认空实现"（子类只覆写关心的那一个）
    ///      → virtual + 默认实现是抽象类的天然能力
    ///
    ///   对照 IProjectileModifier 为什么用接口：
    ///      那个接口只有 1 个方法，且要允许【已有的类】去实现它
    ///      （EquipmentDataSO、BuffEffect 都可能实现）
    ///      → 需要"一个类实现多个能力"，所以用接口
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【依赖边界】
    /// ══════════════════════════════════════════════════════════════
    ///   只依赖 L0（Enums / UnitStat 里的枚举）
    ///        + L1（IUnitContext / UnitStat / ProjectileRuntimeData）
    ///   绝不依赖 UnitBase（L4）—— 所以参数用 IUnitContext，不用 UnitBase
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public abstract class FactionBehaviourBase : ScriptableObject
    {
        /// <summary>
        /// 数值加成。由 FactionManager 在【战斗开始】与【每回合结束】调用。
        ///
        /// 为什么参数里有 aliveMemberCount：
        ///   你定的规则里有"某阵营同时存在 3 人时防御 +5"这类效果。
        ///   人数由 FactionManager 统一统计后传进来，子类不需要自己去查单位。
        /// </summary>
        /// <param name="host">宿主单位（只通过接口访问，不认识 UnitBase）</param>
        /// <param name="aliveMemberCount">该阵营当前存活人数</param>
        /// <param name="stat">当前属性快照</param>
        /// <returns>修改后的属性。返回新值而不是改传入的 stat —— 保持函数式风格，避免副作用</returns>
        public virtual UnitStat ModifyStat(IUnitContext host, int aliveMemberCount, UnitStat stat)
            => stat;

        /// <summary>修改该阵营单位的投射物（在投射物生成前调用）</summary>
        public virtual void ModifyProjectile(IUnitContext host, ref ProjectileRuntimeData data)
        {
        }

        /// <summary>该阵营单位【回合开始】时触发</summary>
        public virtual void OnUnitTurnStart(IUnitContext host, int aliveMemberCount)
        {
        }

        /// <summary>该阵营单位【受到伤害后】触发</summary>
        public virtual void OnUnitDamaged(IUnitContext host, int damage, int aliveMemberCount)
        {
        }

        /// <summary>该阵营单位【死亡时】触发</summary>
        public virtual void OnUnitDied(IUnitContext host)
        {
        }
    }
}