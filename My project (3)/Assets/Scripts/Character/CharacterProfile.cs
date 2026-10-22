using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace GameCore
{
    /// <summary>
    /// 角色运行时进度。每个角色一份，会写进存档。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么用 class 而不是 struct】
    /// ══════════════════════════════════════════════════════════════
    ///  存档语义要求「多处引用同一份数据」：
    ///    角色选择界面把战士升到 7 级 → 战斗里读到的也必须是 7 级。
    ///  用 struct 的话每次赋值都会复制一份，改了一份不会影响另一份，
    ///  就会出现"界面显示 7 级、战斗里还是 3 级"的诡异 bug。
    ///
    ///  对照记忆：
    ///    struct → 希望"赋值 = 复制"（UnitStat / KnockbackResult）
    ///    class  → 希望"赋值 = 共享"（CharacterProfile / BuffManager）
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么只存 ID 和数字，不存 CharacterDataSO 引用】
    /// ══════════════════════════════════════════════════════════════
    ///   ① SO 引用存进存档时记录的是资产 GUID，资产被删 / 改名就会断链
    ///   ② SO 本来就存在于项目里，不需要被存
    ///   ③ 运行时用 CharacterDatabaseSO.Find(characterId) 就能找回配置
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【为什么 AddExp 接收委托而不是 CharacterDataSO】
    /// ══════════════════════════════════════════════════════════════
    ///  本类属于 L1 数据层，只能依赖 L0。
    ///  若直接收 CharacterDataSO（L3），就成了 L1 反向依赖 L3。
    ///  改为接收"一个能算出升级所需经验的函数"，依赖方向就正确了
    ///  （与 DamageCalculator.ResolveKnockbackMove 接收 Func 是同一个手法）
    /// </summary>
    [Serializable]
    public class CharacterProfile
    {
        /// <summary>等级硬上限（安全阀，防止配置错误导致无限升级）</summary>
        public const int MaxLevel = 999;

        /// <summary>
        /// 指向 CharacterDataSO.characterId。
        /// ⚠️ 一旦定下永不修改 —— 它是存档匹配角色的唯一依据。
        /// </summary>
        public string characterId;

        /// <summary>当前等级，从 1 开始</summary>
        public int level = 1;

        /// <summary>当前等级内已累积的经验（升级时会把消耗掉的部分扣掉）</summary>
        public int currentExp = 0;

        /// <summary>
        /// 当前血量，跨战斗保留。
        /// -1 = 尚未初始化（首次使用时按满血处理）。
        /// </summary>
        public int currentHp = -1;

        /// <summary>
        /// 无参构造。
        /// ⚠️ 必须保留：Unity 的 JsonUtility 反序列化需要它。
        ///    一旦只写了带参构造，Unity 会静默地存不上 / 读不出这个类。
        /// </summary>
        public CharacterProfile() { }

        /// <summary>用角色 ID 创建一个全新进度（1 级、未初始化血量）</summary>
        public CharacterProfile(string id)
        {
            characterId = id;
            level = 1;
            currentExp = 0;
            currentHp = -1;
        }

        /// <summary>是否是一个有效的角色档案（ID 非空）</summary>
        public bool IsValid => !string.IsNullOrEmpty(characterId);

        /// <summary>
        /// 累加经验并处理升级（可能一次连升多级）。
        /// </summary>
        /// <param name="amount">本次获得的经验值</param>
        /// <param name="expToNextLevel">
        /// 由调用方注入：「从第 N 级升到 N+1 级需要多少经验」。
        /// 实际调用通常是：profile.AddExp(exp, lv => data.GetExpToNextLevel(lv));
        /// </param>
        /// <returns>本次升了几级（0 表示没升级）</returns>
        public int AddExp(int amount, Func<int, int> expToNextLevel)
        {
            if (amount <= 0 || expToNextLevel == null) return 0;

            currentExp += amount;

            int levelUps = 0;

            while (level < MaxLevel)
            {
                int need = expToNextLevel(level);

                // need <= 0 也要退出：万一配置成 0，就会变成"每级白送"的死循环
                if (need <= 0 || currentExp < need) break;

                currentExp -= need;   // ⚠️ 关键：扣掉已消耗的经验，剩下的继续参与下一级判定
                level++;
                levelUps++;
            }

            return levelUps;
        }
    }
}