using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 路径注册表系统，用于管理和查找所有路径
/// </summary>
public class PathRegistry : MonoBehaviour
{
    private static PathRegistry _instance;
    public static PathRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<PathRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("PathRegistry");
                    _instance = go.AddComponent<PathRegistry>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class PathEntry
    {
        public string pathID;
        public MultiPointPathCreator pathCreator;
    }

    public List<PathEntry> registeredPaths = new List<PathEntry>();
    private Dictionary<string, MultiPointPathCreator> pathLookup = new Dictionary<string, MultiPointPathCreator>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildLookupTable();
    }

    private void BuildLookupTable()
    {
        pathLookup.Clear();
        foreach (var entry in registeredPaths)
        {
            if (!string.IsNullOrEmpty(entry.pathID) && entry.pathCreator != null)
            {
                pathLookup[entry.pathID] = entry.pathCreator;
            }
        }
    }

    // 用于运行时获取路径
    public static MultiPointPathCreator GetPathCreatorByID(string pathID)
    {
        if (string.IsNullOrEmpty(pathID))
            return null;

        // 强制重新构建查找表确保最新状态
        Instance.BuildLookupTable();

        // 首先尝试从查找表查找
        if (Instance.pathLookup.TryGetValue(pathID, out MultiPointPathCreator creator) && creator != null)
        {
            return creator;
        }

        // 如果查找表中没有，直接在场景中搜索
        MultiPointPathCreator[] allPaths = GameObject.FindObjectsOfType<MultiPointPathCreator>(true); // 包括非激活对象
        foreach (var path in allPaths)
        {
            if (path.pathID == pathID)
            {
                // 更新查找表
                Instance.pathLookup[pathID] = path;
                return path;
            }
        }

        // 查找失败，报警告但不阻断
        Debug.LogWarning($"PathRegistry: 无法找到ID为'{pathID}'的路径");
        return null;
    }

    // 用于运行时注册新的路径
    public static void RegisterPath(string pathID, MultiPointPathCreator pathCreator)
    {
        if (string.IsNullOrEmpty(pathID) || pathCreator == null)
            return;

        // 检查是否已注册
        foreach (var entry in Instance.registeredPaths)
        {
            if (entry.pathID == pathID)
            {
                entry.pathCreator = pathCreator;
                Instance.pathLookup[pathID] = pathCreator;
                return;
            }
        }

        // 添加新注册
        Instance.registeredPaths.Add(new PathEntry { pathID = pathID, pathCreator = pathCreator });
        Instance.pathLookup[pathID] = pathCreator;
    }

#if UNITY_EDITOR
    // 编辑器工具 - 自动查找并注册所有路径
    public void AutoRegisterAllPaths()
    {
        registeredPaths.Clear();
        
        // 查找所有MultiPointPathCreator
        MultiPointPathCreator[] allPaths = FindObjectsOfType<MultiPointPathCreator>();
        foreach (var path in allPaths)
        {
            string id = path.pathID;
            
            // 如果没有ID，生成一个
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8);
                path.pathID = id;
                EditorUtility.SetDirty(path);
            }
            
            registeredPaths.Add(new PathEntry { pathID = id, pathCreator = path });
        }
        
        EditorUtility.SetDirty(this);
    }
#endif
}

#if UNITY_EDITOR
// 编辑器检查器
[CustomEditor(typeof(PathRegistry))]
public class PathRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        PathRegistry registry = (PathRegistry)target;
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("自动注册所有路径"))
        {
            registry.AutoRegisterAllPaths();
        }
        
        if (GUILayout.Button("刷新路径注册表"))
        {
            // 确保所有路径都有唯一ID
            HashSet<string> usedIDs = new HashSet<string>();
            bool needsUpdate = false;
            
            for (int i = 0; i < registry.registeredPaths.Count; i++)
            {
                var entry = registry.registeredPaths[i];
                
                // 跳过无效条目
                if (entry.pathCreator == null)
                {
                    registry.registeredPaths.RemoveAt(i);
                    i--;
                    needsUpdate = true;
                    continue;
                }
                
                // 确保ID不为空
                if (string.IsNullOrEmpty(entry.pathID))
                {
                    entry.pathID = System.Guid.NewGuid().ToString().Substring(0, 8);
                    entry.pathCreator.pathID = entry.pathID;
                    EditorUtility.SetDirty(entry.pathCreator);
                    needsUpdate = true;
                }
                
                // 确保ID唯一
                if (usedIDs.Contains(entry.pathID))
                {
                    string newID = System.Guid.NewGuid().ToString().Substring(0, 8);
                    entry.pathID = newID;
                    entry.pathCreator.pathID = newID;
                    EditorUtility.SetDirty(entry.pathCreator);
                    needsUpdate = true;
                }
                
                usedIDs.Add(entry.pathID);
            }
            
            if (needsUpdate)
            {
                EditorUtility.SetDirty(registry);
            }
        }
    }
}
#endif