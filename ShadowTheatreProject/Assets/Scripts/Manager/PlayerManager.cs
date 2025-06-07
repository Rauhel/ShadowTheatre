using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 玩家管理器：负责管理玩家类型和状态
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;

    // 阴影类型枚举
    public enum ShadowType
    {
        None,
        Bird,   // 鸟形手势 - "bird" (双手)
        Wolf,   // 狼形手势 - "wolf" (单手)
        Fist,   // 拳头手势 - "fist" (单手)
        Goose,  // 鹅形手势 - "goose" (双手)
        Frog,   // 蛙形手势 - "frog" (双手)
        Owl     // 猫头鹰手势 - "owl" (双手)
    }

    // 手势映射配置
    [System.Serializable]
    public class GestureMapping
    {
        public string gestureType;
        public ShadowType shadowType;
    }

    [Header("手势类型映射")]
    [SerializeField]
    private List<GestureMapping> gestureMappings = new List<GestureMapping>
    {
        new GestureMapping { gestureType = "bird", shadowType = ShadowType.Bird },
        new GestureMapping { gestureType = "wolf", shadowType = ShadowType.Wolf },
        new GestureMapping { gestureType = "fist", shadowType = ShadowType.Fist },
        new GestureMapping { gestureType = "goose", shadowType = ShadowType.Goose },
        new GestureMapping { gestureType = "frog", shadowType = ShadowType.Frog },
        new GestureMapping { gestureType = "owl", shadowType = ShadowType.Owl }
    };

    [Header("键盘映射")]
    [SerializeField] private KeyCode birdKey = KeyCode.A;
    [SerializeField] private KeyCode wolfKey = KeyCode.S;
    [SerializeField] private KeyCode fistKey = KeyCode.D;
    [SerializeField] private KeyCode gooseKey = KeyCode.F;
    [SerializeField] private KeyCode frogKey = KeyCode.G;
    [SerializeField] private KeyCode owlKey = KeyCode.H;

    // 状态变量
    private ShadowType currentShadowType = ShadowType.None;
    private GameObject currentPlayer;
    private InputManager inputManager;

    // 阴影类型变更事件
    public event Action<ShadowType> OnShadowTypeChanged;

    private void Awake()
    {
        // 单例设置
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

    private void Start()
    {
        Debug.Log($"[PlayerManager] === Start开始 ===");
        
        // 获取InputManager引用
        Debug.Log($"[PlayerManager] 尝试获取InputManager实例...");
        inputManager = InputManager.Instance;
        
        if (inputManager != null)
        {
            Debug.Log($"[PlayerManager] ✓ 成功获取InputManager实例: {inputManager.name}");
            Debug.Log($"[PlayerManager] 准备订阅OnGestureTypeReceived事件...");
            
            // 订阅手势类型事件
            inputManager.OnGestureTypeReceived += HandleGestureType;
            
            Debug.Log($"[PlayerManager] ✓ 事件订阅成功");
        }
        else
        {
            Debug.LogError($"[PlayerManager] ✗ 无法获取InputManager实例!");
            
            // 尝试查找InputManager
            var inputManagerInScene = FindObjectOfType<InputManager>();
            if (inputManagerInScene != null)
            {
                Debug.Log($"[PlayerManager] 在场景中找到InputManager: {inputManagerInScene.name}");
            }
            else
            {
                Debug.LogError($"[PlayerManager] 场景中也找不到InputManager组件！");
            }
        }
        
        Debug.Log($"[PlayerManager] === Start结束 ===");
    }

    private void Update()
    {
        // 检查键盘输入
        CheckKeyboardInput();
    }

    /// <summary>
    /// 处理手势类型消息
    /// </summary>
    private void HandleGestureType(string gestureType, float confidence)
    {
        Debug.Log($"[PlayerManager] === HandleGestureType调用 === 手势: {gestureType}, 置信度: {confidence}");
        
        // 将手势类型映射到阴影类型
        ShadowType shadowType = MapGestureTypeToShadowType(gestureType);
        Debug.Log($"[PlayerManager] 映射结果: {gestureType} -> {shadowType}");

        // 如果是有效的阴影类型，则更新
        if (shadowType != ShadowType.None)
        {
            Debug.Log($"[PlayerManager] 有效阴影类型，准备更新: {shadowType}");
            UpdateShadowType(shadowType, confidence);
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] 无效阴影类型，跳过更新: {gestureType} -> {shadowType}");
        }
    }

    /// <summary>
    /// 将手势类型字符串映射到阴影类型枚举
    /// </summary>
    private ShadowType MapGestureTypeToShadowType(string gestureType)
    {
        Debug.Log($"[PlayerManager] === MapGestureTypeToShadowType === 输入: '{gestureType}'");
        
        // 转换为小写进行比较，确保大小写不敏感
        string lowerType = gestureType.ToLower();
        Debug.Log($"[PlayerManager] 转换为小写: '{lowerType}'");

        // 从配置的映射中查找匹配项
        Debug.Log($"[PlayerManager] 检查映射配置，总数: {gestureMappings.Count}");
        
        for (int i = 0; i < gestureMappings.Count; i++)
        {
            var mapping = gestureMappings[i];
            Debug.Log($"[PlayerManager] 映射 {i+1}: '{mapping.gestureType}' -> {mapping.shadowType}");
            
            if (mapping.gestureType.ToLower() == lowerType)
            {
                Debug.Log($"[PlayerManager] ✓ 找到匹配映射: '{gestureType}' -> {mapping.shadowType}");
                return mapping.shadowType;
            }
        }

        // 如果没有找到匹配项
        Debug.LogWarning($"[PlayerManager] ✗ 未找到匹配的手势映射: '{gestureType}'，使用None");
        return ShadowType.None;
    }

    /// <summary>
    /// 检查键盘输入，用于调试和开发
    /// </summary>
    private void CheckKeyboardInput()
    {
        if (Input.GetKeyDown(birdKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Bird ({birdKey})");
            UpdateShadowType(ShadowType.Bird);
        }
        else if (Input.GetKeyDown(wolfKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Wolf ({wolfKey})");
            UpdateShadowType(ShadowType.Wolf);
        }
        else if (Input.GetKeyDown(fistKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Fist ({fistKey})");
            UpdateShadowType(ShadowType.Fist);
        }
        else if (Input.GetKeyDown(gooseKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Goose ({gooseKey})");
            UpdateShadowType(ShadowType.Goose);
        }
        else if (Input.GetKeyDown(frogKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Frog ({frogKey})");
            UpdateShadowType(ShadowType.Frog);
        }
        else if (Input.GetKeyDown(owlKey))
        {
            Debug.Log($"[PlayerManager] 键盘输入: Owl ({owlKey})");
            UpdateShadowType(ShadowType.Owl);
        }
    }

    /// <summary>
    /// 更新阴影类型
    /// </summary>
    public void UpdateShadowType(ShadowType shadowType, float confidence = 1.0f, bool forceUpdate = false)
    {
        Debug.Log($"[PlayerManager] === UpdateShadowType调用 === 类型: {shadowType}, 置信度: {confidence}, 强制更新: {forceUpdate}");
        Debug.Log($"[PlayerManager] 当前阴影类型: {currentShadowType}");
        
        // 如果类型相同且不是强制更新，不需要更新
        if (currentShadowType == shadowType && !forceUpdate)
        {
            Debug.Log($"[PlayerManager] 阴影类型相同，跳过更新: {shadowType}");
            return;
        }

        // 更新阴影类型
        var previousType = currentShadowType;
        currentShadowType = shadowType;
        Debug.Log($"[PlayerManager] 阴影类型已更新: {previousType} -> {currentShadowType}");

        // 输出日志
        Debug.Log($"[PlayerManager] 阴影类型已更新: {shadowType}, 置信度: {confidence:F3}");

        try
        {
            Debug.Log($"[PlayerManager] 开始广播事件...");
            
            // 触发阴影类型变更事件
            Debug.Log($"[PlayerManager] 触发OnShadowTypeChanged事件");
            OnShadowTypeChanged?.Invoke(shadowType);

            // 通过事件中心广播变化
            if (EventCenter.Instance != null)
            {
                Debug.Log($"[PlayerManager] 发布ShadowTypeChanged事件到EventCenter");
                EventCenter.Instance.Publish("ShadowTypeChanged");

                // 发布特定阴影类型事件
                if (shadowType != ShadowType.None)
                {
                    string eventName = $"{shadowType}Detected";
                    Debug.Log($"[PlayerManager] 发布特定阴影事件: {eventName}");
                    EventCenter.Instance.Publish(eventName);
                }
            }
            else
            {
                Debug.LogWarning($"[PlayerManager] EventCenter.Instance为空，无法发布事件");
            }

            // 这里可以添加更改玩家外观或行为的代码
            Debug.Log($"[PlayerManager] 准备应用阴影类型到玩家...");
            ApplyShadowTypeToPlayer(shadowType);
            
            Debug.Log($"[PlayerManager] === UpdateShadowType完成 ===");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerManager] 处理阴影类型变更时出错: {e.Message}");
            Debug.LogError($"[PlayerManager] 错误堆栈: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 强制更新阴影类型（忽略相同状态检查）
    /// </summary>
    public void ForceUpdateShadowType(ShadowType shadowType, float confidence = 1.0f)
    {
        Debug.Log($"[PlayerManager] 强制更新阴影类型: {shadowType}");
        UpdateShadowType(shadowType, confidence, true);
    }

    /// <summary>
    /// 重置PlayerManager状态为None
    /// </summary>
    [ContextMenu("重置阴影类型为None")]
    public void ResetShadowType()
    {
        Debug.Log($"[PlayerManager] 重置阴影类型为None");
        currentShadowType = ShadowType.None;
        ApplyShadowTypeToPlayer(ShadowType.None);
    }

    /// <summary>
    /// 强制刷新当前阴影类型到外观系统
    /// </summary>
    [ContextMenu("强制刷新当前阴影类型")]
    public void RefreshCurrentShadowType()
    {
        Debug.Log($"[PlayerManager] 强制刷新当前阴影类型: {currentShadowType}");
        ApplyShadowTypeToPlayer(currentShadowType);
    }

    /// <summary>
    /// 将阴影类型应用到玩家对象
    /// </summary>
    private void ApplyShadowTypeToPlayer(ShadowType shadowType)
    {
        Debug.Log($"[PlayerManager] === ApplyShadowTypeToPlayer开始 === 类型: {shadowType}");
        
        if (currentPlayer == null)
        {
            Debug.LogWarning($"[PlayerManager] ✗ currentPlayer为空，无法应用阴影类型");
            return;
        }

        Debug.Log($"[PlayerManager] ✓ currentPlayer存在: {currentPlayer.name}");

        // 获取玩家上的相关组件并更新
        PlayerAppearance appearance = currentPlayer.GetComponent<PlayerAppearance>();
        
        if (appearance != null)
        {
            Debug.Log($"[PlayerManager] ✓ 找到PlayerAppearance组件，准备调用ChangeShadowType");
            appearance.ChangeShadowType(shadowType);
            Debug.Log($"[PlayerManager] ✓ 已调用PlayerAppearance.ChangeShadowType({shadowType})");
        }
        else
        {
            Debug.LogError($"[PlayerManager] ✗ 在玩家对象上找不到PlayerAppearance组件！");
            
            // 调试信息：显示玩家对象上的所有组件
            var allComponents = currentPlayer.GetComponents<MonoBehaviour>();
            Debug.Log($"[PlayerManager] 玩家对象上的所有MonoBehaviour组件 (共{allComponents.Length}个):");
            for (int i = 0; i < allComponents.Length; i++)
            {
                Debug.Log($"[PlayerManager] 组件 {i+1}: {allComponents[i].GetType().Name}");
            }
        }
        
        Debug.Log($"[PlayerManager] === ApplyShadowTypeToPlayer结束 ===");
    }

    /// <summary>
    /// 创建玩家
    /// </summary>
    public GameObject CreatePlayer(Vector3 position = default)
    {
        if (currentPlayer != null)
        {
            Debug.LogWarning("尝试创建玩家，但已经存在一个玩家实例");
            return currentPlayer;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("玩家预制体未设置");
            return null;
        }

        // 实例化玩家
        currentPlayer = Instantiate(playerPrefab, position, Quaternion.identity);

        // 初始化玩家组件
        PlayerMovement movement = currentPlayer.GetComponent<PlayerMovement>();
        if (movement != null && inputManager != null)
        {
            // 可以在这里配置玩家移动方式
        }

        // 如果当前有阴影类型，立即应用
        if (currentShadowType != ShadowType.None)
        {
            ApplyShadowTypeToPlayer(currentShadowType);
        }

        return currentPlayer;
    }

    /// <summary>
    /// 移除当前玩家
    /// </summary>
    public void RemovePlayer()
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
            currentPlayer = null;
        }
    }

    /// <summary>
    /// 获取当前玩家实例
    /// </summary>
    public GameObject GetCurrentPlayer()
    {
        return currentPlayer;
    }

    /// <summary>
    /// 获取当前阴影类型
    /// </summary>
    public ShadowType GetCurrentShadowType()
    {
        return currentShadowType;
    }

    /// <summary>
    /// 处理玩家死亡
    /// </summary>
    public void OnPlayerDeath()
    {
        Debug.Log("玩家死亡");
        EventCenter.Instance.Publish("PlayerDeath");
    }

    /// <summary>
    /// 清理函数
    /// </summary>
    private void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnGestureTypeReceived -= HandleGestureType;
        }
    }
}