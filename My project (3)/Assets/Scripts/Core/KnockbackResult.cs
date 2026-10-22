using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GameCore
{
    /// <summary>
    /// 击退移动的计算结果。值类型结构体。
    ///
    /// 由 DamageCalculator.ResolveKnockbackMove（纯函数）计算得出，
    /// 调用方再根据它去更新网格注册与 transform 位置。
    ///
    /// 之所以做成结构体而不是多个 out 参数：
    ///   ① 可整体返回，调用处只写一行
    ///   ② 可作为字段 / 参数传递，便于以后扩展
    ///   ③ 以后要加"撞到的是什么类型的障碍"这类字段时，改动只在两处
    /// </summary>
    public struct KnockbackResult
    {
        /// <summary>是否撞到了障碍。为 true 时，调用方应触发「撞墙眩晕」判定</summary>
        public bool hitObstacle;

        /// <summary>
        /// 单位击退后的最终【锚点格】。
        /// 未移动时等于起始格；移动了 N 格时等于起始格 + 方向 × N。
        /// </summary>
        public Vector2Int finalGrid;

        /// <summary>
        /// 撞到障碍时，障碍所在的那一格。
        /// 用途：撞击特效定位、调试日志。未撞到时等于 finalGrid。
        /// </summary>
        public Vector2Int blockedGrid;

        /// <summary>实际移动的格数（可能小于请求的格数，因为中途撞到了障碍）</summary>
        public int movedGrids;

        /// <summary>是否真的移动了至少一格</summary>
        public bool HasMoved => movedGrids > 0;

        /// <summary>
        /// 构造一个「原地不动」的结果。
        /// ⚠️ 不要用 new KnockbackResult() 来表达"不动"——
        ///    它的字段会被清零，finalGrid 会变成 (0,0)，那是个合法的地图坐标，容易埋雷。
        /// </summary>
        public static KnockbackResult Stay(Vector2Int grid)
        {
            KnockbackResult r = new KnockbackResult();
            r.hitObstacle = false;
            r.finalGrid = grid;
            r.blockedGrid = grid;
            r.movedGrids = 0;
            return r;
        }
    }
}

