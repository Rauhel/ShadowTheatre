using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;

    private GameObject currentPlayer;
    private InputManager inputManager;
    private ShadowType currentShadowType = ShadowType.None;

    // 添加ShadowType变更事件
    public event Action<ShadowType> OnShadowTypeChanged;

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

    private void Start()
    {
        // 获取InputManager引用
        inputManager = InputManager.Instance;

        // 可以在游戏开始时创建玩家
        //CreatePlayer();
    }

    // 创建玩家
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

        // 确保玩家组件能够获取InputManager
        PlayerMovement movement = currentPlayer.GetComponent<PlayerMovement>();
        if (movement != null && inputManager != null)
        {
            // 可以在这里配置玩家移动方式
        }

        return currentPlayer;
    }

    // 移除当前玩家
    public void RemovePlayer()
    {
        if (currentPlayer != null)
        {
            Destroy(currentPlayer);
            currentPlayer = null;
        }
    }

    // 获取当前玩家实例
    public GameObject GetCurrentPlayer()
    {
        return currentPlayer;
    }

    // 处理玩家死亡
    public void OnPlayerDeath()
    {
        // 处理玩家死亡逻辑
        Debug.Log("玩家死亡");

        // 可以发送事件通知其他系统
        EventCenter.Instance.Publish("PlayerDeath");
    }

    // 设置当前阴影类型
    public void SetCurrentShadowType(ShadowType type)
    {
        if (currentShadowType != type)
        {
            Debug.Log($"<color=yellow>[PlayerManager]</color> 阴影类型正在从 <color=cyan>{currentShadowType}</color> 切换到 <color=green>{type}</color>");

            currentShadowType = type;

            // 触发事件通知其他组件
            OnShadowTypeChanged?.Invoke(currentShadowType);

            // 可选：通过事件中心广播事件 - 修复参数数量
            if (EventCenter.Instance != null)
            {
                EventCenter.Instance.Publish("ShadowTypeChanged");
                Debug.Log($"<color=yellow>[PlayerManager]</color> 已通过事件中心广播阴影类型变更事件");
            }

            // 应用阴影类型变更的额外逻辑
            ApplyShadowTypeChange(type);
        }
    }

    // 应用阴影类型变更时的额外逻辑
    private void ApplyShadowTypeChange(ShadowType type)
    {
        // 这里可以添加根据不同阴影类型执行的特定逻辑
        switch (type)
        {
            case ShadowType.Bird:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>鸟</color>形态: 可能具有飞行或轻盈跳跃能力");
                break;
            case ShadowType.Deer:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>鹿</color>形态: 可能具有跳跃或奔跑能力");
                break;
            case ShadowType.Wolf:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>狼</color>形态: 可能具有攻击或嗅探能力");
                break;
            case ShadowType.Sheep:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>羊</color>形态: 可能具有跳跃或群聚能力");
                break;
            case ShadowType.Goose:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>鹅</color>形态: 可能具有游泳或鸣叫能力");
                break;
            default:
                Debug.Log($"<color=yellow>[PlayerManager]</color> 切换为<color=cyan>默认</color>形态");
                break;
        }
    }

    // 获取当前阴影类型
    public ShadowType GetCurrentShadowType()
    {
        return currentShadowType;
    }
}

