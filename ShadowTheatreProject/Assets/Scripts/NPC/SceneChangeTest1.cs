using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneChangeTest1 : MonoBehaviour
{
    public Transform scene1Target;  // ����1��Ŀ��λ��
    public float scene1Speed = 5f;  // ����1���ƶ��ٶ�
    public float scene2Speed = 3f;  // ����2�������ƶ��ٶ�

    private float timer = 0f;
    private bool isInScene1 = true;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer <= 5f && isInScene1)
        {
            // ǰ5�룺�ڳ���1�����ƶ�
            MoveInScene1();
        }
        else if (timer > 5f)
        {
            // ��5�룺�л�������2�������ƶ�
            if (isInScene1)
            {
                isInScene1 = false;
                // ���������ӳ����л����߼����缤��/���ò�ͬ��GameObject��
            }
            MoveInScene2();
        }
    }

    void MoveInScene1()
    {
        if (scene1Target != null)
        {
            // �����ƶ���Ŀ��λ��
            transform.position = Vector3.MoveTowards(
                transform.position,
                scene1Target.position,
                scene1Speed * Time.deltaTime
            );
        }
    }

    void MoveInScene2()
    {
        // �����ƶ�
        transform.Translate(Vector3.up * scene2Speed * Time.deltaTime);
    }
}
