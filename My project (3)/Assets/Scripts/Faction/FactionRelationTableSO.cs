using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 阵营关系表。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【数据从哪来】
    /// ══════════════════════════════════════════════════════════════
    ///  不在本文件里存 45 条规则，而是从每个 FactionDataSO 自己声明的
    ///  allyFactions / cooperativeFactions 聚合出来。
    ///
    ///  本文件负责三件事：
    ///    ① 把「单向声明」对称化 → 保证 A 视 B = B 视 A
    ///    ② 提供反查：「和 X 是友军的所有阵营有哪些」
    ///    ③ 编辑器自检
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么用「每个阵营自己声明」而不是「一张 45 行的规则表」】
    /// ══════════════════════════════════════════════════════════════
    ///  10 个阵营两两组合 = 45 对。
    ///    规则表方案：手动填 45 行，每行拖 2 个资产 + 选 1 个枚举
    ///                → 135 次操作，且极容易漏填、填错方向
    ///    声明方案：  每个阵营只填自己的友军和合作
    ///                → 22 条（9 友 + 13 合），且反向自动对称
    ///  → 填写量减少 51%，且语义更直观（「人类的盟友是精灵和矮人」）
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【对称化规则（优先级）】
    /// ══════════════════════════════════════════════════════════════
    ///  任意一方声明为 Ally          → Ally（善意优先）
    ///  否则任意一方声明为 Cooperative → Cooperative
    ///  否则                          → defaultRelation（建议 Enemy）
    ///
    ///  优先级为什么是「善意优先」而不是「恶意优先」：
    ///    · 善意优先时，漏填一边不会导致友军误判为敌人（友军互殴是最严重的 bug）
    ///    · 恶意优先时，漏填一边会让友军变成敌人，后果更严重
    ///  → 所以选择「最坏情况是少打一场架，而不是队友互殴」
    ///
    /// 存放位置：Assets/ScriptableObjects/Factions/FactionRelationTable.asset
    /// </summary>
    [CreateAssetMenu(fileName = "FactionRelationTable", menuName = "GameCore/Faction/FactionRelationTable")]
    public class FactionRelationTableSO : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  一、配置
        // ══════════════════════════════════════════════════════════

        [Header("① 所有阵营（把项目里每个 FactionDataSO 都放进来）")]
        [Tooltip("随机生成时需要知道「一共有哪些阵营」。\n" +
                 "⚠️ 漏放一个阵营，它就不会被生成出来")]
        public FactionDataSO[] allFactions = new FactionDataSO[0];

        [Header("② 没声明关系时的默认值")]
        [Tooltip("建议保持 Enemy（陌生即敌对）")]
        public FactionRelation defaultRelation = FactionRelation.Enemy;

        // ══════════════════════════════════════════════════════════
        //  二、核心查询
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 查两个阵营的关系（对称：A→B 与 B→A 结果相同）。
        ///
        /// 这是【唯一应该被外部调用的关系查询入口】。
        /// 不要在业务代码里自己去翻 allyFactions 数组 —— 那样会丢掉对称化。
        /// </summary>
        public FactionRelation GetRelation(FactionDataSO x, FactionDataSO y)
        {
            if (x == null || y == null) return defaultRelation;
            if (x == y) return FactionRelation.Ally;    // 自己对自己永远是友军

            // 善意优先：任意一方声明就生效
            if (x.DeclaresAlly(y) || y.DeclaresAlly(x))
                return FactionRelation.Ally;

            if (x.DeclaresCooperative(y) || y.DeclaresCooperative(x))
                return FactionRelation.Cooperative;

            return defaultRelation;
        }

        /// <summary>是否可被选为敌方目标</summary>
        public bool CanBeTargetedAsEnemy(FactionDataSO attacker, FactionDataSO target)
            => GetRelation(attacker, target) == FactionRelation.Enemy;

        /// <summary>是否可以使用友方技能</summary>
        public bool CanUseAllySkill(FactionDataSO caster, FactionDataSO target)
            => GetRelation(caster, target) == FactionRelation.Ally;

        // ══════════════════════════════════════════════════════════
        //  三、反查（随机生成的核心）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 找出所有和 origin 【关系为 relation】的阵营。
        ///
        /// ⚠️ 结果里【包含 origin 自己】吗？
        ///    包含 —— 因为「自己和自己」的关系是 Ally。
        ///    生成友军时通常需要「同阵营的其他单位」，所以这是有意的。
        ///    如果你不想要自己，调用后手动剔除。
        ///
        /// 用法（生成友军）：
        ///     var pool = table.GetFactionsByRelation(playerFaction, FactionRelation.Ally);
        /// </summary>
        public List<FactionDataSO> GetFactionsByRelation(FactionDataSO origin, FactionRelation relation)
        {
            List<FactionDataSO> result = new List<FactionDataSO>();

            if (origin == null || allFactions == null) return result;

            for (int i = 0; i < allFactions.Length; i++)
            {
                FactionDataSO f = allFactions[i];
                if (f == null) continue;

                if (GetRelation(origin, f) == relation)
                    result.Add(f);
            }
            return result;
        }

        /// <summary>所有友军阵营（含自己）</summary>
        public List<FactionDataSO> GetAlliesOf(FactionDataSO origin)
            => GetFactionsByRelation(origin, FactionRelation.Ally);

        /// <summary>所有敌军阵营</summary>
        public List<FactionDataSO> GetEnemiesOf(FactionDataSO origin)
            => GetFactionsByRelation(origin, FactionRelation.Enemy);

        /// <summary>所有合作阵营</summary>
        public List<FactionDataSO> GetCooperativesOf(FactionDataSO origin)
            => GetFactionsByRelation(origin, FactionRelation.Cooperative);

        /// <summary>按 factionId 找阵营资产</summary>
        public FactionDataSO FindById(string factionId)
        {
            if (string.IsNullOrEmpty(factionId) || allFactions == null) return null;

            for (int i = 0; i < allFactions.Length; i++)
            {
                if (allFactions[i] != null && allFactions[i].factionId == factionId)
                    return allFactions[i];
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════
        //  四、编辑器自检
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (allFactions == null || allFactions.Length == 0) return;

            // ── 检查 1：空引用 / 重复 / 缺 factionId ──
            HashSet<string> seenIds = new HashSet<string>();
            for (int i = 0; i < allFactions.Length; i++)
            {
                FactionDataSO f = allFactions[i];
                if (f == null)
                {
                    Debug.LogWarning($"[FactionRelationTable] allFactions 第 {i} 项是空的", this);
                    continue;
                }
                if (string.IsNullOrEmpty(f.factionId))
                {
                    Debug.LogError($"[FactionRelationTable] 阵营 {f.name} 没有填 factionId", this);
                    continue;
                }
                if (!seenIds.Add(f.factionId))
                {
                    Debug.LogError($"[FactionRelationTable] factionId 重复：{f.factionId}", this);
                }
            }

            // ── 检查 2：每对关系是否「双向一致」（冲突检查）──
            for (int i = 0; i < allFactions.Length; i++)
            {
                FactionDataSO a = allFactions[i];
                if (a == null) continue;

                for (int j = i + 1; j < allFactions.Length; j++)
                {
                    FactionDataSO b = allFactions[j];
                    if (b == null) continue;

                    bool aAlly = a.DeclaresAlly(b), bAlly = b.DeclaresAlly(a);
                    bool aCoop = a.DeclaresCooperative(b), bCoop = b.DeclaresCooperative(a);

                    // a 说是友军，b 说是合作 → 冲突（善意优先会取友军，但这是配置错误）
                    if (aAlly && bCoop)
                        Debug.LogWarning(
                            $"[FactionRelationTable] 关系冲突：{a.name} 把 {b.name} 当友军，" +
                            $"但 {b.name} 把 {a.name} 当合作。将按「善意优先」取友军。", this);

                    if (bAlly && aCoop)
                        Debug.LogWarning(
                            $"[FactionRelationTable] 关系冲突：{b.name} 把 {a.name} 当友军，" +
                            $"但 {a.name} 把 {b.name} 当合作。将按「善意优先」取友军。", this);
                }
            }

            // ── 检查 3：有没有阵营「一个友军都没有」──
            // 这种阵营在随机生成友军时会得到空池，是个功能性缺陷
            for (int i = 0; i < allFactions.Length; i++)
            {
                FactionDataSO f = allFactions[i];
                if (f == null) continue;

                List<FactionDataSO> allies = GetAlliesOf(f);

                // 减掉自己（GetAlliesOf 会把自己算进去）
                int otherAllies = allies.Count - 1;

                if (otherAllies <= 0)
                {
                    Debug.LogError(
                        $"[FactionRelationTable] 阵营 {f.name} 没有任何友军阵营。" +
                        $"玩家选它时「友军名额」会无法填充。请给它至少加 1 个友军，或调整生成权重。", this);
                }
            }
        }

        /// <summary>编辑器按钮：打印完整的关系矩阵（方便核对）</summary>
        [ContextMenu("打印关系矩阵")]
        private void PrintRelationMatrix()
        {
            if (allFactions == null || allFactions.Length == 0) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("阵营关系矩阵（行 → 列）");

            // 表头
            sb.Append("            ");
            for (int j = 0; j < allFactions.Length; j++)
                sb.Append($"{allFactions[j]?.name,-10}");
            sb.AppendLine();

            for (int i = 0; i < allFactions.Length; i++)
            {
                FactionDataSO a = allFactions[i];
                sb.Append($"{a?.name,-12}");

                for (int j = 0; j < allFactions.Length; j++)
                {
                    FactionDataSO b = allFactions[j];
                    FactionRelation r = (a == b) ? FactionRelation.Ally : GetRelation(a, b);

                    string mark = r == FactionRelation.Ally ? "友"
                                : r == FactionRelation.Cooperative ? "合"
                                : "敌";

                    sb.Append($"{mark,-10}");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString(), this);
        }
#endif
    }
}