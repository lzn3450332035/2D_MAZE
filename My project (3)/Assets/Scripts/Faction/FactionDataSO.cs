using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 阵营定义。一个资产 = 一个阵营（人类 / 精灵 / 恶魔 ……）。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【关系怎么填】
    /// ══════════════════════════════════════════════════════════════
    ///  每个阵营只需要填【友军】和【合作】两个列表。
    ///  没列进去的阵营，一律视为【敌军】（由 FactionRelationTableSO 的
    ///  defaultRelation 决定，建议保持 Enemy）。
    ///
    ///  ⚠️ 只需要填一边！
    ///     人类把精灵写进 allyFactions，精灵那边不写人类也行。
    ///     FactionRelationTableSO.GetRelation() 会做对称化：
    ///     只要任意一方声明为友军，双向都是友军。
    ///
    ///  填写示例（人类）：
    ///     allyFactions          = [精灵, 矮人, 天使]
    ///     cooperativeFactions   = [巫师, 原住民]
    ///     → 剩下的 恶魔 / 僵尸 / 骷髅 / 哥布林 自动是敌军
    ///
    /// 存放位置：Assets/ScriptableObjects/Factions/
    /// </summary>
    [CreateAssetMenu(fileName = "FactionData", menuName = "GameCore/Faction/FactionDataSO")]
    public class FactionDataSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、身份
        // ══════════════════════════════════════════════════════════
        [Header("① 身份")]
        [Tooltip("唯一 ID。存档与查表用它匹配，一旦定下永不修改")]
        public string factionId;

        [Tooltip("显示名称")]
        public string factionName;

        [Multiline(3)] public string description;

        // ══════════════════════════════════════════════════════════
        //  二、外观
        // ══════════════════════════════════════════════════════════
        [Header("② 外观")]
        [Tooltip("阵营主色（UI 标记 / 单位血条颜色）")]
        public Color factionColor = Color.white;

        [Tooltip("阵营图标")]
        public Sprite icon;

        // ══════════════════════════════════════════════════════════
        //  三、行为
        // ══════════════════════════════════════════════════════════
        [Header("③ 行为")]
        [Tooltip("该阵营的 AI 行为配置。\n" +
                 "⚠️ 只需建 3 个共享资产：\n" +
                 "   FactionPlayerBehaviour.asset  （玩家控制时用）\n" +
                 "   FactionMonsterBehaviour.asset （主动进攻型 AI）\n" +
                 "   FactionNeutralBehaviour.asset （被动/中立型 AI）\n" +
                 "10 个阵营按需引用这 3 个，不需要建 10 个")]
        public FactionBehaviourBase behaviour;

        // ══════════════════════════════════════════════════════════
        //  四、关系声明（只需填友军 + 合作，其余默认敌对）
        // ══════════════════════════════════════════════════════════
        [Header("④ 友军阵营（Ally）—— 可互助、可用友方技能")]
        [Tooltip("把这些阵营列为友军。⚠️ 只需要填一边，反向自动对称")]
        public FactionDataSO[] allyFactions = new FactionDataSO[0];

        [Header("⑤ 合作阵营（Cooperative）—— 不互相攻击，但也不互助")]
        [Tooltip("把这些阵营列为合作。⚠️ 只需要填一边，反向自动对称")]
        public FactionDataSO[] cooperativeFactions = new FactionDataSO[0];

        // ══════════════════════════════════════════════════════════
        //  五、便利方法
        // ══════════════════════════════════════════════════════════

        /// <summary>该阵营是否在【自己声明的】友军列表里包含另一方（单向判断，不做对称化）</summary>
        public bool DeclaresAlly(FactionDataSO other)
        {
            if (other == null || allyFactions == null) return false;
            for (int i = 0; i < allyFactions.Length; i++)
            {
                if (allyFactions[i] == other) return true;
            }
            return false;
        }

        /// <summary>该阵营是否在【自己声明的】合作列表里包含另一方（单向判断，不做对称化）</summary>
        public bool DeclaresCooperative(FactionDataSO other)
        {
            if (other == null || cooperativeFactions == null) return false;
            for (int i = 0; i < cooperativeFactions.Length; i++)
            {
                if (cooperativeFactions[i] == other) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(factionId))
                Debug.LogWarning($"[FactionDataSO] {name} 没有填 factionId", this);

            // 自己不能出现在自己的友军/合作列表里
            if (allyFactions != null)
            {
                for (int i = 0; i < allyFactions.Length; i++)
                {
                    if (allyFactions[i] == this)
                        Debug.LogWarning($"[FactionDataSO] {name} 把自己写进了 allyFactions", this);
                }
            }
            if (cooperativeFactions != null)
            {
                for (int i = 0; i < cooperativeFactions.Length; i++)
                {
                    if (cooperativeFactions[i] == this)
                        Debug.LogWarning($"[FactionDataSO] {name} 把自己写进了 cooperativeFactions", this);
                }
            }

            // 同一个阵营不能同时是友军和合作
            if (allyFactions != null && cooperativeFactions != null)
            {
                for (int i = 0; i < allyFactions.Length; i++)
                {
                    for (int j = 0; j < cooperativeFactions.Length; j++)
                    {
                        if (allyFactions[i] != null && allyFactions[i] == cooperativeFactions[j])
                        {
                            Debug.LogError(
                                $"[FactionDataSO] {name}：{allyFactions[i].name} 同时出现在" +
                                $"友军和合作列表里，同一个阵营只能有一个关系", this);
                        }
                    }
                }
            }
        }
#endif
    }
}