using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Manager;

public class Manager : MonoBehaviour
{


    //NPC 静态数据
    [CreateAssetMenu(fileName = "NPCData", menuName = "NPC/NPC Data")]
    public class NPCData : ScriptableObject
    {
        public string npcName;
        public float moveSpeed = 2.0f;
        public AnimationClip idleAnimation;
        public AnimationClip walkAnimation;
        public AnimationClip talkAnimation;
        public Transform[] waypoints; // 移动路径点（场景1、场景2等）
    }



    //NPC 行为逻辑
    public class NPCActionLogic : MonoBehaviour
    {
        public NPCData npcData; // 由Manager或Inspector赋值
        private Animator animator;
        private int currentWaypointIndex = 0;

        void Start()
        {
            animator = GetComponent<Animator>();
            if (npcData == null)
            {
                Debug.LogError("NPCData is not assigned!");
                return;
            }
        }

        // 外部调用，执行NPC的默认行为
        public void StartDefaultBehavior()
        {
            StartCoroutine(DefaultBehavior());
        }

        // 停止当前行为（如被Manager暂停）
        public void StopBehavior()
        {
            StopAllCoroutines();
            PlayAnimation("Idle"); // 回到空闲状态
        }

        private IEnumerator DefaultBehavior()
        {
            while (true)
            {
                // 空闲状态
                PlayAnimation("Idle");
                yield return new WaitForSeconds(3f);

                // 移动到下一个路径点
                if (npcData.waypoints != null && npcData.waypoints.Length > 0)
                {
                    PlayAnimation("Walk");
                    yield return MoveToWaypoint(npcData.waypoints[currentWaypointIndex].position);
                    currentWaypointIndex = (currentWaypointIndex + 1) % npcData.waypoints.Length;
                }

                // 说话状态
                PlayAnimation("Talk");
                yield return new WaitForSeconds(2f);
            }
        }

        private IEnumerator MoveToWaypoint(Vector3 targetPosition)
        {
            while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    npcData.moveSpeed * Time.deltaTime
                );
                yield return null;
            }
        }

        private void PlayAnimation(string animationName)
        {
            if (animator != null)
            {
                animator.Play(animationName);
            }
        }
    }


    //NPC 管理器

    public class NPCManager : MonoBehaviour
    {
        public static NPCManager Instance { get; private set; }

        [SerializeField] private List<NPCActionLogic> npcs = new List<NPCActionLogic>();
        [SerializeField] private float npcLoadInterval = 1.0f; // NPC加载间隔

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }
        }

        private void Start()
        {
            StartCoroutine(LoadNPCsSequentially());
        }

        // 按顺序加载NPC并启动行为
        private IEnumerator LoadNPCsSequentially()
        {
            foreach (var npc in npcs)
            {
                npc.StartDefaultBehavior();
                yield return new WaitForSeconds(npcLoadInterval);
            }
        }

        // 暂停所有NPC行为（如剧情触发）
        public void PauseAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StopBehavior();
            }
        }

        // 恢复所有NPC行为
        public void ResumeAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StartDefaultBehavior();
            }
        }

        // 动态添加NPC（如场景加载后）
        public void RegisterNPC(NPCActionLogic npc)
        {
            if (!npcs.Contains(npc))
            {
                npcs.Add(npc);
            }
        }
    }
}


