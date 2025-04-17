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
        Bird,   // 鸟形手势 - "bird"
        Deer,   // 鹿形手势 - "deer" 
        Wolf,   // 狼形手势 - "wolf"
        Sheep,  // 羊形手势 - "sheep"
        Goose   // 鹅形手势 - "goose"
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
        new GestureMapping { gestureType = "deer", shadowType = ShadowType.Deer },
        new GestureMapping { gestureType = "wolf", shadowType = ShadowType.Wolf },
        new GestureMapping { gestureType = "sheep", shadowType = ShadowType.Sheep },
        new GestureMapping { gestureType = "goose", shadowType = ShadowType.Goose }
    };

    [Header("键盘映射")]
    [SerializeField] private KeyCode birdKey = KeyCode.A;
    [SerializeField] private KeyCode deerKey = KeyCode.S;
    [SerializeField] private KeyCode wolfKey = KeyCode.D;
    [SerializeField] private KeyCode sheepKey = KeyCode.F;
    [SerializeField] private KeyCode gooseKey = KeyCode.G;

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
        // 获取InputManager引用
        inputManager = InputManager.Instance;
        if (inputManager != null)
        {
            Debug.Log("PlayerManager: 订阅InputManager.OnGestureTypeReceived事件");
            // 订阅手势类型事件
            inputManager.OnGestureTypeReceived += HandleGestureType;
        }
        else
        {
            Debug.LogError("无法获取InputManager实例!");
        }
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
        Debug.Log($"PlayerManager: 收到手势类型 {gestureType}, 置信度: {confidence}");
        // 将手势类型映射到阴影类型
        ShadowType shadowType = MapGestureTypeToShadowType(gestureType);

        // 如果是有效的阴影类型，则更新
        if (shadowType != ShadowType.None)
        {
            Debug.Log($"PlayerManager: 映射手势类型 {gestureType} 到阴影类型 {shadowType}");
            UpdateShadowType(shadowType, confidence);
        }
    }

    /// <summary>
    /// 将手势类型字符串映射到阴影类型枚举
    /// </summary>
    private ShadowType MapGestureTypeToShadowType(string gestureType)
    {
        // 转换为小写进行比较，确保大小写不敏感
        string lowerType = gestureType.ToLower();

        // 从配置的映射中查找匹配项
        foreach (var mapping in gestureMappings)
        {
            if (mapping.gestureType.ToLower() == lowerType)
            {
                return mapping.shadowType;
            }
        }

        // 如果没有找到匹配项
        Debug.LogWarning($"未知的手势类型: {gestureType}，使用None");
        return ShadowType.None;
    }

    /// <summary>
    /// 检查键盘输入，用于调试和开发
    /// </summary>
    private void CheckKeyboardInput()
    {
        if (Input.GetKeyDown(birdKey))
            UpdateShadowType(ShadowType.Bird);
        else if (Input.GetKeyDown(deerKey))
            UpdateShadowType(ShadowType.Deer);
        else if (Input.GetKeyDown(wolfKey))
            UpdateShadowType(ShadowType.Wolf);
        else if (Input.GetKeyDown(sheepKey))
            UpdateShadowType(ShadowType.Sheep);
        else if (Input.GetKeyDown(gooseKey))
            UpdateShadowType(ShadowType.Goose);
    }

    /// <summary>
    /// 更新阴影类型
    /// </summary>
    public void UpdateShadowType(ShadowType shadowType, float confidence = 1.0f)
    {
        // 如果类型相同，不需要更新
        if (currentShadowType == shadowType) return;

        // 更新阴影类型
        currentShadowType = shadowType;

        // 输出日志
        Debug.Log($"[PlayerManager] 阴影类型已更新: {shadowType}, 置信度: {confidence:F3}");

        try
        {
            // 触发阴影类型变更事件
            OnShadowTypeChanged?.Invoke(shadowType);

            // 通过事件中心广播变化
            EventCenter.Instance.Publish("ShadowTypeChanged");

            // 发布特定阴影类型事件
            if (shadowType != ShadowType.None)
            {
                EventCenter.Instance.Publish($"{shadowType}Detected");
                Debug.Log($"[PlayerManager] 发布{shadowType}阴影事件");
            }

            // 这里可以添加更改玩家外观或行为的代码
            ApplyShadowTypeToPlayer(shadowType);
        }
        catch (Exception e)
        {
            Debug.LogError($"处理阴影类型变更时出错: {e.Message}");
        }
    }

    /// <summary>
    /// 将阴影类型应用到玩家对象
    /// </summary>
    private void ApplyShadowTypeToPlayer(ShadowType shadowType)
    {
        if (currentPlayer == null) return;

        // 获取玩家上的相关组件并更新
        PlayerAppearance appearance = currentPlayer.GetComponent<PlayerAppearance>();
        if (appearance != null)
        {
            appearance.ChangeShadowType(shadowType);
        }
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