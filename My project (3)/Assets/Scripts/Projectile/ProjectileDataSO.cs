using GameCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 投射物静态配置。一个资产 = 一种投射物模板。
    ///
    /// ⚠️ SO 只读：运行时绝不修改这里的字段。
    ///    所有动态数据都写进 ProjectileRuntimeData 副本。
    ///
    /// 存放位置：Assets/ScriptableObjects/ProjectileDatas/
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileData", menuName = "GameCore/Projectile/ProjectileDataSO")]
    public class ProjectileDataSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、基础信息
        // ══════════════════════════════════════════════════════════
        [Header("基础信息")]
        [Tooltip("投射物名称（调试用）")]
        public string projectileName;

        // ══════════════════════════════════════════════════════════
        //  二、行为次数
        //      0 = 不启用该行为；判定优先级：反弹 → 分裂 → 穿透。
        // ══════════════════════════════════════════════════════════
        [Header("行为次数（0 = 不启用；判定优先级：反弹 → 分裂 → 穿透）")]
        [Tooltip("反弹次数")]
        [Min(0)] public int bounceCountBase;

        [Tooltip("穿透次数")]
        [Min(0)] public int pierceCountBase;

        [Tooltip("分裂次数。⚠️ 分裂次数耗尽后投射物直接消失，不再走穿透")]
        [Min(0)] public int splitCountBase;

        // ══════════════════════════════════════════════════════════
        //  三、网格移动
        // ══════════════════════════════════════════════════════════
        [Header("网格移动")]
        [Tooltip("每格移动速度（格/秒）")]
        [Min(0.1f)] public float moveSpeed = 1f;

        // ══════════════════════════════════════════════════════════
        //  四、伤害与元素
        // ══════════════════════════════════════════════════════════
        [Header("伤害与元素")]
        [Tooltip("基础伤害。⚠️ 会被卡牌数值覆盖，此处仅作模板默认值")]
        [Min(0)] public int baseDamage;

        [Tooltip("伤害元素。决定走目标的 Defense 还是 ElementResist")]
        public ElementType damageElement;

        // ══════════════════════════════════════════════════════════
        //  五、命中附带
        // ══════════════════════════════════════════════════════════
        [Header("命中附带（可选）")]
        [Tooltip("命中时施加的 Buff")]
        public BuffDataSO applyBuffOnHit;

        [Tooltip("命中特效预制体")]
        public GameObject hitVFX;

        // ══════════════════════════════════════════════════════════
        //  六、模板自带击退（可选）
        //      填 0 表示由卡牌决定；填了则本投射物自带该击退
        // ══════════════════════════════════════════════════════════
        [Header("模板自带击退（0 = 由卡牌决定）")]
        [Tooltip("击退触发概率（0~1 的小数，不是百分点）")]
        [Range(0f, 1f)] public float knockbackChance;

        [Tooltip("击退格数（0 = 不击退）")]
        [Min(0)] public int knockbackGrids;

        // ══════════════════════════════════════════════════════════
        //  七、生成运行时副本
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 生成运行时副本，SO 本身保持只读。
        ///
        /// ⚠️ 本方法只填"模板自带的默认值"。
        ///    卡牌相关数值（伤害倍率 / 暴击点数 / 击退）由
        ///    ActionSpawnProjectile 拿到副本后写入。
        /// </summary>
        public ProjectileRuntimeData GetRuntimeData()
        {
            ProjectileRuntimeData runtime = new ProjectileRuntimeData();

            // 行为次数
            runtime.bounceCount = bounceCountBase;
            runtime.pierceCount = pierceCountBase;
            runtime.splitCount = splitCountBase;
            runtime.maxSplitCount = splitCountBase;   // 记录初值，用于判定"分尽即消失"

            // 伤害
            runtime.baseDamage = baseDamage;
            runtime.damageMultiplier = 1f;
            runtime.damageElement = damageElement;

            // 暴击（默认关闭，由卡牌 + 角色写入）
            runtime.isDisableCrit = false;
            runtime.critRatePoint = 0;
            runtime.critDamagePoint = 0;

            // 击退（模板自带值，可能被卡牌覆盖）
            runtime.knockbackChance = knockbackChance;
            runtime.knockbackGrids = knockbackGrids;

            // 视觉与移动
            runtime.visualScale = 1f;
            runtime.moveSpeed = moveSpeed;

            // 命中资源
            runtime.applyBuffOnHit = applyBuffOnHit;
            runtime.hitVFX = hitVFX;

            return runtime;
        }
    }
}