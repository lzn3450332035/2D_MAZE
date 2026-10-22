using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    public class UnitBase : MonoBehaviour
    {
        [Header("基础属性")]
        public UnitStat baseStat;
        [Header("组件引用")]
        public UnitAction unitAction;
        [Header("战斗状态")]
        public bool isAlive = true;
        public Vector2Int gridPos;
        //Buff运行实例容器，由BuffManager管理
        //public List<BuffInstance> buffInstances = new List<BuffInstance>() ;
        /// <summary>
        /// 获取【最终属性】=基础属性 + Buff带来的属性增量
        /// 后续Buff系统完成后，在这里叠加buff属性加成
        /// </summary>
        public UnitStat GetFinalStat()
        {
            //当前阶段：暂时直接返回基础属性；后续Buff增量写好后在这里叠加
            UnitStat final = baseStat;
            //TODO：遍历buffInstances，叠加所有buff提供的属性增量
            //例：final.ElementResist += buff.elementResistAdd;
            return final;
        }
        /// <summary>
        /// 单位受到伤害，修改当前血量，血量<=0标记死亡
        /// </summary>
        /// <param name="damage">扣血数值</param>
        public void TakeDamage(int damage)
        {
            if (!isAlive) return;
            baseStat.CurrentHp -= damage;
            if (baseStat.CurrentHp <= 0)
            {
                baseStat.CurrentHp = 0;
                Die();
            }
        }
        /// <summary>
        /// 单位死亡逻辑
        /// </summary>
        public void Die()
        {
            isAlive = false;
            //TODO：死亡事件回调，通知BattleManager、BuffManager清除这个单位的buff
        }
        /// <summary>
        /// 回血，不能超过最大生命值
        /// </summary>
        /// <param name="heal">治疗量</param>
        public void Heal(int heal)
        {
            if (!isAlive) return;
            baseStat.CurrentHp = Mathf.Min(baseStat.CurrentHp + heal, baseStat.MaxHp);
        }

    }
}