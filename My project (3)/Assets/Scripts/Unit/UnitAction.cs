using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单位「本回合出牌计划」。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【本文件历史：职责搬迁说明】
    /// ══════════════════════════════════════════════════════════════
    ///  旧版 UnitAction 负责：眩晕标记 / 冰冻标记 / CanAct()。
    ///  这些职责已经搬走：
    ///
    ///     眩晕 → UnitBase.SetStun() / IsStunned
    ///     冰冻 → UnitBase.SetFreeze() / IsFrozen
    ///
    ///  原因：BuffStun / BuffFreeze 只认识 IUnitContext 接口，
    ///  不认识 UnitAction。状态必须放在接口实现方（UnitBase）身上，
    ///  否则 Buff 层就要多依赖一个 L4 实体，依赖方向就乱了。
    ///
    ///  现在 UnitAction 只做一件事：
    ///     记录「本回合这个单位排了哪些牌、一共多少消耗」。
    /// ══════════════════════════════════════════════════════════════
    ///  【核心设计：计划阶段不扣资源，执行阶段才扣】
    /// ══════════════════════════════════════════════════════════════
    ///  方案 A（❌ 不用）：TryPlan 时立刻 TrySpend 扣掉，取消时退还。
    ///      问题：退还时如果期间资源涨到上限了，退不满 → 玩家资产凭空消失。
    ///      例：体力 8/10 → 排 3 体力 → 5/10 → 回合内回了 5 → 10/10（被 max 截断）
    ///          → 取消这张牌 → 想退 3 → 10+3 被钳到 10 → 玩家净亏 1 点体力。
    ///
    ///  方案 B（✅ 采用）：TryPlan 只做「够不够」的校验并入队，不扣资源。
    ///                  TryCancel 只从队列移除，不碰资源。
    ///                  由 TurnResolver 在真正执行每张牌之前才 TrySpend 扣费。
    ///      → 排 / 撤 / 重排 全程都是纯列表操作，零资损风险。
    ///      → UI 显示「剩余 5 体力、已排 3」也能直接算出来，不需要改资源。
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public class UnitAction : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  一、内部数据
        // ══════════════════════════════════════════════════════════

        [Header("本回合出牌计划（运行时，只读）")]
        [SerializeField] private List<PlannedCard> plannedCards = new List<PlannedCard>();

        [SerializeField] private int totalStaminaCost;
        [SerializeField] private int totalEnergyCost;

        /// <summary>资源引用。由 UnitBase 在 Awake 里 Bind() 注入。</summary>
        private UnitResource resource;

        // ══════════════════════════════════════════════════════════
        //  二、对外只读查询
        // ══════════════════════════════════════════════════════════

        /// <summary>本回合已排入的牌（按玩家点击顺序）。TurnResolver 按这个顺序执行。</summary>
        public IReadOnlyList<PlannedCard> PlannedCards => plannedCards;

        /// <summary>已排入几张牌</summary>
        public int PlannedCount => plannedCards.Count;

        /// <summary>本回合是否已经排过牌</summary>
        public bool HasPlan => plannedCards.Count > 0;

        /// <summary>已排入的牌一共要花多少体力</summary>
        public int TotalStaminaCost => totalStaminaCost;

        /// <summary>已排入的牌一共要花多少能量</summary>
        public int TotalEnergyCost => totalEnergyCost;

        // ══════════════════════════════════════════════════════════
        //  三、初始化
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 注入资源引用。由 UnitBase 调用（UnitBase 持有 UnitResource 实例）。
        /// ⚠️ 这行注释里的“由 UnitBase 调用”是硬约定 ——
        ///    如果没人调，TryPlan 会打 LogError 并拒绝入队，不会静默出错。
        /// </summary>
        public void Bind(UnitResource unitResource)
        {
            resource = unitResource;
        }

        // ══════════════════════════════════════════════════════════
        //  四、排牌
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 这张牌现在能不能排进去（只判断，不修改任何状态）。
        /// UI 上用来决定「这张牌灰不灰」。
        /// </summary>
        public bool CanPlan(CardDataSO data, CardInstance inst)
        {
            if (data == null || inst == null) return false;
            if (resource == null) return false;

            // 同一张牌不能在一个回合里排两次（CardInstance 是引用类型，比引用即可）
            if (IsPlanned(inst)) return false;

            int staminaCost = data.GetStaminaCost(inst);
            int energyCost = data.GetEnergyCost(inst);

            // 用「已排总消耗 + 这张」去和当前资源比
            return resource.CanAfford(
                totalStaminaCost + staminaCost,
                totalEnergyCost + energyCost);
        }

        /// <summary>
        /// 尝试把一张牌排入本回合计划。成功返回 true。
        /// ⚠️ 只入队，【不扣资源】—— 见类注释的方案 B。
        /// </summary>
        public bool TryPlan(CardDataSO data, CardInstance inst)
        {
            if (data == null || inst == null)
            {
                Debug.LogWarning("[UnitAction] TryPlan 收到空引用，已忽略。");
                return false;
            }

            if (resource == null)
            {
                Debug.LogError("[UnitAction] 还没 Bind(UnitResource) 就被调用了 TryPlan。" +
                               "请检查 UnitBase.Awake 里是否调用了 unitAction.Bind(resource)。");
                return false;
            }

            if (!CanPlan(data, inst))
                return false;

            int staminaCost = data.GetStaminaCost(inst);
            int energyCost = data.GetEnergyCost(inst);

            plannedCards.Add(new PlannedCard
            {
                data = data,
                inst = inst,
                staminaCost = staminaCost,
                energyCost = energyCost
            });

            totalStaminaCost += staminaCost;
            totalEnergyCost += energyCost;
            return true;
        }

        /// <summary>
        /// 撤销一张已排入的牌。成功返回 true。
        /// ⚠️ 只出队，【不退资源】—— 因为入队时就没扣过。
        /// </summary>
        public bool TryCancel(CardInstance inst)
        {
            if (inst == null) return false;

            for (int i = 0; i < plannedCards.Count; i++)
            {
                if (plannedCards[i].inst != inst) continue;

                totalStaminaCost -= plannedCards[i].staminaCost;
                totalEnergyCost -= plannedCards[i].energyCost;
                plannedCards.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>这张牌是否已经排在本回合计划里</summary>
        public bool IsPlanned(CardInstance inst)
        {
            if (inst == null) return false;

            for (int i = 0; i < plannedCards.Count; i++)
            {
                if (plannedCards[i].inst == inst) return true;
            }
            return false;
        }

        /// <summary>
        /// 清空本回合计划。
        /// 调用时机：① 回合结束、行动全部执行完之后；② 单位死亡；③ 战斗结束。
        /// ⚠️ 不碰 UnitResource —— 执行阶段已经扣过的资源【不退还】。
        /// </summary>
        public void ClearPlan()
        {
            plannedCards.Clear();
            totalStaminaCost = 0;
            totalEnergyCost = 0;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  计划条目
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 一条「已排入的牌」。
    ///
    /// 【为什么是 struct】
    ///   它只是一个一次性的数据打包，不需要身份、不需要被修改、不需要继承。
    ///   放进 List 里也是按值存储（不会为每条多分配一个堆对象）。
    ///
    /// 【为什么要把 staminaCost / energyCost 缓存下来】
    ///   1. 排牌时算过一次，随时要用就不用反复调 CardDataSO.GetStaminaCost()
    ///      （那个方法内部要遍历 upgradeEntries 求和，不算便宜）
    ///   2. 累计总消耗时直接加字段，不用重新遍历
    ///   3. 万一玩家在 UI 上撤销了一半，退的计算量和当初扣的完全一致（不会因为
    ///      中途改了 SO 数值而对不上——虽然运行时 SO 不改，但这样更稳）
    ///
    /// 【为什么把 data 也存下来】
    ///   CardInstance 里只有 cardId，没有 CardDataSO 引用。
    ///   执行阶段需要 data 来读 actions[]，所以必须在这里带上。
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    [System.Serializable]
    public struct PlannedCard
    {
        [Tooltip("这张牌的静态配置")]
        public CardDataSO data;

        [Tooltip("这张牌的运行时实例（等级 / 升级进度在这里）")]
        public CardInstance inst;

        [Tooltip("排入时的实际体力消耗（已含升级修正）")]
        public int staminaCost;

        [Tooltip("排入时的实际能量消耗（已含升级修正）")]
        public int energyCost;

        public bool IsValid => data != null && inst != null;
    }
}