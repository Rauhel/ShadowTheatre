using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }
    
    [SerializeField] private GameObject playerPrefab;
    
    private GameObject currentPlayer;
    private InputManager inputManager;
    
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
}

