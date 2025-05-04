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

    // 标记一个正在运行的事件对话序列
    private bool inEventActionSequence = false;

    // 上次触发对话的路径点索引
    private int lastPathPointIndex = -1;

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
            eventManager.OnEventCompleted += OnEventCompleted;
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
            eventManager.OnEventCompleted -= OnEventCompleted;
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
        // 获取当前路径配置
        NPCData npcData = controller.Data;
        if (npcData == null)
            return;

        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
            return;

        Debug.Log($"[{gameObject.name}] 对话管理器 - 检查路径点 {pathPointIndex} 的对话");

        // 如果有事件序列在运行，标记它为中断状态
        inEventActionSequence = false;

        // 检查是否有与此路径点关联的已完成事件
        if (eventManager != null)
        {
            // 获取所有已完成的事件，而不仅仅是与当前路径点关联的
            List<PathEvent> completedEvents = eventManager.GetAllCompletedEvents();

            // 检查每个已完成事件，查找与当前路径点匹配的动作
            foreach (var completedEvent in completedEvents)
            {
                string eventID = completedEvent.eventID;
                string completionGesture = eventManager.GetEventCompletionGesture(eventID);

                Debug.Log($"[{gameObject.name}] 检查已完成事件 {eventID} (手势={completionGesture}) 的路径点{pathPointIndex}对话");

                // 首先处理默认响应的对话 - 这总是执行
                bool defaultDialoguePlayed = false;
                if (completedEvent.defaultResponse != null)
                {
                    foreach (var action in completedEvent.defaultResponse.actions)
                    {
                        if (action.pathPointIndex == pathPointIndex &&
                            action.isActionActive &&
                            !string.IsNullOrEmpty(action.dialogueText))
                        {
                            // 显示对话
                            DisplayDialogue(
                                action.dialogueText,
                                action.displayDuration,
                                action.voiceClip,
                                action.overridePrevious
                            );
                            defaultDialoguePlayed = true;
                            Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}显示已完成事件 {eventID} 的默认响应对话");
                            break; // 只显示第一个匹配的
                        }
                    }
                }

                // 如果有特定手势响应，并且使用了该手势完成事件，执行对应对话
                if (!string.IsNullOrEmpty(completionGesture))
                {
                    foreach (var response in completedEvent.gestureResponses)
                    {
                        if (response.gestureType == completionGesture)
                        {
                            foreach (var action in response.actions)
                            {
                                if (action.pathPointIndex == pathPointIndex &&
                                    action.isActionActive &&
                                    !string.IsNullOrEmpty(action.dialogueText))
                                {
                                    if (action.overridePrevious || !defaultDialoguePlayed)
                                    {
                                        // 显示对话
                                        DisplayDialogue(
                                            action.dialogueText,
                                            action.displayDuration,
                                            action.voiceClip,
                                            true
                                        );
                                        Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}显示已完成事件 {eventID} 的手势 {completionGesture} 响应对话");
                                    }
                                    return; // 找到对应手势的动作后退出
                                }
                            }
                            break; // 已找到对应手势响应，跳出循环
                        }
                    }
                }

                // 如果已经显示了默认对话，直接返回
                if (defaultDialoguePlayed)
                    return;
            }
        }

        // 如果没有已完成事件的可用对话，查找默认路径动作中的可用对话
        foreach (var action in currentPath.pathActions)
        {
            if (action.pathPointIndex == pathPointIndex &&
                action.isActionActive &&
                !string.IsNullOrEmpty(action.dialogueText))
            {
                // 显示对话
                DisplayDialogue(
                    action.dialogueText,
                    action.displayDuration,
                    action.voiceClip,
                    action.overridePrevious
                );
                Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}显示默认路径对话: {action.dialogueText.Substring(0, Mathf.Min(20, action.dialogueText.Length))}...");
                lastPathPointIndex = pathPointIndex;
                return;
            }
        }

        // 如果没有找到任何对话，清除当前对话（如果有的话）
        if (displayCoroutine != null && currentDialogueBubble != null && currentDialogueBubble.activeSelf)
        {
            Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}没有找到对话，隐藏当前对话");
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
            currentDialogueBubble.SetActive(false);
        }
    }

    // 事件完成回调
    private void OnEventCompleted(string pathId, PathEvent completedEvent, string gestureType)
    {
        if (completedEvent == null)
            return;

        Debug.Log($"[{gameObject.name}] 对话管理器 - 事件完成回调：事件={completedEvent.eventID}，手势={gestureType}");

        // 根据手势类型查找对应的响应
        GestureResponse response = null;

        // 查找匹配的手势响应
        if (!string.IsNullOrEmpty(gestureType))
        {
            foreach (var r in completedEvent.gestureResponses)
            {
                if (r.gestureType == gestureType)
                {
                    response = r;
                    Debug.Log($"[{gameObject.name}] 找到匹配手势响应: {gestureType}");
                    break;
                }
            }
        }

        // 如果没有找到，使用默认响应
        if (response == null)
        {
            response = completedEvent.defaultResponse;
            Debug.Log($"[{gameObject.name}] 使用默认响应");
        }

        // 如果有动作序列，播放动作序列中的对话
        if (response != null && response.actions != null && response.actions.Count > 0)
        {
            // 过滤只获取当前路径点的可用动作，或没有指定路径点的动作
            int currentPathPointIndex = eventManager?.GetCurrentPathPointIndex() ?? -1;
            List<ActionData> activeActions = new List<ActionData>();

            foreach (var action in response.actions)
            {
                // 只添加当前点的动作或无特定路径点的动作
                if (action.isActionActive &&
                    (action.pathPointIndex < 0 || action.pathPointIndex == currentPathPointIndex) &&
                    !string.IsNullOrEmpty(action.dialogueText))
                {
                    activeActions.Add(action);
                }
            }

            if (activeActions.Count > 0)
            {
                Debug.Log($"[{gameObject.name}] 开始播放对话序列: {activeActions.Count}个动作(当前路径点{currentPathPointIndex})");
                inEventActionSequence = true;
                StartCoroutine(PlayActionSequenceCoroutine(activeActions));
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 事件响应中没有当前路径点({currentPathPointIndex})可用的对话");
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 事件响应中没有定义动作或对话");
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
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning($"[{gameObject.name}] 尝试显示空对话");
            return;
        }

        // 检查是否有正在显示的对话，如果有且不覆盖，则返回
        if (displayCoroutine != null && !overridePrevious)
        {
            Debug.Log($"[{gameObject.name}] 有对话正在显示且不覆盖，忽略新对话: {text.Substring(0, Mathf.Min(20, text.Length))}...");
            return;
        }

        // 停止之前的显示协程
        if (displayCoroutine != null)
        {
            StopCoroutine(displayCoroutine);
            displayCoroutine = null;
        }

        // 播放声音
        PlayDialogueVoice(voiceClip);

        // 开始新的显示协程
        displayCoroutine = StartCoroutine(DisplayDialogueCoroutine(text, duration));
    }

    // 播放对话声音
    private void PlayDialogueVoice(AudioClip voiceClip)
    {
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
    }

    // 显示对话的协程
    private IEnumerator DisplayDialogueCoroutine(string text, float duration)
    {
        if (dialogueText == null || currentDialogueBubble == null)
        {
            Debug.LogError($"[{gameObject.name}] 对话组件未正确初始化");
            yield break;
        }

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
        {
            Debug.LogWarning($"[{gameObject.name}] 尝试播放空动作序列");
            return;
        }

        inEventActionSequence = true;
        StartCoroutine(PlayActionSequenceCoroutine(actions));
    }

    // 改进的动作序列协程，支持分段等待和中断检查
    private IEnumerator PlayActionSequenceCoroutine(List<ActionData> actions)
    {
        if (actions == null || actions.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 对话动作序列为空");
            yield break;
        }

        Debug.Log($"[{gameObject.name}] 准备播放对话序列: {actions.Count}个动作");

        // 获取当前路径上的点索引
        int currentPathPointIndex = -1;
        if (eventManager != null)
        {
            currentPathPointIndex = eventManager.GetCurrentPathPointIndex();
            Debug.Log($"[{gameObject.name}] 当前路径点索引: {currentPathPointIndex}");
        }

        int actionCounter = 0;
        foreach (var action in actions)
        {
            // 如果序列被中断，提前退出
            if (!inEventActionSequence)
            {
                Debug.Log($"[{gameObject.name}] 对话序列被中断");
                yield break;
            }

            actionCounter++;
            Debug.Log($"[{gameObject.name}] 准备执行对话动作{actionCounter}: 文本={action.dialogueText?.Substring(0, Mathf.Min(20, action.dialogueText?.Length ?? 0))}..., 路径点={action.pathPointIndex}");

            // 等待延迟
            if (action.delay > 0)
            {
                Debug.Log($"[{gameObject.name}] 对话延迟: {action.delay}秒");

                // 分段等待延迟
                float remainingDelay = action.delay;
                float waitInterval = 0.1f;

                while (remainingDelay > 0 && inEventActionSequence)
                {
                    float waitTime = Mathf.Min(waitInterval, remainingDelay);
                    yield return new WaitForSeconds(waitTime);
                    remainingDelay -= waitTime;

                    if (!inEventActionSequence)
                    {
                        Debug.Log($"[{gameObject.name}] 对话序列被中断(延迟中)");
                        yield break;
                    }
                }
            }

            // 检查是否需要在当前点显示此动作的对话
            bool shouldShowDialogue = false;

            // 如果没有指定路径点，或者路径点与当前点匹配，才显示对话
            if (action.pathPointIndex < 0 || action.pathPointIndex == currentPathPointIndex)
            {
                shouldShowDialogue = true;
            }

            // 显示对话
            if (shouldShowDialogue && !string.IsNullOrEmpty(action.dialogueText))
            {
                Debug.Log($"[{gameObject.name}] 显示对话: {action.dialogueText.Substring(0, Mathf.Min(20, action.dialogueText.Length))}...");
                DisplayDialogue(
                    action.dialogueText,
                    action.displayDuration,
                    action.voiceClip,
                    action.overridePrevious
                );

                // 等待此动作完成
                if (action.displayDuration > 0)
                {
                    // 分段等待，以便可以被路径点对话打断
                    float remainingTime = action.displayDuration;
                    float waitInterval = 0.1f; // 每段等待0.1秒

                    while (remainingTime > 0 && inEventActionSequence)
                    {
                        float waitTime = Mathf.Min(waitInterval, remainingTime);
                        yield return new WaitForSeconds(waitTime);
                        remainingTime -= waitTime;

                        // 检查是否已经被新的路径点对话中断
                        if (!inEventActionSequence)
                        {
                            Debug.Log($"[{gameObject.name}] 对话序列被路径点对话中断");
                            yield break;
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 跳过对话，不在当前路径点(当前={currentPathPointIndex}, 动作定义={action.pathPointIndex})或没有指定对话");
            }
        }

        Debug.Log($"[{gameObject.name}] 对话序列播放完成");
        inEventActionSequence = false;
    }
}