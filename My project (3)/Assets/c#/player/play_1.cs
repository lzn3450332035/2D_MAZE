using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class play_1 : MonoBehaviour
{
    public bool isMoving = false;        // 是否正在移动，防止连续输入打断移动
    private float cellStep;              // 一格的距离，等于主角自身宽度
    public float moveDuration = 0.2f;    // 移动一格花费的时间（秒）
    private Vector3 targetPos;           // 移动的目标位置
    private Vector3 faceDir;      // 当前面朝方向

    private SpriteRenderer spriteRenderer; // 获取主角精灵组件

    void Awake()
    {
        // 获取身上的SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 把一格大小设置为精灵宽度，也就是主角自身大小
        cellStep = spriteRenderer.bounds.size.x;

        // 默认面朝右侧
        faceDir = Vector3.right;
    }
    void Update()
    {
        // WASD 只修改面朝方向，不移动
        if (!isMoving)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                faceDir = Vector3.up;
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                faceDir = Vector3.down;
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                faceDir = Vector3.left;
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                faceDir = Vector3.right;
            }
        }

        // 平滑移动逻辑：如果正在移动
        if (isMoving)
        {
            // 从当前位置向目标位置移动
            transform.position = Vector3.MoveTowards(transform.position, targetPos, cellStep / moveDuration * Time.deltaTime);

            // 到达目标位置，结束移动状态
            if (Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                isMoving = false;
            }
        }
    }
    public void MoveToFacingDir()
    {
        // 如果正在移动，直接返回，禁止重复移动
        if (isMoving) return;

        // 计算目标位置：当前位置 + 面朝方向 * 一格距离
        targetPos = transform.position + faceDir * cellStep;
        // 开启移动状态
        isMoving = true;
    }



}
