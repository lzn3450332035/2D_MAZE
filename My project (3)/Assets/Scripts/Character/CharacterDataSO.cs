using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色静态配置。一个资产 = 一个可玩角色的模板。
    ///
    /// ⚠️ 只在编辑器里配置，运行时绝不修改。
    ///    运行时进度（等级 / 经验 / 当前血量）记在 CharacterProfile 上。
    ///
    /// 存放位置：Assets/ScriptableObjects/Characters/
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "GameCore/Character/CharacterDataSO")]
    public class CharacterDataSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、身份
        // ══════════════════════════════════════════════════════════
        [Header("① 身份")]
        [Tooltip("唯一 ID。与 CharacterProfile.characterId 对应，一旦定下永不修改")]
        public string characterId;

        public string characterName;

        [Multiline(3)] public string description;

        [Tooltip("立绘 / 头像")]
        public Sprite portrait;

        // ══════════════════════════════════════════════════════════
        //  二、外观与体型
        // ══════════════════════════════════════════════════════════
        [Header("② 外观与体型")]
        [Tooltip("战斗中的预制体")]
        public GameObject prefab;

        [Tooltip("占用格数：Small=1×1 / Elite=2×2 / Boss=3×3")]
        public UnitSize unitSize = UnitSize.Small;

        // ══════════════════════════════════════════════════════════
        //  三、阵营
        // ══════════════════════════════════════════════════════════
        [Header("③ 阵营")]
        public FactionDataSO faction;

        // ══════════════════════════════════════════════════════════
        //  四、1 级基础属性
        // ══════════════════════════════════════════════════════════
        [Header("④ 1 级基础属性")]
        [Min(1)] public int baseMaxHp = 100;
        public int baseDefense;
        [Min(0)] public int basePhysicalAtk = 10;
        public int baseMagicAtk;
        public int baseElementResist;
        [Min(0)] public int baseDelayDamageResist;
        [Min(0)] public int baseControlResistPoint;
        [Range(0f, 1f)] public float baseKnockbackResist;
        [Min(0)] public int baseCritRatePoint;
        [Min(0)] public int baseCritDamagePoint;
        public int baseSpeed = 10;

        // ══════════════════════════════════════════════════════════
        //  五、每级成长
        // ══════════════════════════════════════════════════════════
        [Header("⑤ 每级成长（每升 1 级增加多少）")]
        public int growMaxHp;
        public int growDefense;
        public int growPhysicalAtk;
        public int growMagicAtk;
        public int growElementResist;
        public int growDelayDamageResist;
        public int growControlResistPoint;
        [Range(0f, 1f)] public float growKnockbackResist;
        public int growCritRatePoint;
        public int growCritDamagePoint;
        public int growSpeed;

        // ══════════════════════════════════════════════════════════
        //  六、等级与经验
        // ══════════════════════════════════════════════════════════
        [Header("⑥ 等级与经验")]
        [Min(1)] public int maxLevel = 999;

        [Tooltip("1 级 → 2 级所需经验")]
        [Min(1)] public int expBase = 100;

        [Tooltip("每升 1 级，所需经验增加多少（线性）")]
        [Min(0)] public int expGrowthPerLevel = 50;

        // ══════════════════════════════════════════════════════════
        //  七、能量与体力
        // ══════════════════════════════════════════════════════════
        [Header("⑦ 能量与体力（每场战斗开始时重置）")]
        [Min(0)] public int initialEnergy = 10;
        [Min(0)] public int maxEnergy = 10;
        [Min(0)] public int energyRegenPerTurn = 2;

        [Min(0)] public int initialStamina = 5;
        [Min(0)] public int maxStamina = 10;
        [Min(0)] public int staminaRegenPerTurn = 3;

        // ══════════════════════════════════════════════════════════
        //  八、复活
        // ══════════════════════════════════════════════════════════
        [Header("⑧ 复活次数")]
        [Min(0)] public int initialReviveCount = 1;

        // ══════════════════════════════════════════════════════════
        //  九、初始卡组
        // ══════════════════════════════════════════════════════════
        [Header("⑨ 初始卡组（固定 21 张：5 移动 + 5 防御 + 3 回血 + 8 攻击）")]
        [Tooltip("本轮开始时的固定牌组，不经过卡池抽取")]
        public CardDataSO[] starterDeck = new CardDataSO[0];

        // ══════════════════════════════════════════════════════════
        //  十、计算方法
        // ══════════════════════════════════════════════════════════

        /// <summary>指定等级下的完整属性</summary>
        public UnitStat GetStatAtLevel(int lv)
        {
            int bonus = Mathf.Max(0, lv - 1);
            UnitStat s = new UnitStat();

            s.MaxHp = baseMaxHp + growMaxHp * bonus;
            s.CurrentHp = s.MaxHp;
            s.Defense = baseDefense + growDefense * bonus;
            s.PhysicalAtk = basePhysicalAtk + growPhysicalAtk * bonus;
            s.MagicAtk = baseMagicAtk + growMagicAtk * bonus;
            s.ElementResist = baseElementResist + growElementResist * bonus;
            s.DelayDamageResist = baseDelayDamageResist + growDelayDamageResist * bonus;
            s.ControlResistPoint = baseControlResistPoint + growControlResistPoint * bonus;
            s.KnockbackResist = Mathf.Clamp01(baseKnockbackResist + growKnockbackResist * bonus);
            s.CritRatePoint = baseCritRatePoint + growCritRatePoint * bonus;
            s.CritDamagePoint = baseCritDamagePoint + growCritDamagePoint * bonus;
            s.Speed = baseSpeed + growSpeed * bonus;

            return s;
        }

        /// <summary>
        /// 从 lv 升到 lv+1 所需经验。
        /// 线性曲线：expBase + expGrowthPerLevel × (lv − 1)
        /// </summary>
        public int GetExpToNextLevel(int lv)
        {
            if (lv >= maxLevel) return 0;
            return Mathf.Max(1, expBase + expGrowthPerLevel * (lv - 1));
        }

        /// <summary>等级是否已满</summary>
        public bool IsMaxLevel(int lv) => lv >= maxLevel;
    }
}