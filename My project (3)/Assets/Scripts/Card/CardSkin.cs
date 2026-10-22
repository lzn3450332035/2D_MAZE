using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 卡面皮肤。一个资产 = 一套卡面外观，可被多张卡复用。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【这里只放美术与显示文本】
    /// ══════════════════════════════════════════════════════════════
    ///   ✅ 放：主插画、卡面描述文本
    ///   ❌ 不放：费用、稀有度、伤害数值（一律以 CardDataSO 为准）
    ///   ❌ 不放：卡框（由 CardFrameTableSO 按 风格×稀有度 自动查）
    ///
    /// 【为什么要拆成独立资产，而不是塞进 CardDataSO】
    ///   · 同一套卡面可以被多张卡复用（例如 5 张"基础攻击卡"共用一张底图）
    ///   · 美术换皮时只改这一个资产，不用动战斗数据
    ///   · CardDataSO 已经很重（动作数组 + 升级项），再塞美术会更难读
    ///
    /// 存放位置：Assets/ScriptableObjects/CardSkins/
    /// </summary>
    [CreateAssetMenu(fileName = "CardSkin", menuName = "GameCore/Card/CardSkin")]
    public class CardSkin : ScriptableObject
    {
        [Header("① 卡牌背景 / 主插画")]
        [Tooltip("画在卡框内部的主插画，抽象表达这张卡的效果")]
        public Sprite backgroundSprite;

        [Header("② 卡面文本")]
        [Tooltip("显示给玩家的描述。⚠️ 与 CardDataSO.description（给程序看的逻辑说明）不是同一件事")]
        [Multiline(3)] public string displayText;
    }
}