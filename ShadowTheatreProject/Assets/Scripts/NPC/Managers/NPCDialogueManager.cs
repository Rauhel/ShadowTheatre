using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NPCDialogueManager : MonoBehaviour
{
    [Header("对话 UI 设置")]
    public GameObject dialoguePrefab; // 对话气泡预制体
    public Vector3 dialogueOffset = new Vector3(0, 2.0f, 0); // 对话气泡相对于 NPC 的偏移
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.2f;

    [Header("引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;

    // 当前活跃的对话气泡
    private GameObject currentDialogueBubble;
    private TextMeshProUGUI dialogueText;
    private Coroutine displayCoroutine;

    // 上次触发对话的路径点索引
    private int lastPathPointIndex = -1;

    // 当前活跃事件的对话信息
    private bool isPlayingEventOutcomeDialogue = false;
    // 删除对 PathPointDialogue 的引用，改用 ActionData
    private List<ActionData> currentEventDialogues;
    private int currentEventDialogueIndex = 0;

    private void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();

        // 初始化对话气泡
        if (dialoguePrefab != null)
        {
            currentDialogueBubble = Instantiate(dialoguePrefab, transform);
            currentDialogueBubble.transform.localPosition = dialogueOffset;
            dialogueText = currentDialogueBubble.GetComponentInChildren<TextMeshProUGUI>();
            currentDialogueBubble.SetActive(false);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 对话气泡预制体未分配！请分配一个带有 TextMeshProUGUI 组件的预制体。");
        }
    }

    private void OnEnable()
    {
        // 订阅事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached += CheckForPathDialogue;
        }

        if (eventManager != null)
        {
            eventManager.OnEventCompleted += PlayEventOutcomeDialogue;
        }
    }

    private void OnDisable()
    {
        // 取消订阅事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached -= CheckForPathDialogue;
        }

        if (eventManager != null)
        {
            eventManager.OnEventCompleted -= PlayEventOutcomeDialogue;
        }

        // 停止所有协程
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }
    }

    // 检查当前路径点是否有对话
    private void CheckForPathDialogue(int pathPointIndex)
    {
        // 如果正在播放事件对话，不处理路径对话
        if (isPlayingEventOutcomeDialogue)
            return;

        // 获取当前路径配置
        NPCData npcData = controller.Data;
        if (npcData == null)
            return;

        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
            return;

        // 检查是否有与此路径点关联的已完成事件
        if (eventManager != null)
        {
            PathEvent completedEvent = eventManager.FindCompletedEventForPathPoint(pathPointIndex);

            // 查找是否有未完成的事件与此路径点关联
            bool hasUncompletedEventForPathPoint = false;
            foreach (var evt in eventManager.CurrentPathEvents)
            {
                if (!eventManager.IsEventCompleted(evt.eventID) &&
                    eventManager.IsPathPointInEventRange(pathPointIndex, evt))
                {
                    hasUncompletedEventForPathPoint = true;
                    break;
                }
            }

            // 情况1: 有已完成的事件关联到此路径点
            if (completedEvent != null)
            {
                Debug.Log($"[{gameObject.name}] 对话管理器 - 路径点 {pathPointIndex} 有已完成的事件: {completedEvent.eventID}");

                // 寻找此事件响应中的路径点动作
                foreach (var action in completedEvent.defaultResponse.actions)
                {
                    if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.dialogueText))
                    {
                        // 显示对话
                        DisplayDialogue(
                            action.dialogueText,
                            action.displayDuration,
                            action.voiceClip,
                            action.overridePrevious
                        );

                        lastPathPointIndex = pathPointIndex;
                        return;
                    }
                }
            }
            // 情况2: 有未完成的事件关联到此路径点 - 使用默认响应
            else if (hasUncompletedEventForPathPoint)
            {
                // 查找未完成事件的默认响应
                foreach (var evt in eventManager.CurrentPathEvents)
                {
                    if (!eventManager.IsEventCompleted(evt.eventID) &&
                        eventManager.IsPathPointInEventRange(pathPointIndex, evt))
                    {
                        // 查找默认响应中的动作
                        foreach (var action in evt.defaultResponse.actions)
                        {
                            if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.dialogueText))
                            {
                                // 显示对话
                                DisplayDialogue(
                                    action.dialogueText,
                                    action.displayDuration,
                                    action.voiceClip,
                                    action.overridePrevious
                                );
                                lastPathPointIndex = pathPointIndex;
                                return;
                            }
                        }
                    }
                }

                Debug.Log($"[{gameObject.name}] 对话管理器 - 路径点 {pathPointIndex} 有未完成的事件，使用默认响应");
                return;
            }
        }

        // 情况3: 没有事件关联到此路径点，执行默认路径动作
        Debug.Log($"[{gameObject.name}] 对话管理器 - 检查路径点 {pathPointIndex} 的默认对话");

        // 查找当前路径点对应的对话
        foreach (var action in currentPath.pathActions)
        {
            if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.dialogueText))
            {
                // 显示对话
                DisplayDialogue(
                    action.dialogueText,
                    action.displayDuration,
                    action.voiceClip,
                    action.overridePrevious
                );

                lastPathPointIndex = pathPointIndex;
                return;
            }
        }
    }

    // 播放事件结果对话
    private void PlayEventOutcomeDialogue(string pathId, PathEvent completedEvent, string gestureType)
    {
        if (completedEvent == null)
            return;

        // 获取匹配的响应
        GestureResponse response = null;

        if (!string.IsNullOrEmpty(gestureType))
        {
            foreach (var r in completedEvent.gestureResponses)
            {
                if (r.gestureType == gestureType)
                {
                    response = r;
                    break;
                }
            }
        }

        // 如果没有找到响应，使用默认响应
        if (response == null)
        {
            response = completedEvent.defaultResponse;
        }

        // 如果响应包含动作，播放动作序列中的对话
        if (response != null && response.actions != null && response.actions.Count > 0)
        {
            // 使用 ActionData 类型
            StartCoroutine(PlayActionSequenceCoroutine(response.actions));
        }
    }

    // 基本版本 - 只有文本和持续时间
    public void DisplayDialogue(string text, float duration)
    {
        DisplayDialogue(text, duration, null, true);
    }

    // 带覆盖选项的版本
    public void DisplayDialogue(string text, float duration, bool overridePrevious)
    {
        DisplayDialogue(text, duration, null, overridePrevious);
    }

    // 完整版本 - 所有参数
    public void DisplayDialogue(string text, float duration, AudioClip voiceClip, bool overridePrevious)
    {
        // 检查是否有正在显示的对话，如果有且不覆盖，则返回
        if (displayCoroutine != null && !overridePrevious)
            return;

        // 停止之前的显示协程
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        // 播放声音
        if (voiceClip != null && SoundManager.Instance != null)
        {
            // 检查声音是否已经添加到SoundManager
            int sfxIndex = SoundManager.Instance.MySFXList.IndexOf(voiceClip);

            if (sfxIndex >= 0)
            {
                // 如果已经存在，直接播放
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, gameObject, 1.0f);
            }
            else
            {
                // 如果不存在，添加到列表并播放
                SoundManager.Instance.MySFXList.Add(voiceClip);
                sfxIndex = SoundManager.Instance.MySFXList.Count - 1;
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, gameObject, 1.0f);
            }
        }

        // 开始新的显示协程
        displayCoroutine = StartCoroutine(DisplayDialogueCoroutine(text, duration));
    }

    // 显示对话的协程
    private IEnumerator DisplayDialogueCoroutine(string text, float duration)
    {
        if (dialogueText == null || currentDialogueBubble == null)
            yield break;

        // 设置文本
        dialogueText.text = text;

        // 淡入
        currentDialogueBubble.SetActive(true);
        CanvasGroup canvasGroup = currentDialogueBubble.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float startTime = Time.time;

            while (Time.time < startTime + fadeInTime)
            {
                canvasGroup.alpha = (Time.time - startTime) / fadeInTime;
                yield return null;
            }

            canvasGroup.alpha = 1;
        }

        // 等待显示时间
        yield return new WaitForSeconds(duration);

        // 如果是事件对话序列，检查是否有下一条
        if (isPlayingEventOutcomeDialogue)
        {
            currentEventDialogueIndex++;

            if (currentEventDialogueIndex < currentEventDialogues.Count)
            {
                // 继续显示下一条对话，使用 ActionData
                DisplayDialogue(
                    currentEventDialogues[currentEventDialogueIndex].dialogueText,
                    currentEventDialogues[currentEventDialogueIndex].displayDuration,
                    currentEventDialogues[currentEventDialogueIndex].voiceClip,
                    true
                );
                yield break;
            }
            else
            {
                // 对话序列结束
                isPlayingEventOutcomeDialogue = false;
            }
        }

        // 淡出
        if (canvasGroup != null)
        {
            float startTime = Time.time;

            while (Time.time < startTime + fadeOutTime)
            {
                canvasGroup.alpha = 1 - (Time.time - startTime) / fadeOutTime;
                yield return null;
            }

            canvasGroup.alpha = 0;
        }

        currentDialogueBubble.SetActive(false);
        displayCoroutine = null;
    }

    // 添加播放动作序列的方法
    public void PlayActionSequence(List<ActionData> actions)
    {
        if (actions == null || actions.Count == 0)
            return;

        StartCoroutine(PlayActionSequenceCoroutine(actions));
    }

    private IEnumerator PlayActionSequenceCoroutine(List<ActionData> actions)
    {
        foreach (var action in actions)
        {
            // 等待延迟
            if (action.delay > 0)
            {
                yield return new WaitForSeconds(action.delay);
            }

            // 显示对话并播放声音
            DisplayDialogue(
                action.dialogueText,
                action.displayDuration,
                action.voiceClip,
                action.overridePrevious
            );

            // 等待此动作完成
            yield return new WaitForSeconds(action.displayDuration);
        }
    }
}