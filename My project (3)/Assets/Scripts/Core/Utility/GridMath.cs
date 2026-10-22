using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 网格坐标数学工具。全部是【纯函数】。
    ///
    /// ⚠️ 本类属于 L2 计算层，严禁访问任何单例
    ///    （GridManager / BuffManager / BattleManager ……）。
    ///    需要"某个格子能不能走"这类判断时，由调用方把尺寸或判断函数传进来。
    ///
    /// 用途：击退、闪电连锁、范围判定、寻找最近 / 最远单位、方向换算
    /// </summary>
    public static class GridMath
    {
        // ══════════════════════════════════════════════════════════
        //  一、距离与坐标运算
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 曼哈顿距离（网格游戏标准距离：只允许上下左右，不允许斜向）
        /// |x1-x2| + |y1-y2|
        /// </summary>
        public static int ManhattanDistance(Vector2Int a,Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return dx + dy;
        }
        /// <summary>
        /// 两个网格坐标是否完全相同
        /// </summary>
        public static bool PosEqual(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;

   
        /// <summary>
        /// 坐标相加
        /// </summary>
        public static Vector2Int AddPos(Vector2Int  a,Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);

        /// <summary>
        /// 坐标相减 a - b
        /// </summary>
        public static Vector2Int SubPos(Vector2Int a,Vector2Int b)=> new Vector2Int(a.x - b.x, a.y - b.y);
 
        /// <summary>
        /// 获取两点之间坐标差的绝对值
        /// </summary
        public static Vector2Int AbsDelta(Vector2Int a,Vector2Int b) => new Vector2Int(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        /// <summary>AddPos 的别名（早期命名，保留以兼容旧调用）</summary>
        public static Vector2Int AddGrid(Vector2Int a, Vector2Int b) => AddPos(a, b);
        // ══════════════════════════════════════════════════════════
        //  二、边界判断（尺寸由调用方传入 —— 这就是"纯函数化"）
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 目标格是否在网格内。
        /// ⚠️ 与旧版 IsOutOfBounds 的差别：尺寸改为【参数传入】，
        ///    因此本方法不再依赖 GridManager，L2 保持纯净。
        /// </summary>
        public static bool IsInsideBounds(Vector2Int grid, Vector2Int gridSize) => grid.x >= 0 && grid.y >= 0 && grid.x < gridSize.x && grid.y < gridSize.y;
        // ══════════════════════════════════════════════════════════
        //  三、方向
        // ══════════════════════════════════════════════════════════

        /// <summary>反向向量</summary>
        
        public static Vector2Int ReverseDir(Vector2Int dir) => new Vector2Int(-dir.x, -dir.y);
        /// <summary>
        /// 从 from 指向 to 的【四方向】单位向量。
        ///
        /// 规则：取绝对值较大的那一轴；平局（真斜向）时优先取水平方向。
        /// 两点重合时返回 (0, 0)，调用方需自行判空。
        ///
        /// ⚠️ 旧实现会把 (2,1) 这类偏移直接返回 (1,1) —— 那是斜向，
        ///    会让击退与投射物斜穿格网。此处已修正为只返回四方向。
        /// </summary>

        public static Vector2Int GetDirectionNormalized(Vector2Int from,Vector2Int to)
        {
            Vector2Int delta = SubPos(to, from);
            if (delta.x == 0 && delta.y == 0) return Vector2Int.zero;
            int adx = Mathf.Abs(delta.x);

            int ady = Mathf.Abs(delta.y);
            if (adx >= ady) return new Vector2Int(delta.x > 0 ? 1 : -1, 0);

            
         
            return new Vector2Int(0,delta.y > 0 ? 1 : - 1);
        }
        /// <summary>
        /// 四方向枚举 → 网格向量。
        /// ⚠️ 坐标约定：网格原点在【左下角】，Y 轴向上（与 Unity 世界坐标一致）。
        /// </summary>
        public static Vector2Int ToVector(this GridDirection dir)
        {
            switch (dir)
            {
                case GridDirection.Up: return new Vector2Int(0, 1);
                case GridDirection.Down: return new Vector2Int(0, -1);
                case GridDirection.Left: return new Vector2Int(-1, 0);
                case GridDirection.Right: return new Vector2Int(1, 0);
                default:                 return Vector2Int.zero;
            }

        }
        /// <summary>网格向量 → 四方向枚举。非正交向量返回 null</summary>
        public static GridDirection? ToDirection(Vector2Int v)
        {
            if (v.x == 0 && v.y == 1) return GridDirection.Up;
            if (v.x == 0 && v.y == -1) return GridDirection.Down;
            if (v.x == -1 && v.y == 0) return GridDirection.Left;
            if (v.x == 1 && v.y == 0) return GridDirection.Right;
            return null;
        }
        /// <summary>顺时针旋转 90 度（分裂投射物的左右分支用）</summary>
        public static Vector2Int RotateRight(Vector2Int dir) => new Vector2Int(dir.y, -dir.x);

        /// <summary>逆时针旋转 90 度</summary>
        public static Vector2Int RotateLeft(Vector2Int dir) => new Vector2Int(-dir.y, dir.x);
    }


}
