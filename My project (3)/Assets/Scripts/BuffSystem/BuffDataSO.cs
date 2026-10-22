using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// Buff 静态配置。一个资产 = 一种 Buff 模板。
    ///
    /// ⚠️ SO 只读：运行时绝不修改这里的任何字段。
    ///    运行时状态（当前剩余回合、当前层数）写在 BuffBase 实例上。
    ///
    /// 存放位置：Assets/ScriptableObjects/BuffDatas/
    /// </summary>
    [CreateAssetMenu(fileName = "BuffData", menuName = "GameCore/Buff/BuffDataSO")]
    public class BuffDataSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、基础信息
        // ══════════════════════════════════════════════════════════
        [Header("基础信息")]
        [Tooltip("Buff 类型。决定创建哪个 BuffEffect 子类，也是同类型判定 / 互斥判定的依据")]
        public BuffType buffType;

        [Tooltip("UI 显示名称")]
        public string buffName;

        [Tooltip("UI 悬浮描述")]
        [Multiline(4)] public string description;

        [Tooltip("UI 图标")]
        public Sprite icon;

        [Tooltip("true = 减益（Debuff），用于 UI 配色与驱散过滤")]
        public bool isDebuff;

        // ══════════════════════════════════════════════════════════
        //  二、持续规则
        // ══════════════════════════════════════════════════════════
        [Header("持续规则")]
        [Tooltip("TurnDuration = 按回合倒计时；Permanent = 永久，只能被驱散")]
        public BuffRemoveRule removeRule;

        [Tooltip("仅 TurnDuration 有效：持续回合数")]
        [Min(0)] public int defaultDuration = 1;

        // ══════════════════════════════════════════════════════════
        //  三、叠层
        // ══════════════════════════════════════════════════════════
        [Header("叠层")]
        [Tooltip("是否允许叠层。⚠️ 灼烧走【多实例】，不用叠层，此项应为 false")]
        public bool allowStack;

        [Tooltip("最大叠层数（仅 allowStack = true 时有效）")]
        [Min(1)] public int maxStack = 1;

        // ══════════════════════════════════════════════════════════
        //  四、互斥
        // ══════════════════════════════════════════════════════════
        [Header("互斥")]
        [Tooltip("互斥的 Buff 类型。施加本 Buff 时，目标身上这些类型的 Buff 会被驱散")]
        public BuffType[] mutexBuffType = new BuffType[0];

        [Tooltip("true = 覆盖旧互斥 Buff；false = 本次施加失败")]
        public bool replaceMutexBuff = true;

        // ══════════════════════════════════════════════════════════
        //  五、实例化方式
        // ══════════════════════════════════════════════════════════
        [Header("实例化方式")]
        [Tooltip("true  = 多实例：每次施法创建独立实例，各自独立倒计时、各自到期\n" +
                 "false = 单实例：重复施加时按 allowStack 走叠层或替换\n" +
                 "灼烧 / 护盾 = true；眩晕 / 冰冻 / 中毒 / 无敌 / 控制抗性 = false")]
        public bool multiInstance;

        // ══════════════════════════════════════════════════════════
        //  六、各类型专用参数
        // ══════════════════════════════════════════════════════════
        [Header("参数 · 灼烧")]
        [Tooltip("每个实例每回合的固定点数伤害。会先减去目标延迟抗性；抗性 ≥ 该值时伤害为 0")]
        [Min(0)] public int burnDamagePerStack;

        [Header("参数 · 中毒")]
        [Tooltip("每层每回合占最大生命的比例（0.05 = 5%）。会先减去目标延迟抗性（每点减 1 个百分点）")]
        [Min(0f)] public float poisonPercentPerStack;

        [Header("参数 · 控制抗性")]
        [Tooltip("ControlResist 类型生效时，给宿主增加多少控制抗性点数（1 点 = 0.8 个百分点眩晕减免）")]
        [Min(0)] public int addControlResistPoint;

        [Header("参数 · 护盾")]
        [Tooltip("每个实例提供的护盾值。总护盾 = 实例数 × 该值；每实例独立倒计时，到期后该份护盾消失")]
        [Min(0)] public int shieldPerStack = 5;
    }
}