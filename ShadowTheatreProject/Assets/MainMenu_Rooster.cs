using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu_Rooster : MonoBehaviour
{
    [Header("场景设置")]
    public string gameSceneName = "GameScene"; // 游戏场景名称，可在Inspector中设置
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    /// <summary>
    /// 开始游戏 - 跳转到指定场景
    /// </summary>
    public void StartGame()
    {
        Debug.Log($"开始游戏，跳转到场景: {gameSceneName}");
        
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError("游戏场景名称未设置！请在Inspector中设置gameSceneName");
        }
    }
    
    /// <summary>
    /// 开始游戏 - 重载方法，可指定场景名称
    /// </summary>
    /// <param name="sceneName">要跳转的场景名称</param>
    public void StartGame(string sceneName)
    {
        Debug.Log($"开始游戏，跳转到场景: {sceneName}");
        
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("场景名称为空！");
        }
    }
    
    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("退出游戏");
        
#if UNITY_EDITOR
        // 在编辑器中停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 在构建版本中退出应用程序
        Application.Quit();
#endif
    }
}
