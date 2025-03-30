using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneChangeTest1 : MonoBehaviour
{
    public Transform scene1Target;  // 场景1的目标位置
    public float scene1Speed = 5f;  // 场景1的移动速度
    public float scene2Speed = 3f;  // 场景2的向上移动速度

    private float timer = 0f;
    private bool isInScene1 = true;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer <= 5f && isInScene1)
        {
            // 前5秒：在场景1匀速移动
            MoveInScene1();
        }
        else if (timer > 5f)
        {
            // 后5秒：切换到场景2并向上移动
            if (isInScene1)
            {
                isInScene1 = false;
                // 这里可以添加场景切换的逻辑（如激活/禁用不同的GameObject）
            }
            MoveInScene2();
        }
    }

    void MoveInScene1()
    {
        if (scene1Target != null)
        {
            // 匀速移动到目标位置
            transform.position = Vector3.MoveTowards(
                transform.position,
                scene1Target.position,
                scene1Speed * Time.deltaTime
            );
        }
    }

    void MoveInScene2()
    {
        // 向上移动
        transform.Translate(Vector3.up * scene2Speed * Time.deltaTime);
    }
}
