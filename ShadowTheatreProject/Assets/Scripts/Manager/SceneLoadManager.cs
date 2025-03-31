using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoadManager : MonoBehaviour
{
    public static SceneLoadManager Instance { get; private set; }

    [Header("加载画面设置")]
    [SerializeField] private GameObject loadingScreenPrefab;
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("加载选项")]
    [SerializeField] private bool useAsyncLoading = true;
    [SerializeField] private bool saveGameBeforeLoading = false;

    [Header("默认交互设置")]
    [Tooltip("默认是否在进入触发器时自动加载")]
    [SerializeField] private bool defaultLoadOnTriggerEnter = true;

    [Tooltip("默认是否需要按键激活")]
    [SerializeField] private bool defaultRequireKeyPress = false;

    [Tooltip("默认激活按键")]
    [SerializeField] private KeyCode defaultActivationKey = KeyCode.E;

    [Tooltip("默认交互提示文本")]
    [SerializeField] private string defaultPromptText = "按 E 键进入下一个场景";

    // 内部引用
    private GameObject loadingScreenInstance;
    private Slider progressBar;
    private CanvasGroup fadeCanvasGroup;

    // 当前加载操作
    private AsyncOperation currentLoadOperation;
    private bool isLoading = false;

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// 获取默认的自动加载设置
    /// </summary>
    public bool GetDefaultLoadOnTriggerEnter()
    {
        return defaultLoadOnTriggerEnter;
    }

    /// <summary>
    /// 获取默认的按键需求设置
    /// </summary>
    public bool GetDefaultRequireKeyPress()
    {
        return defaultRequireKeyPress;
    }

    /// <summary>
    /// 获取默认的激活按键
    /// </summary>
    public KeyCode GetDefaultActivationKey()
    {
        return defaultActivationKey;
    }

    /// <summary>
    /// 获取默认的提示文本
    /// </summary>
    public string GetDefaultPromptText()
    {
        return defaultPromptText;
    }

    /// <summary>
    /// 加载指定场景
    /// </summary>
    /// <param name="sceneName">要加载的场景名称</param>
    /// <param name="showLoadingScreen">是否显示加载画面</param>
    public void LoadScene(string sceneName, bool showLoadingScreen = true)
    {
        Debug.Log($"尝试加载场景: {sceneName}, 显示加载画面: {showLoadingScreen}");

        // 防止重复加载
        if (isLoading)
        {
            Debug.LogWarning("已有场景正在加载中，请等待当前加载完成");
            return;
        }

        // 检查场景是否存在
        if (!IsSceneValid(sceneName))
        {
            Debug.LogError($"场景 {sceneName} 不存在或未添加到构建设置中");
            return;
        }

        Debug.Log($"开始加载场景: {sceneName}");

        // 保存游戏状态（如果需要）
        if (saveGameBeforeLoading)
        {
            SaveGameState();
        }

        // 开始加载
        if (useAsyncLoading && showLoadingScreen)
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }
        else
        {
            // 简单直接加载，适用于小场景或不需要加载画面的情况
            if (showLoadingScreen)
            {
                ShowLoadingScreen();
            }
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// 异步加载场景并显示进度
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        isLoading = true;

        // 创建加载画面
        ShowLoadingScreen();

        // 淡入加载画面
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvas(fadeCanvasGroup, 0, 1));
        }

        // 延迟一帧，确保UI已经完全显示
        yield return null;

        // 开始异步加载
        currentLoadOperation = SceneManager.LoadSceneAsync(sceneName);

        // 防止加载完成后自动激活场景
        currentLoadOperation.allowSceneActivation = false;

        // 等待加载进度达到90%
        float targetProgress = 0.9f;
        while (currentLoadOperation.progress < targetProgress)
        {
            // 更新进度条
            if (progressBar != null)
            {
                // 将0-0.9的进度映射到0-1
                float displayProgress = currentLoadOperation.progress / targetProgress;
                progressBar.value = displayProgress;
            }

            yield return null;
        }

        // 将进度条设为100%
        if (progressBar != null)
        {
            progressBar.value = 1.0f;
        }

        // 等待一小段时间，让用户看到100%进度
        yield return new WaitForSeconds(0.5f);

        // 淡出加载画面
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvas(fadeCanvasGroup, 1, 0));
        }

        // 激活场景
        currentLoadOperation.allowSceneActivation = true;

        // 清理
        if (loadingScreenInstance != null)
        {
            Destroy(loadingScreenInstance);
        }

        isLoading = false;
        currentLoadOperation = null;
    }

    /// <summary>
    /// 显示加载画面
    /// </summary>
    private void ShowLoadingScreen()
    {
        if (loadingScreenPrefab == null)
        {
            Debug.LogWarning("未设置加载画面预制体");
            return;
        }

        // 实例化加载画面
        loadingScreenInstance = Instantiate(loadingScreenPrefab);

        // 确保加载画面不会被销毁
        DontDestroyOnLoad(loadingScreenInstance);

        // 查找进度条和淡入淡出组件
        progressBar = loadingScreenInstance.GetComponentInChildren<Slider>();
        fadeCanvasGroup = loadingScreenInstance.GetComponent<CanvasGroup>();

        if (progressBar == null)
        {
            Debug.LogWarning("加载画面中未找到Slider组件，无法显示进度");
        }

        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("加载画面中未找到CanvasGroup组件，无法实现淡入淡出效果");
        }
        else
        {
            // 初始设置为透明
            fadeCanvasGroup.alpha = 0;
        }
    }

    /// <summary>
    /// 通用画布淡入淡出方法
    /// </summary>
    private IEnumerator FadeCanvas(CanvasGroup canvasGroup, float startAlpha, float endAlpha)
    {
        float elapsedTime = 0;

        // 设置初始透明度
        canvasGroup.alpha = startAlpha;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
            yield return null;
        }

        // 确保最终值精确
        canvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// 验证场景是否存在于构建设置中
    /// </summary>
    private bool IsSceneValid(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("场景名称为空!");
            return false;
        }

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            int lastSlash = path.LastIndexOf('/');
            string name = path.Substring(lastSlash + 1, path.LastIndexOf('.') - lastSlash - 1);

            if (name == sceneName)
            {
                Debug.Log($"验证场景名称: {sceneName} 存在于构建设置中");
                return true;
            }
        }

        Debug.LogError($"验证场景名称失败: {sceneName} 不在构建设置中");
        return false;
    }

    /// <summary>
    /// 保存游戏状态
    /// </summary>
    private void SaveGameState()
    {
        Debug.Log("保存游戏状态");

        // 通过事件系统通知其他系统保存状态
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Publish("SaveGameState");
        }
    }

    /// <summary>
    /// 中断当前加载并返回主菜单
    /// </summary>
    public void CancelLoading()
    {
        if (!isLoading)
            return;

        StopAllCoroutines();

        if (loadingScreenInstance != null)
        {
            Destroy(loadingScreenInstance);
        }

        // 加载主菜单
        SceneManager.LoadScene("MainMenu");

        isLoading = false;
        currentLoadOperation = null;
    }

    /// <summary>
    /// 检查是否正在加载
    /// </summary>
    public bool IsLoadingInProgress()
    {
        return isLoading;
    }

    /// <summary>
    /// 显示交互提示
    /// </summary>
    public void ShowInteractionPrompt(string promptText)
    {
        Debug.Log($"显示交互提示: {promptText}");
        // 暂时注释掉UI相关部分
        // UIManager uiManager = FindObjectOfType<UIManager>();
        // if (uiManager != null)
        // {
        //     uiManager.ShowPrompt(promptText);
        // }
        // else
        // 简单控制台提示
        Debug.Log(promptText);
        // }
    }

    /// <summary>
    /// 隐藏交互提示
    /// </summary>
    public void HideInteractionPrompt()
    {
        // 暂时注释掉UI相关部分
        // UIManager uiManager = FindObjectOfType<UIManager>();
        // if (uiManager != null)
        // {
        //     uiManager.HidePrompt();
        // }
    }

    /// <summary>
    /// 处理触发器进入事件
    /// </summary>
    public void HandleTriggerEnter(string sceneName, bool useCustomSettings, bool customLoadOnEnter,
                                  bool customRequireKeyPress, string customPromptText)
    {
        Debug.Log($"HandleTriggerEnter - 场景: {sceneName}");
        Debug.Log($"参数: 自定义设置={useCustomSettings}, 自动加载={customLoadOnEnter}, 需要按键={customRequireKeyPress}");
        Debug.Log($"默认设置: 自动加载={defaultLoadOnTriggerEnter}, 需要按键={defaultRequireKeyPress}");

        // 计算条件结果
        bool shouldAutoLoad = (useCustomSettings && customLoadOnEnter && !customRequireKeyPress) ||
                              (!useCustomSettings && defaultLoadOnTriggerEnter && !defaultRequireKeyPress);

        bool shouldShowPrompt = (useCustomSettings && customRequireKeyPress) ||
                               (!useCustomSettings && defaultRequireKeyPress);

        Debug.Log($"计算结果: 自动加载={shouldAutoLoad}, 显示提示={shouldShowPrompt}");

        // 如果应该自动加载，无需按键
        if (shouldAutoLoad)
        {
            Debug.Log($"条件满足，立即加载场景: {sceneName}");
            LoadScene(sceneName);
            return;
        }

        // 如果需要按键，显示提示
        if (shouldShowPrompt)
        {
            string prompt = useCustomSettings ? customPromptText : defaultPromptText;
            Debug.Log($"显示提示: {prompt}");
            ShowInteractionPrompt(prompt);
        }
    }

    /// <summary>
    /// 处理触发器退出事件
    /// </summary>
    public void HandleTriggerExit()
    {
        HideInteractionPrompt();
    }
}