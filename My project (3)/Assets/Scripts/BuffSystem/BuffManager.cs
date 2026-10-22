using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 单位身上的 Buff 管理器。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【它是 class，不是 MonoBehaviour】
    /// ══════════════════════════════════════════════════════════════
    ///  · 不需要挂在 GameObject 上，UnitBase 持有一个实例即可
    ///  · 一个单位 = 一个 BuffManager（互相独立，不会串）
    ///  · 是引用类型，所以 `unit.BuffManager.HasBuff(...)` 永远读到同一份
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【它为什么依赖 IUnitContext 而不是 UnitBase】
    /// ══════════════════════════════════════════════════════════════
    ///  和 BuffBase 同理 —— 这是整个架构的承重墙：
    ///
    ///      BuffManager ──▶ IUnitContext ◀── UnitBase
    ///      BuffBase    ──▶ IUnitContext
    ///
    ///  → 本文件可以脱离 UnitBase 独立编译，也能用假对象单独测试
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【本文件里唯一允许出现 "if (buffType == Xxx)" 的地方】
    /// ══════════════════════════════════════════════════════════════
    ///  BuffBase 里禁止写类型分支（那是子类该重写的）。
    ///  但 BuffManager 是协调者，它【必须】知道"眩晕碰到冰冻要失败"
    ///  这类跨 Buff 的规则 —— 这是它的本职工作，写在别处反而更糟。
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public class BuffManager
    {
        // ══════════════════════════════════════════════════════════
        //  一、内部数据
        // ══════════════════════════════════════════════════════════

        /// <summary>宿主。所有 Buff 都挂在它身上</summary>
        private readonly IUnitContext owner;

        /// <summary>所有 Buff 实例（按施加顺序）</summary>
        private readonly List<BuffBase> buffs = new List<BuffBase>();

        /// <summary>实例序号自增。用于给多实例 Buff 排稳定顺序</summary>
        private int nextInstanceSeq;

        /// <summary>遍历用的快照缓存（避免在遍历时改原列表）</summary>
        private readonly List<BuffBase> snapshot = new List<BuffBase>();

        /// <summary>护盾吸收用的临时缓存</summary>
        private readonly List<BuffBase> shieldCache = new List<BuffBase>();

        /// <summary>护盾排序比较器（静态字段，避免每次 Sort 都分配一个委托）</summary>
        private static readonly System.Comparison<BuffBase> ShieldCompare =
            (a, b) =>
            {
                // 第一排序键：剩余回合少的先扣（最早到期的先消失）
                int cmp = a.RemainingTurn.CompareTo(b.RemainingTurn);
                if (cmp != 0) return cmp;
                // 第二排序键：创建得早的先扣（保证行为可预测）
                return a.InstanceSeq.CompareTo(b.InstanceSeq);
            };

        // ══════════════════════════════════════════════════════════
        //  二、公开查询
        // ══════════════════════════════════════════════════════════

        /// <summary>所有 Buff（只读视图）。⚠️ 不要在 foreach 里调用 AddBuff / RemoveBuff</summary>
        public IReadOnlyList<BuffBase> AllBuffs => buffs;

        /// <summary>当前 Buff 数量</summary>
        public int Count => buffs.Count;

        /// <summary>
        /// 当前总护盾值 = 所有护盾实例之和。
        /// 注意这是「还能吸收多少」，护盾被消耗后会立刻变小。
        /// 性能：每次访问都遍历一遍，不要放进 Update() 里每帧调用。
        /// </summary>
        public int TotalShield
        {
            get
            {
                int total = 0;
                for (int i = 0; i < buffs.Count; i++)
                    total += buffs[i].ShieldAmount;
                return total;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  三、构造
        // ══════════════════════════════════════════════════════════

        public BuffManager(IUnitContext owner)
        {
            this.owner = owner;
        }

        // ══════════════════════════════════════════════════════════
        //  四、查询
        // ══════════════════════════════════════════════════════════

        /// <summary>身上是否有指定类型的 Buff</summary>
        public bool HasBuff(BuffType type)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].IsType(type)) return true;
            }
            return false;
        }

        /// <summary>取第一个指定类型的 Buff（没有则返回 null）</summary>
        public BuffBase GetFirst(BuffType type)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].IsType(type)) return buffs[i];
            }
            return null;
        }

        /// <summary>
        /// 取所有指定类型的 Buff（新的 List）。
        /// ⚠️ 会产生一次小 GC 分配，不要每帧调用。
        ///   只需要判断"有没有"请用 HasBuff()。
        /// </summary>
        public List<BuffBase> GetAll(BuffType type)
        {
            List<BuffBase> result = new List<BuffBase>();
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].IsType(type)) result.Add(buffs[i]);
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════
        //  五、添加 Buff（本文件最复杂的方法）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 给宿主施加一个 Buff。
        ///
        /// 返回值：
        ///   · 施加成功 → 返回那个 Buff 实例（可能是新建的，也可能是被叠层的旧实例）
        ///   · 施加失败 → 返回 null
        ///
        /// 失败的情况有五种：
        ///   1. data 为 null
        ///   2. 宿主已死
        ///   3. 冰冻期间试图施加眩晕（冰冻免疫眩晕）
        ///   4. 护盾值为 0 的护盾（加了也没用）
        ///   5. 有互斥冲突且 replaceMutexBuff = false
        /// </summary>
        /// <param name="data">Buff 配置</param>
        /// <param name="duration">持续回合。传 -1 表示用 data.defaultDuration</param>
        /// <param name="stackCount">初始层数。新施加一般传 1</param>
        public BuffBase AddBuff(BuffDataSO data, int duration = -1, int stackCount = 1)
        {
            // ── 1. 基础校验 ──
            if (data == null)
            {
                Debug.LogWarning("[BuffManager] AddBuff 收到 null 配置，已忽略。");
                return null;
            }

            if (owner == null || !owner.IsAlive)
                return null;

            // ── 2. 类型免疫规则（跨 Buff 规则，属于 Manager 的职责）──
            // 冰冻期间无法获得眩晕：冰冻是一个"更强的控制"，
            // 如果允许叠加眩晕，解冻后单位会立刻又被眩晕，手感极差。
            if (data.buffType == BuffType.Stun && owner.IsFrozen)
                return null;

            // 护盾值为 0 的护盾没有任何意义，直接拒绝（避免 UI 上挂一个空盾图标）
            if (data.buffType == BuffType.Shield && data.shieldPerStack <= 0)
                return null;

            // ── 3. 互斥处理 ──
            if (!ResolveMutex(data))
                return null;

            // ── 4. 决定持续回合 ──
            int finalDuration = duration >= 0 ? duration : data.defaultDuration;

            // ══════════════════════════════════════════════════════
            //  5. 三种施加方式（判断顺序不能改）
            // ══════════════════════════════════════════════════════
            //  多实例优先：护盾 / 灼烧 永远走这条，不管 allowStack 是什么
            //  再判断叠层：中毒走这条
            //  最后是替换：眩晕 / 冰冻 / 无敌 / 控制抗性 走这条
            // ══════════════════════════════════════════════════════

            if (data.multiInstance)
            {
                // 每次都新建一个独立实例，各自倒计时
                return CreateNew(data, finalDuration, stackCount);
            }

            BuffBase existing = GetFirst(data.buffType);
            if (existing != null)
            {
                if (data.allowStack)
                {
                    // 叠层：层数 +1，同时刷新剩余回合
                    bool stacked = existing.AddStack(stackCount);
                    existing.RefreshDuration(finalDuration);

                    // 即使已经到 maxStack 没加成功，也算"施加成功"（
                    // 因为玩家打出了这张牌，不该因为层数满了就报失败）
                    return existing;
                }

                // 替换：先摘掉旧的，再建新的
                RemoveBuff(existing, BuffRemoveReason.Replaced);
            }

            return CreateNew(data, finalDuration, stackCount);
        }

        /// <summary>
        /// 创建一个 Buff 实例，加入列表，并触发 OnApply。
        /// ⚠️ 调用顺序是写死的，不要调换：
        ///    ① Initialize（填数据）
        ///    ② 加入列表
        ///    ③ OnApply（此时子类已经能查到自己在列表里了）
        /// </summary>
        private BuffBase CreateNew(BuffDataSO data, int duration, int stackCount)
        {
            BuffBase buff = CreateInstanceByType(data.buffType);
            if (buff == null)
            {
                Debug.LogError($"[BuffManager] 无法创建 BuffType = {data.buffType} 的实例。" +
                               $"请检查是否有对应的 Buff{data.buffType} 类，且命名完全一致。");
                return null;
            }

            buff.InstanceSeq = nextInstanceSeq++;
            buff.Initialize(data, owner, this, duration, stackCount);

            buffs.Add(buff);
            buff.OnApply();

            return buff;
        }

        /// <summary>
        /// 按类型创建实例。
        ///
        /// 【为什么用反射，而不是 switch + new】
        ///   因为 ⑤~⑪ 那些子类现在还不存在。如果这里写 switch，
        ///   本文件就无法编译，整个项目会一直红着。
        ///   用反射可以「现在就编译通过，等子类写完自动生效」。
        ///
        /// 【命名约定】
        ///   BuffType.Stun      → GameCore.BuffStun
        ///   BuffType.Burn      → GameCore.BuffBurn
        ///   BuffType.Poison    → GameCore.BuffPoison
        ///   BuffType.Freeze    → GameCore.BuffFreeze
        ///   BuffType.ControlResist → GameCore.BuffControlResist
        ///   BuffType.Invincible → GameCore.BuffInvincible
        ///   BuffType.Shield     → GameCore.BuffShield
        ///
        /// ⚠️ IL2CPP 打包注意：反射创建的类型可能被裁剪。
        ///    Mono / Editor 下没问题；等到要出包时，需要在
        ///    Assets/link.xml 里保留 GameCore.Buff* 这几个类型。
        ///    （现在不用管，等做到打包阶段我会提醒你）
        /// </summary>
        private static BuffBase CreateInstanceByType(BuffType type)
        {
            string className = "GameCore.Buff" + type.ToString();
            System.Type t = System.Type.GetType(className);
            if (t == null) return null;
            if (!typeof(BuffBase).IsAssignableFrom(t)) return null;

            return System.Activator.CreateInstance(t) as BuffBase;
        }

        // ══════════════════════════════════════════════════════════
        //  六、互斥
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 处理互斥。返回 false 表示"本次施加应当失败"。
        ///
        /// 逻辑：
        ///   1. 配置里没写互斥类型 → 直接通过
        ///   2. 身上没有冲突 → 直接通过
        ///   3. 有冲突 + replaceMutexBuff = true → 驱散冲突项，通过
        ///   4. 有冲突 + replaceMutexBuff = false → 本次施加失败
        /// </summary>
        private bool ResolveMutex(BuffDataSO data)
        {
            if (data.mutexBuffType == null || data.mutexBuffType.Length == 0)
                return true;

            // 先判断有没有冲突（不急着删，因为可能整体要失败）
            bool hasConflict = false;
            for (int i = 0; i < buffs.Count && !hasConflict; i++)
            {
                for (int j = 0; j < data.mutexBuffType.Length; j++)
                {
                    if (buffs[i].IsType(data.mutexBuffType[j]))
                    {
                        hasConflict = true;
                        break;
                    }
                }
            }

            if (!hasConflict) return true;

            // 有冲突但不能覆盖 → 本次施加失败
            if (!data.replaceMutexBuff) return false;

            // 驱散所有冲突项（倒序遍历，边遍历边删）
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < data.mutexBuffType.Length; j++)
                {
                    if (buffs[i].IsType(data.mutexBuffType[j]))
                    {
                        RemoveBuffAt(i, BuffRemoveReason.Dispelled);
                        break;
                    }
                }
            }
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  七、移除
        // ══════════════════════════════════════════════════════════

        /// <summary>移除指定的某个 Buff 实例。成功返回 true</summary>
        public bool RemoveBuff(BuffBase buff, BuffRemoveReason reason)
        {
            if (buff == null) return false;

            int index = buffs.IndexOf(buff);
            if (index < 0) return false;   // 不在列表里（可能已经被移除了）

            RemoveBuffAt(index, reason);
            return true;
        }

        /// <summary>移除指定类型的第一个 Buff。成功返回 true</summary>
        public bool RemoveByType(BuffType type, BuffRemoveReason reason)
        {
            int index = IndexOfType(type);
            if (index < 0) return false;

            RemoveBuffAt(index, reason);
            return true;
        }

        /// <summary>
        /// 移除多个类型的所有 Buff，返回移除个数。
        /// 用于牌堆效果"驱散所有减益"这类。
        /// </summary>
        public int RemoveByTypes(BuffType[] types, BuffRemoveReason reason)
        {
            if (types == null || types.Length == 0) return 0;

            int removed = 0;
            // ⚠️ 倒序遍历：这样 RemoveAt(i) 之后，前面的元素索引不受影响
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < types.Length; j++)
                {
                    if (buffs[i].IsType(types[j]))
                    {
                        RemoveBuffAt(i, reason);
                        removed++;
                        break;
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// 移除所有可驱散的减益 Buff（isDebuff = true 且 CanDispel = true）。
        /// 返回移除个数。
        /// </summary>
        public int RemoveAllDebuffs(BuffRemoveReason reason)
        {
            int removed = 0;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                BuffBase b = buffs[i];
                if (b.Data != null && b.Data.isDebuff && b.CanDispel)
                {
                    RemoveBuffAt(i, reason);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>清空所有 Buff。返回移除个数</summary>
        public int ClearAll(BuffRemoveReason reason)
        {
            int removed = buffs.Count;

            // 倒序逐个移除，保证每个 Buff 都能收到 OnRemove 回调
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                RemoveBuffAt(i, reason);
            }
            return removed;
        }

        /// <summary>
        /// 宿主死亡时调用（单位死亡清场用）。
        /// </summary>
        public void OnOwnerDied()
        {
            ClearAll(BuffRemoveReason.OwnerDied);
        }

        // ══════════════════════════════════════════════════════════
        //  八、内部：按索引移除
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 按索引移除并触发回调。
        ///
        /// ⚠️ 顺序必须是：先从列表移除 → 再调 OnRemove。
        ///    反过来的话，OnRemove 里如果查询"我现在还剩什么 Buff"，
        ///    会查到自己（因为还在列表里），逻辑就错了。
        /// </summary>
        private void RemoveBuffAt(int index, BuffRemoveReason reason)
        {
            BuffBase buff = buffs[index];

            buffs.RemoveAt(index);   // ① 先摘出来
            buff.OnRemove(reason);   // ② 再通知
        }

        /// <summary>找指定类型的第一个索引，找不到返回 -1</summary>
        private int IndexOfType(BuffType type)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].IsType(type)) return i;
            }
            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  九、属性加成
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 把所有 Buff 的属性加成叠加到传入的 stat 上。
        /// 由 UnitBase.GetFinalStat() 调用。
        ///
        /// 用法：
        ///     UnitStat final = baseStat;
        ///     buffManager.ApplyAllModifiers(ref final);
        /// </summary>
        public void ApplyAllModifiers(ref UnitStat stat)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                buffs[i].ModifyStat(ref stat);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  十、护盾吸收
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 用护盾吸收伤害。返回【实际吸收的点数】。
        /// 由 UnitBase.TakeDamage() 在扣血之前调用。
        ///
        /// 消耗顺序：剩余回合最少的先扣（最早到期的先消失）；
        ///           剩余回合相同时，创建得早的先扣。
        ///
        /// 这个顺序的意义：让"即将过期的护盾"先被用掉，
        /// 玩家不会有"马上就没了却没用上"的浪费感。
        /// </summary>
        public int AbsorbDamage(int incomingDamage)
        {
            if (incomingDamage <= 0) return 0;

            // 收集所有有盾的实例
            shieldCache.Clear();
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ShieldAmount > 0)
                    shieldCache.Add(buffs[i]);
            }
            if (shieldCache.Count == 0) return 0;

            // 排序只影响 shieldCache，不动 buffs 本身
            shieldCache.Sort(ShieldCompare);

            int remaining = incomingDamage;
            for (int i = 0; i < shieldCache.Count && remaining > 0; i++)
            {
                remaining -= shieldCache[i].AbsorbDamage(remaining);
            }
            return incomingDamage - remaining;
        }

        // ══════════════════════════════════════════════════════════
        //  十一、回合钩子
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 宿主回合开始时调用。
        ///
        /// ⚠️ 为什么用 snapshot 快照：
        ///    灼烧在 OnTurnStart 里扣血 → 单位可能被烧死 →
        ///    死亡会 ClearAll() → buffs 列表被清空。
        ///    如果直接用 for (int i = 0; i < buffs.Count; i++) 遍历，
        ///    循环条件里的 buffs.Count 会变成 0，剩下的 Buff 全部漏处理，
        ///    甚至会 IndexOutOfRange。
        ///    所以先复制一份快照，遍历快照，但每次调用前确认它还活着。
        /// </summary>
        public void OnTurnStart()
        {
            snapshot.Clear();
            snapshot.AddRange(buffs);

            for (int i = 0; i < snapshot.Count; i++)
            {
                BuffBase buff = snapshot[i];
                // 这个 Buff 可能在上一轮的 OnTurnStart 里已经被移除了
                if (!buffs.Contains(buff)) continue;

                buff.OnTurnStart();
            }
        }

        /// <summary>
        /// 宿主回合结束时调用。
        /// 两件事：① 让每个 Buff 结算回合末效果  ② 统一倒计时并移除到期的
        /// </summary>
        public void OnTurnEnd()
        {
            // ── ① 回合末效果 ──
            snapshot.Clear();
            snapshot.AddRange(buffs);

            for (int i = 0; i < snapshot.Count; i++)
            {
                BuffBase buff = snapshot[i];
                if (!buffs.Contains(buff)) continue;

                buff.OnTurnEnd();
            }

            // ── ② 倒计时 + 到期移除 ──
            // 倒序遍历是"边遍历边删"的标准写法：
            //   i 从大到小，RemoveAt(i) 之后，比 i 小的索引全部不受影响。
            //   如果正序，RemoveAt(0) 之后原来的 buffs[1] 变成 buffs[0]，
            //   而 i 已经加到 1 了，就会跳过它。
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                buffs[i].TickDown();

                if (buffs[i].IsExpired)
                {
                    RemoveBuffAt(i, BuffRemoveReason.Expired);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  十二、调试
        // ══════════════════════════════════════════════════════════

        /// <summary>把所有 Buff 拼成一行字符串，方便 Debug.Log 打印</summary>
        public string Dump()
        {
            if (buffs.Count == 0) return "(无 Buff)";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"总护盾={TotalShield} | ");
            for (int i = 0; i < buffs.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(buffs[i].ToString());
            }
            return sb.ToString();
        }
    }
}