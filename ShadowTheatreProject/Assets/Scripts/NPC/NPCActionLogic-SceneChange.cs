using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCActionLogic : MonoBehaviour
{
    private Animator animator; //用于控制NPC的动画播放
    private bool isAffected = false; // 是否受到外界影响

    public Transform scene1; // 场景1的位置
    public Transform scene2; // 场景2的位置
    public float speed = 2.0f; // NPC移动速度

    // 定义NPC的默认行为状态
    private enum NPCState
    {
        Idle,
        Walk,
        Talk
    }

    private NPCState currentState = NPCState.Idle;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        StartCoroutine(DefaultBehavior());

    }

    // Update is called once per frame
    void Update()
    {
        // 如果受到外界影响，停止默认行为
        if (isAffected)
        {
            StopAllCoroutines();
            return;
        }

    }
    // NPC的默认行为协程
    IEnumerator DefaultBehavior()
    {
        while (true)
        {

            switch (currentState)
            {
                case NPCState.Idle:
                    animator.Play("Idle");
                    yield return new WaitForSeconds(3); // 空闲3秒
                    currentState = NPCState.Walk;
                    break;

                case NPCState.Walk:
                    animator.Play("Walk");
                    yield return new WaitForSeconds(5); // 行走5秒
                    currentState = NPCState.Talk;
                    break;

                case NPCState.Talk:
                    animator.Play("Talk");
                    yield return new WaitForSeconds(2); // 说话2秒
                    currentState = NPCState.Idle;
                    break;
            }

            
                // 从场景1走到场景2
                animator.Play("Walk");
                yield return MoveToPosition(scene2.position);

                // 在场景2停顿5秒
                animator.Play("Idle");
                yield return new WaitForSeconds(5);

                // 从场景2走回场景1
                animator.Play("Walk");
                yield return MoveToPosition(scene1.position);

                // 在场景1停顿5秒
                animator.Play("Idle");
                yield return new WaitForSeconds(5);
            
        }

    }

    // 移动到目标位置的协程
    IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }
    }


    // 外界影响的接口
    public void AffectNPC()
    {
        isAffected = true;
        // 这里可以添加受到外界影响时的行为逻辑
    }


    // 恢复默认行为
    public void RestoreDefaultBehavior()
    {
        isAffected = false;
        StartCoroutine(DefaultBehavior());
    }

}
