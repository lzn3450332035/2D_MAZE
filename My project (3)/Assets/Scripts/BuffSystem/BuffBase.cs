using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace GameCore
{
    // ══════════════════════════════════════════════════════════════
    //  一、Buff 移除原因
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Buff 被移除的原因。
    ///
    /// 【为什么需要这个枚举】
    ///   很多 Buff 的「收尾动作」只在特定原因下才触发。
    ///   最典型的例子：眩晕（BuffStun）
    ///       · 自然到期（Expired）→ 给宿主 +50 点控制抗性（抗眩晕递增机制）
    ///       · 被驱散（Dispelled）→ 不给抗性（玩家主动解控不该有收益）
    ///       · 宿主死亡（OwnerDied）→ 不给抗性（人都死了）
    ///   如果 OnRemove 不带原因参数，这三种情况就没法区分。
    ///
    /// ⚠️ 这个枚举只服务于 Buff 生命周期，不进 L0 的 Enums.cs
    ///    （Enums.cs 放的是跨层共享的公共枚举）
    /// </summary>
    public enum BuffRemoveReason
    {
        /// <summary>回合倒计时归零，自然到期</summary>
        Expired = 0,

        /// <summary>被驱散类效果主动清除</summary>
        Dispelled = 1,

        /// <summary>被新施加的同类 Buff 替换掉了</summary>
        Replaced = 2,

        /// <summary>宿主死亡，连同身上的 Buff 一起清空</summary>
        OwnerDied = 3,

        /// <summary>代码强制移除（战斗结束、复活清场等）</summary>
        Manual = 4
    }

    // ══════════════════════════════════════════════════════════════
    //  二、Buff 抽象基类
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 所有 Buff 的抽象基类。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【分工：本类做什么，子类做什么】
    /// ══════════════════════════════════════════════════════════════
    ///  本类（BuffBase）—— 所有 Buff 共有的东西，【只写一次】
    ///      · 持有配置（BuffDataSO）与宿主（IUnitContext）
    ///      · 管理运行时状态（剩余回合 RemainingTurn / 层数 StackCount）
    ///      · 提供生命周期钩子（OnApply / OnTurnStart / OnTurnEnd / OnRemove）
    ///      · 提供属性加成钩子（ModifyStat）
    ///      · 提供护盾钩子（ShieldAmount / AbsorbDamage）
    ///      · 提供倒计时（TickDown）
    ///
    ///  子类（BuffStun / BuffBurn / ...）—— 每种 Buff 独有的事，【各自重写】
    ///      · 例：BuffBurn 重写 OnTurnStart，扣血
    ///      · 例：BuffStun 重写 OnRemove，给抗性
    ///
    ///  ⚠️ 本类【不写任何 "如果 buffType == Xxx 就如何" 的分支】
    ///     那种写法会让基类越来越胖，最后 7 个子类全挤在一个 switch 里。
    ///     具体差异一律通过重写虚方法实现。
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么是 abstract class，不是 interface】
    /// ══════════════════════════════════════════════════════════════
    ///  1. 7 个子类有大量【完全相同的字段和实现】
    ///     （Data、Target、RemainingTurn、StackCount、TickDown……）
    ///     interface 不能存字段，也不能写默认实现（C# 8 可以，但 Unity
    ///     的 C# 9 环境里 default interface members 与 IL2CPP 兼容性差）。
    ///     用 interface 的话，这 7 份样板代码要抄 7 遍。
    ///
    ///  2. 虚方法可以「有默认实现 + 子类按需重写」，
    ///     interface 只能「全部实现」或「全部不实现」。
    ///
    ///  3. 需要「半成品」语义：BuffBase 不允许被 new，
    ///     abstract 可以在编译期拦住 new BuffBase()。
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么依赖 IUnitContext，而不是 UnitBase】
    /// ══════════════════════════════════════════════════════════════
    ///  如果没有 IUnitContext，会出现依赖环：
    ///
    ///      BuffBase ──▶ UnitBase          （Buff 要读宿主属性）
    ///      UnitBase ──▶ BuffManager       （单位持有自己的管理器）
    ///      BuffManager ──▶ BuffBase       （管理器要创建 / 遍历实例）
    ///      → 环！三个文件必须同时写完才能编译，且无法单独测试
    ///
    ///  有了 IUnitContext 之后：
    ///
    ///      BuffBase ──▶ IUnitContext ◀── UnitBase
    ///      → 依赖方向全部单向，本文件可以脱离 UnitBase 独立编译
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public abstract class BuffBase
    {
        // ══════════════════════════════════════════════════════════
        //  一、身份（初始化后不可变）
        // ══════════════════════════════════════════════════════════

        /// <summary>静态配置。⚠️ 只读，运行时绝不修改 SO 里的任何字段</summary>
        public BuffDataSO Data { get; private set; }

        /// <summary>宿主。只依赖接口，不认识 UnitBase</summary>
        public IUnitContext Target { get; private set; }

        /// <summary>所属的管理器。移除自己时要用到</summary>
        public BuffManager Manager { get; private set; }

        /// <summary>创建序号。护盾多实例按「最早创建先消耗」排序时会用到（相同剩余回合时做稳定排序）</summary>
        public int InstanceSeq { get; internal set; }

        /// <summary>本 Buff 的类型（从配置里取，避免每次写 Data.buffType）</summary>
        public BuffType Type => Data != null ? Data.buffType : BuffType.Stun;

        // ══════════════════════════════════════════════════════════
        //  二、运行时状态（子类可读，只能通过方法改）
        // ══════════════════════════════════════════════════════════

        /// <summary>当前层数。⚠️ 只有 allowStack = true 才会 > 1</summary>
        public int StackCount { get; protected set; }

        /// <summary>剩余回合数。Permanent 的 Buff 这个值没有意义（不会被倒计时）</summary>
        public int RemainingTurn { get; protected set; }

        /// <summary>是否是永久 Buff（只能被驱散，不会自然到期）</summary>
        public bool IsPermanent => Data != null && Data.removeRule == BuffRemoveRule.Permanent;

        /// <summary>是否已经是多实例模式（护盾 / 灼烧）</summary>
        public bool IsMultiInstance => Data != null && Data.multiInstance;

        /// <summary>
        /// 是否已经到期、应该被移除。
        /// ⚠️ Permanent 的 Buff 永远不会「到期」，只能被驱散。
        /// </summary>
        public bool IsExpired => !IsPermanent && RemainingTurn <= 0;

        /// <summary>
        /// 当前还能吸收多少伤害（0 = 不提供护盾）。
        /// 只有 BuffShield 会重写它，其余 Buff 保持默认 0。
        /// </summary>
        public virtual int ShieldAmount => 0;

        /// <summary>
        /// 是否能被驱散类效果清除。
        /// 只有 BuffInvincible 会重写为 false（无敌不可被驱散）。
        /// </summary>
        public virtual bool CanDispel => true;

        // ══════════════════════════════════════════════════════════
        //  三、初始化
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 初始化。由 BuffManager 在创建实例后【立刻】调用。
        ///
        /// 【为什么不用构造函数】
        ///   1. 子类不必写 `public BuffStun(...) : base(...)` 转发一遍，
        ///      以后加参数只改这一处
        ///   2. 允许「先 new、后填数据」，调试时能看到未初始化的状态
        ///   3. BuffManager 可以在加入列表【之前】就完成初始化，
        ///      顺序可控，不会出现「已入列表但 Data 还是 null」的中间态
        ///
        /// ⚠️ 本方法【不调用 OnApply】—— 那一步由 BuffManager 在
        ///    实例真正加入列表之后调用，避免子类 OnApply 里遍历列表时漏掉自己。
        /// </summary>
        /// <param name="data">静态配置，不能为 null</param>
        /// <param name="target">宿主，不能为 null</param>
        /// <param name="manager">所属管理器</param>
        /// <param name="duration">初始剩余回合（Permanent 时随便传，不会被用）</param>
        /// <param name="stackCount">初始层数（新施加一般传 1）</param>
        public void Initialize(
            BuffDataSO data,
            IUnitContext target,
            BuffManager manager,
            int duration,
            int stackCount)
        {
            Data = data;
            Target = target;
            Manager = manager;
            StackCount = stackCount < 1 ? 1 : stackCount;
            RemainingTurn = duration < 0 ? 0 : duration;
        }

        // ══════════════════════════════════════════════════════════
        //  四、生命周期钩子（子类按需重写）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 刚被施加到宿主身上时调用（此时已经在管理器列表里了）。
        /// 典型用途：设置状态标记（眩晕 → Target.SetStun(true)）。
        /// </summary>
        public virtual void OnApply() { }

        /// <summary>
        /// 宿主【自己回合开始】时调用。
        /// 典型用途：灼烧 / 中毒按回合扣血。
        ///
        /// ⚠️ 调用顺序由 BuffManager 保证：
        ///    先倒计时吗？不。回合开始【先结算效果】，回合结束才倒计时。
        /// </summary>
        public virtual void OnTurnStart() { }

        /// <summary>
        /// 宿主【自己回合结束】时调用。
        /// 典型用途：需要「回合末才生效」的效果（目前没有）。
        /// </summary>
        public virtual void OnTurnEnd() { }

        /// <summary>
        /// 被移除时调用。子类在这里做收尾。
        ///
        /// ⚠️ 一定要看 reason 参数：
        ///    眩晕自然到期 → 给宿主加控制抗性
        ///    眩晕被驱散   → 不给
        /// </summary>
        public virtual void OnRemove(BuffRemoveReason reason) { }

        /// <summary>
        /// 属性加成钩子。宿主每次 GetFinalStat() 都会把所有 Buff 的加成叠加上去。
        ///
        /// 【为什么用 ref】
        ///   UnitStat 是 struct，按值传递会复制一份，改了不生效。
        ///   用 ref 传引用，子类直接改宿主正在组装的那一份。
        ///
        /// 典型用途：BuffControlResist 给 ControlResistPoint 加成。
        /// </summary>
        public virtual void ModifyStat(ref UnitStat stat) { }

        // ══════════════════════════════════════════════════════════
        //  五、护盾专用
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 吸收伤害。返回【实际吸收的点数】（0 ~ incomingDamage）。
        /// 只有 BuffShield 会重写它。
        ///
        /// 【为什么用虚方法，而不是在 UnitBase 里写 if (type == Shield)】
        ///   护盾的「扣减」逻辑属于护盾自己（它要维护剩余护盾量）。
        ///   基类只声明接口，具体怎么扣由 BuffShield 决定。
        ///   以后加「能量护盾」「反伤护盾」也只需要新写一个子类。
        /// </summary>
        public virtual int AbsorbDamage(int incomingDamage) => 0;

        // ══════════════════════════════════════════════════════════
        //  六、倒计时
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 剩余回合 -1。由 BuffManager 在宿主回合结束时调用。
        ///
        /// ⚠️ 本方法【不负责移除自己】。移除动作必须由 BuffManager 做，
        ///    因为要改列表 —— Buff 自己 RemoveAt 会破坏正在进行的遍历。
        ///    Manager 的写法是：
        ///        buff.TickDown();
        ///        if (buff.IsExpired) manager.Remove(buff, Expired);
        /// </summary>
        public void TickDown()
        {
            if (IsPermanent) return;      // 永久 Buff 不倒计时
            if (RemainingTurn <= 0) return;

            RemainingTurn--;
        }

        /// <summary>
        /// 刷新持续回合（叠层时重置倒计时 / 卡牌升级提高持续时间时用）。
        /// </summary>
        public void RefreshDuration(int duration)
        {
            RemainingTurn = duration < 0 ? 0 : duration;
        }

        // ══════════════════════════════════════════════════════════
        //  七、叠层
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 层数 +1（受 maxStack 限制）。返回是否真的加上了。
        ///
        /// ⚠️ 只有 Data.allowStack = true 的 Buff 才应该调它。
        ///    multiInstance 的 Buff（护盾 / 灼烧）走「新建实例」路线，
        ///    完全不碰层数。
        /// </summary>
        public bool AddStack(int amount = 1)
        {
            if (Data == null || !Data.allowStack) return false;
            if (amount <= 0) return false;
            if (StackCount >= Data.maxStack) return false;

            StackCount += amount;
            if (StackCount > Data.maxStack) StackCount = Data.maxStack;
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  八、工具
        // ══════════════════════════════════════════════════════════

        /// <summary>是否是指定类型</summary>
        public bool IsType(BuffType t) => Data != null && Data.buffType == t;

        /// <summary>UI 显示名（配置里没填就退回枚举名）</summary>
        public string GetDisplayName()
        {
            if (Data == null) return "(null)";
            return string.IsNullOrEmpty(Data.buffName) ? Data.buffType.ToString() : Data.buffName;
        }

        public override string ToString()
        {
            return $"[{GetDisplayName()} x{StackCount} 剩余{RemainingTurn}回合]";
        }
    }
}