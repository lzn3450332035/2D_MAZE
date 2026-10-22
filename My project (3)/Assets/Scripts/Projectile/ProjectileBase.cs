using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GameCore
{
    /// <summary>
    /// 投射物主脚本，网格移动、命中判定、反弹/穿透/分裂状态机
    /// 严格执行：边界/障碍物/命中单位三套不同消耗优先级
    /// 由 ProjectileSpawner 的 Update 调用 TickMove 驱动
    /// </summary>

    public class ProjectileBase : MonoBehaviour
    {
        
    }
}