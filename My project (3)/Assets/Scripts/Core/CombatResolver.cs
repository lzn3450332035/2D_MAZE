using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GameCore
{
    /// <summary>
    /// 战斗结算中心：所有伤害/效果统一从这里结算
    /// 当前实现：投射物命中结算（伤害→防御减免→暴击→扣血→上buff→特效）
    /// </summary>
    public static  class CombatResolver
    {
        public static void ApplyHit(ProjectileRuntimeData runtime,UnitBase target, Vector3 hitPos)
        {
            if (target == null || !target.isAlive) return;
            // 1. 基础伤害 × 倍率
            float raw = runtime.baseDamage * runtime.damageMultiplier;
            // 2. 防御分段减免（复用现有 MathHelper + CombatConst 分段公式）
            //float reduceMultiplier = MathHelper.CalcSegmentDamageMultiplier(target.baseStat.Defense);
            //int finalDamage = Mathf.Max(1, Mathf.RoundToInt(raw * reduceMultiplier));
            // 3. 暴击：子投射物 isDisableCrit=true 强制关闭；暴击率暂用目标自身CritRate
            //    后续应改为施法者暴击率（需要传入caster），此处留作适配占位
            //if (!runtime.isDisableCrit )
            //{
              //  float critRate = target.baseStat.CritRate;
                //if (UnityEngine.Random.value < critRate )
                //{
                  //  finalDamage = Mathf.RoundToInt(finalDamage * 1.5f);

                //}
            //}
            // 4. 扣血
            //target.TakeDamage(finalDamage);
            // 5. 命中上buff（Buff完整系统未建，走 UnitBase.ApplyBuff 占位入口）
            if (runtime.applyBuffOnHit != null )
            {
                

            }
            // 6. 命中特效
            if (runtime .hitVFX != null )
            {
               
            }

        }
    }
}