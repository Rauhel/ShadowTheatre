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

        // 检查是否有与此路径点关联的已完成事件
        if (eventManager != null)
        {
            // 获取与此路径点关联的已完成事件
            PathEvent completedEvent = eventManager.FindCompletedEventForPathPoint(pathPointIndex);

            if (completedEvent != null)
            {
                string eventID = completedEvent.eventID;
                string completionGesture = eventManager.GetEventCompletionGesture(eventID);

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
                lastPathPointIndex = pathPointIndex;
                return;
            }
        }
    }

    // 事件完成回调
    private void OnEventCompleted(string pathId, PathEvent completedEvent, string gestureType)
    {
        if (completedEvent == null)
            return;

        Debug.Log($"[{gameObject.name}] 对话管理器 - 事件完成回调：事件={completedEvent.eventID}，手势={gestureType}");

        // 准备要显示的动作列表
        List<ActionData> actionsToPlay = new List<ActionData>();

        // 首先处理默认响应的对话 - 这总是执行
        if (completedEvent.defaultResponse != null)
        {
            foreach (var action in completedEvent.defaultResponse.actions)
            {
                if (action.isActionActive && !string.IsNullOrEmpty(action.dialogueText))
                {
                    actionsToPlay.Add(action);
                }
            }
        }

        // 如果有特定手势响应且使用的是该手势，执行对应对话
        if (!string.IsNullOrEmpty(gestureType))
        {
            GestureResponse gestureResponse = null;

            foreach (var response in completedEvent.gestureResponses)
            {
                if (response.gestureType == gestureType)
                {
                    gestureResponse = response;
                    break;
                }
            }

            if (gestureResponse != null && gestureResponse.actions != null)
            {
                foreach (var action in gestureResponse.actions)
                {
                    if (action.isActionActive && !string.IsNullOrEmpty(action.dialogueText))
                    {
                        // 检查是否覆盖默认响应
                        if (action.overridePrevious)
                        {
                            // 如果要覆盖，清空之前的所有动作
                            actionsToPlay.Clear();
                        }
                        actionsToPlay.Add(action);
                    }
                }
            }
        }

        // 如果有要播放的动作，启动序列
        if (actionsToPlay.Count > 0)
        {
            StartCoroutine(PlayActionSequenceCoroutine(actionsToPlay));
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
                Debug.Log($"[{gameObject.name}] 对话延迟: {action.delay}秒");
                yield return new WaitForSeconds(action.delay);
            }

            // 显示对话并播放声音
            if (!string.IsNullOrEmpty(action.dialogueText))
            {
                Debug.Log($"[{gameObject.name}] 显示对话: {action.dialogueText}");
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
}