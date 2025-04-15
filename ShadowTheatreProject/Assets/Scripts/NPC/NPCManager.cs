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


    //���ȶ��������Ϊ�ڵ�
    public abstract class NPCBehaviorNode
    {
        public abstract NPCBehaviorStatus Execute();
    }

    public enum NPCBehaviorStatus
    {
        Running,
        Success,
        Failure
    }



    //ʵ�־�����Ϊ�ڵ�����

    // ˳��ִ�нڵ�
    public class SequenceNode : NPCBehaviorNode
    {
        private List<NPCBehaviorNode> children = new List<NPCBehaviorNode>();
        private int currentChildIndex = 0;

        public SequenceNode(List<NPCBehaviorNode> behaviors)
        {
            children = behaviors;
        }

        public override NPCBehaviorStatus Execute()
        {
            if (currentChildIndex >= children.Count)
            {
                currentChildIndex = 0;
                return NPCBehaviorStatus.Success;
            }

            var status = children[currentChildIndex].Execute();
            if (status == NPCBehaviorStatus.Success)
            {
                currentChildIndex++;
                return NPCBehaviorStatus.Running;
            }

            return status;
        }
    }

    // �����ڵ�
    public class ConditionNode : NPCBehaviorNode
    {
        public delegate bool ConditionDelegate();
        private ConditionDelegate condition;

        public ConditionNode(ConditionDelegate condition)
        {
            this.condition = condition;
        }

        public override NPCBehaviorStatus Execute()
        {
            return condition() ? NPCBehaviorStatus.Success : NPCBehaviorStatus.Failure;
        }
    }

    // �����ڵ�
    public class ActionNode : NPCBehaviorNode
    {
        public delegate NPCBehaviorStatus ActionDelegate();
        private ActionDelegate action;

        public ActionNode(ActionDelegate action)
        {
            this.action = action;
        }

        public override NPCBehaviorStatus Execute()
        {
            return action();
        }
    }



    //NPC ��Ϊ�߼�
    public class NPCActionLogic : MonoBehaviour
    {
        [System.Serializable]
        public class BehaviorSequence
        {
            public string sequenceName;
            public List<BehaviorAction> actions = new List<BehaviorAction>();
        }

        [System.Serializable]
        public class BehaviorAction
        {
            public string actionName;
            public float duration;
            public AnimationClip animation;
            public Vector3 moveToPosition;
            public bool waitForCondition;
            public ConditionType conditionType;
        }

        public enum ConditionType
        {
            NPCState,
            PlayerGesture,
            GameState
        }

        public NPCData npcData;
        public List<BehaviorSequence> behaviorSequences = new List<BehaviorSequence>();

        private Animator animator;
        private NPCBehaviorNode behaviorTree;
        private int currentSequenceIndex = 0;

        void Start()
        {
            animator = GetComponent<Animator>();
            InitializeBehaviorTree();
        }

        void Update()
        {
            if (behaviorTree != null)
            {
                behaviorTree.Execute();
            }
        }

        private void InitializeBehaviorTree()
        {
            // Ϊÿ����Ϊ���д����ڵ�
            var sequenceNodes = new List<NPCBehaviorNode>();

            foreach (var sequence in behaviorSequences)
            {
                var sequenceActions = new List<NPCBehaviorNode>();

                foreach (var action in sequence.actions)
                {
                    // ���������ڵ�
                    var actionNode = new ActionNode(() => {
                        return ExecuteAction(action);
                    });

                    // ��������������������ڵ�
                    if (action.waitForCondition)
                    {
                        var conditionNode = new ConditionNode(() => {
                            return CheckCondition(action.conditionType);
                        });

                        sequenceActions.Add(conditionNode);
                    }

                    sequenceActions.Add(actionNode);
                }

                sequenceNodes.Add(new SequenceNode(sequenceActions));
            }

            behaviorTree = new SequenceNode(sequenceNodes);
        }

        private NPCBehaviorStatus ExecuteAction(BehaviorAction action)
        {
            // ���Ŷ���
            if (action.animation != null && animator != null)
            {
                animator.Play(action.animation.name);
            }

            // �ƶ��߼�
            if (action.moveToPosition != Vector3.zero)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    action.moveToPosition,
                    npcData.moveSpeed * Time.deltaTime);

                if (Vector3.Distance(transform.position, action.moveToPosition) > 0.1f)
                {
                    return NPCBehaviorStatus.Running;
                }
            }

            // �ȴ�ʱ��
            if (action.duration > 0)
            {
                // ����򻯴���ʵ��Ӧ����Э�̻��ʱ��
                return NPCBehaviorStatus.Running;
            }

            return NPCBehaviorStatus.Success;
        }

        private bool CheckCondition(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.NPCState:
                    // �����ΧNPC״̬
                    return CheckNPCStates();

                case ConditionType.PlayerGesture:

                    // ����������
                    return InputManager.Instance.IsGestureValid(InputManager.Instance.CurrentGesture.position);
                case ConditionType.GameState:
                    // �����Ϸ״̬
                    return GameState.Instance.GetCurrentState() == GameState.State.GamePaused; // ʾ��������Ƿ�Ϊ��ͣ״̬
                default:
                    return false;
            }
        }

        private bool CheckNPCStates()
        {
            // ʵ�ּ����ΧNPC״̬���߼�
            return true;
        }

        // �������������ⲿ������Ϊ
        public void StartBehaviorSequence(int sequenceIndex)
        {
            if (sequenceIndex >= 0 && sequenceIndex < behaviorSequences.Count)
            {
                currentSequenceIndex = sequenceIndex;
                // ����������������Ϊ����ֱ���л���ָ������
            }
        }

        public void StopCurrentBehavior()
        {
            // ֹͣ��ǰ��Ϊ
            if (animator != null)
            {
                animator.Play("Idle");
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
                npc.StartBehaviorSequence(0); // �޸�Ϊ���庯������������һ����Ϊ����
                yield return new WaitForSeconds(npcLoadInterval);
            }
        }

        // ��ͣ����NPC��Ϊ������鴥����
        public void PauseAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StopCurrentBehavior(); // �޸�Ϊ���庯����
            }
        }

        // �ָ�����NPC��Ϊ
        public void ResumeAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StartBehaviorSequence(0);
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


