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


    //首先定义基础行为节点
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



    //实现具体行为节点类型

    // 顺序执行节点
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

    // 条件节点
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

    // 动作节点
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



    //NPC 行为逻辑
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
            // 为每个行为序列创建节点
            var sequenceNodes = new List<NPCBehaviorNode>();

            foreach (var sequence in behaviorSequences)
            {
                var sequenceActions = new List<NPCBehaviorNode>();

                foreach (var action in sequence.actions)
                {
                    // 创建动作节点
                    var actionNode = new ActionNode(() => {
                        return ExecuteAction(action);
                    });

                    // 如果有条件，创建条件节点
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
            // 播放动画
            if (action.animation != null && animator != null)
            {
                animator.Play(action.animation.name);
            }

            // 移动逻辑
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

            // 等待时间
            if (action.duration > 0)
            {
                // 这里简化处理，实际应该用协程或计时器
                return NPCBehaviorStatus.Running;
            }

            return NPCBehaviorStatus.Success;
        }

        private bool CheckCondition(ConditionType type)
        {
            switch (type)
            {
                case ConditionType.NPCState:
                    // 检查周围NPC状态
                    return CheckNPCStates();

                case ConditionType.PlayerGesture:

                    // 检查玩家手势
                    return InputManager.Instance.IsGestureValid(InputManager.Instance.CurrentGesture.position);
                case ConditionType.GameState:
                    // 检查游戏状态
                    return GameState.Instance.GetCurrentState() == GameState.State.GamePaused; // 示例：检查是否为暂停状态
                default:
                    return false;
            }
        }

        private bool CheckNPCStates()
        {
            // 实现检查周围NPC状态的逻辑
            return true;
        }

        // 公共方法用于外部触发行为
        public void StartBehaviorSequence(int sequenceIndex)
        {
            if (sequenceIndex >= 0 && sequenceIndex < behaviorSequences.Count)
            {
                currentSequenceIndex = sequenceIndex;
                // 可以在这里重置行为树或直接切换到指定序列
            }
        }

        public void StopCurrentBehavior()
        {
            // 停止当前行为
            if (animator != null)
            {
                animator.Play("Idle");
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
                npc.StartBehaviorSequence(0); // 修改为具体函数名，启动第一个行为序列
                yield return new WaitForSeconds(npcLoadInterval);
            }
        }

        // 暂停所有NPC行为（如剧情触发）
        public void PauseAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StopCurrentBehavior(); // 修改为具体函数名
            }
        }

        // 恢复所有NPC行为
        public void ResumeAllNPCs()
        {
            foreach (var npc in npcs)
            {
                npc.StartBehaviorSequence(0);
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


