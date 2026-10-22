using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
 {
    /// <summary>
    /// 单位战斗属性。值类型结构体（struct）。
    /// 每次 GetFinalStat() 都会返回一份【独立副本】，
    /// 因此修改副本不会污染原始的角色/装备配置。
    /// ⚠️ 能量、体力不在这里 —— 它们由 UnitResource 单独管理。
    /// </summary>
    [System.Serializable]
    public struct UnitStat
    {
        [Header("生命")]
        public int MaxHp;
        public int CurrentHp;
       

        [Header("攻防")]
        [Tooltip("减免物理伤害用。与 ElementResist 共用同一套分段公式，有效上限 230 点")]
        public int Defense;
        [Tooltip("物理攻击力。卡牌实际伤害 = 该值 × 卡牌倍率")]
        public int PhysicalAtk;
        [Tooltip("法术攻击力（预留，当前伤害公式尚未使用）")]
        public int MagicAtk;
        [Header("抗性")]
        [Tooltip("减免元素伤害用。与 Defense 共用同一套分段公式，有效上限 230 点")]
        public int ElementResist;
        [Tooltip("减免灼烧/中毒/破冰用，有效上限 20 点")]
        public int DelayDamageResist;
        [Tooltip("控制抗性点数，0~125。每点减免 0.8 个百分点的眩晕概率，125 点完全免疫眩晕")]
        public int ControlResistPoint;
        [Header("击退抗性（0~1，>0.5 击退格数减半向上取整）")]
        [Tooltip("超过 0.5 时，击退格数减半并向上取整")]
        [Range(0f, 1f)] public float KnockbackResist;
        [Header("暴击")]
        [Tooltip("暴击率点数（0~100）。1 点 = 1 个百分点。\n" +
                        "实际暴击率 = (角色点数 + 卡牌点数) × 转化系数，总和上限 100%")]
        [Min(0)] public int CritRatePoint;

        [Tooltip("暴击伤害点数（1 点 = 0.05 暴击倍率）。\n" +
                 "前 30 点全额生效，超出部分只算一半。\n" +
                 "实际倍率 = 1.5（基础）+ 折算后点数 × 0.05\n" +
                 "例：角色 4 点 + 卡牌 4 点 = 8 点 → 1.5 + 0.4 = 1.9 倍")]
        [Min(0)] public int CritDamagePoint;
        [Header("行动顺序")]
        [Tooltip("速度：数值大的先行动；同速时每轮重新随机")]
        public int Speed;
        

    }
}