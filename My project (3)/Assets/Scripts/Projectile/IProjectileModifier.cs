using System.Collections;
using System.Collections.Generic;
namespace GameCore
{
    /// <summary>
    /// 投射物修饰器接口。
    ///
    /// 实现者：装备效果、Buff 效果、阵营行为、角色特性
    /// 作用：在投射物实体生成之前，修改它携带的运行时数据
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么参数必须用 ref】
    /// ══════════════════════════════════════════════════════════════
    ///   ProjectileRuntimeData 是 struct（值类型）。
    ///   不加 ref → 方法收到的是【副本】→ 在里面怎么改都不影响调用方的原件。
    ///   加了 ref → 方法收到的是【原件的引用】→ 修改会传回调用方。
    ///
    ///   三种情况对照：
    ///     ① 在 struct 变量上直接调方法（data.AddBounce(-1)）
    ///          → 能改自己 ✅
    ///     ② 把 struct 当参数传给别的方法
    ///          → 改的是副本，原件不变 ❌
    ///     ③ 参数声明了 ref / out
    ///          → 能改原件 ✅
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【完整调用链】
    /// ══════════════════════════════════════════════════════════════
    ///   ProjectileDataSO.GetRuntimeData()            得到初始副本
    ///   → ActionSpawnProjectile 写入卡牌数值（伤害 / 暴击 / 击退）
    ///   → 遍历 caster.GetAllProjectileModifiers()
    ///       逐个调用 ModifyProjectile(ref runtimeData)
    ///   → ProjectileSpawner.SpawnMainProjectile()    生成实体
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public interface IProjectileModifier
    {
        /// <summary>
        /// 修改投射物的运行时数据。
        /// ⚠️ 参数必须用 ref，否则对本参数的修改不会传回调用方。
        /// </summary>
        void ModifyProjectile(ref ProjectileRuntimeData runtimeData);
    }
}