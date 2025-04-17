using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 玩家外观组件：负责处理玩家的外观变化
/// </summary>
public class PlayerAppearance : MonoBehaviour
{
    [System.Serializable]
    public class ShadowTypeVisual
    {
        public PlayerManager.ShadowType shadowType;
        public GameObject visualPrefab;
    }

    [Header("Shadow Type Visuals")]
    [SerializeField] private List<ShadowTypeVisual> shadowTypeVisuals = new List<ShadowTypeVisual>();

    [Header("Default Appearance")]
    [SerializeField] private GameObject defaultVisual;

    // 当前激活的视觉对象
    private GameObject currentVisual;

    private void Start()
    {
        // 初始显示默认外观
        if (defaultVisual != null)
        {
            currentVisual = defaultVisual;
        }
    }

    /// <summary>
    /// 根据阴影类型更改玩家外观
    /// </summary>
    public void ChangeShadowType(PlayerManager.ShadowType shadowType)
    {
        // 查找匹配的视觉对象
        GameObject newVisual = null;
        foreach (var typeVisual in shadowTypeVisuals)
        {
            if (typeVisual.shadowType == shadowType)
            {
                newVisual = typeVisual.visualPrefab;
                break;
            }
        }

        // 如果没有找到匹配项，使用默认外观
        if (newVisual == null)
        {
            newVisual = defaultVisual;
            Debug.LogWarning($"未找到阴影类型 {shadowType} 的视觉对象，使用默认外观");
        }

        // 替换当前外观
        if (currentVisual != newVisual)
        {
            // 禁用当前外观
            if (currentVisual != null)
            {
                currentVisual.SetActive(false);
            }

            // 启用新外观
            if (newVisual != null)
            {
                newVisual.SetActive(true);
            }

            // 更新当前外观引用
            currentVisual = newVisual;

            Debug.Log($"玩家外观已更改为: {shadowType}");
        }
    }
}