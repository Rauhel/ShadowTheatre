using UnityEngine;

public class Managers : MonoBehaviour
{
    private void Awake()
    {
        // 确保父级物体在场景切换时不被销毁
        DontDestroyOnLoad(gameObject);
    }
}