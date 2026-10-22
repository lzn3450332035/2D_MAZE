using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class move_1 : MonoBehaviour
{
    [Header("拖拽赋值：玩家对象player_1")]
    public play_1 player;

    private Button btn;

    void Awake()
    {
        // 获取当前物体身上的Button组件
        btn = GetComponent<Button>();
        // 绑定点击事件，点击就执行MovePlayer方法
        btn.onClick.AddListener(MovePlayer);
    }

    void MovePlayer()
    {
        // 调用玩家脚本的移动函数
        player.MoveToFacingDir();
    }
}
