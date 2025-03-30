using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Manager;

public class Manager : MonoBehaviour
{


    //NPC ��̬����
    [CreateAssetMenu(fileName = "NPCData", menuName = "NPC/NPC Data")]
    public class NPCData : ScriptableObject
    {
        public string npcName;
        public float moveSpeed = 2.0f;
        public AnimationClip idleAnimation;
        public AnimationClip walkAnimation;
        public AnimationClip talkAnimation;
        public Transform[] waypoints; // �ƶ�·���㣨����1������2�ȣ�
    }



    //NPC ��Ϊ�߼�
    public class NPCActionLogic : MonoBehaviour
    {
        public NPCData npcData; // ��Manager��Inspector��ֵ
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

        // �ⲿ���ã�ִ��NPC��Ĭ����Ϊ
        public void StartDefaultBehavior()
        {
            StartCoroutine(DefaultBehavior());
        }

        // ֹͣ��ǰ��Ϊ���类Manager��ͣ��
        public void StopBehavior()
        {
            StopAllCoroutines();
            PlayAnimation("Idle"); // �ص�����״̬
        }

        private IEnumerator DefaultBehavior()
        {
            while (true)
            {
                // ����״̬
                PlayAnimation("Idle");
                yield return new WaitForSeconds(3f);

                // �ƶ�����һ��·����
                if (npcData.waypoints != null && npcData.waypoints.Length > 0)
                {
                    PlayAnimation("Walk");
                    yield return MoveToWaypoint(npcData.waypoints[currentWaypointIndex].position);
                    currentWaypointIndex = (currentWaypointIndex + 1) % npcData.waypoints.Length;
                }

                // ˵��״̬
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


    //NPC ������

    public class NPCManager : MonoBehaviour
    {
        public static NPCManager Instance { get; private set; }

        [SerializeField] private List<NPCActionLogic> npcs = new List<NPCActionLogic>();
        [SerializeField] private float npcLoadInterval = 1.0f; // NPC���ؼ��

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

        // ��˳�����NPC��������Ϊ
        private IEnumerator LoadNPCsSequentially()
        {
            foreach (var npc in npcs)
            {
                npc.StartDefaultBehavior();
                yield return new WaitForSeconds(npcLoadInterval);
            }
        }

        // ��ͣ����NPC��Ϊ������鴥����
        public void PauseAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StopBehavior();
            }
        }

        // �ָ�����NPC��Ϊ
        public void ResumeAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StartDefaultBehavior();
            }
        }

        // ��̬���NPC���糡�����غ�
        public void RegisterNPC(NPCActionLogic npc)
        {
            if (!npcs.Contains(npc))
            {
                npcs.Add(npc);
            }
        }
    }
}


