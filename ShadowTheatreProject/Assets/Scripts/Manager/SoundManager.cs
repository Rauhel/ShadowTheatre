using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SoundManager>();
                if (instance == null)
                {
                    GameObject singleton = new GameObject(typeof(SoundManager).ToString());
                    instance = singleton.AddComponent<SoundManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;

            // 初始化音频播放器
            InitializeBGMPlayers();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public List<AudioSource> BGMPlayers = new List<AudioSource>();
    public List<AudioClip> MyMusicList;
    public List<AudioClip> MySFXList;

    [Header("Spatial Audio Settings")]
    public float maxDistance = 20f;       // Maximum distance at which sound is still audible
    public float minDistance = 1f;        // Distance at which sound is at full volume
    public AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // Volume falloff curve

    private Transform playerTransform;    // Reference to player's transform
    private Dictionary<AudioSource, GameObject> spatialAudioSources = new Dictionary<AudioSource, GameObject>();

    private void Start()
    {
        // 检查音频列表
        if (MyMusicList == null || MyMusicList.Count == 0)
        {
            Debug.LogWarning("音乐列表为空，无法播放背景音乐");
            MyMusicList = new List<AudioClip>();
        }

        if (MySFXList == null || MySFXList.Count == 0)
        {
            Debug.LogWarning("音效列表为空");
            MySFXList = new List<AudioClip>();
        }

        // 订阅场景加载事件
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 只有当音乐列表不为空时才播放默认背景音乐
        if (MyMusicList.Count > 0)
        {
            PlayMusic(0, true, true, 1.0f);
        }

        // Try to find player
        FindPlayer();
    }

    private void Update()
    {
        // Update spatial audio volumes based on distance
        if (playerTransform != null)
        {
            UpdateSpatialAudio();
        }
        else
        {
            // Try to find player if reference is lost
            FindPlayer();
        }
    }

    private void OnDestroy()
    {
        // 取消订阅场景加载事件
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void FindPlayer()
    {
        // Attempt to find player by tag - you may need to adjust this based on your player setup
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void UpdateSpatialAudio()
    {
        List<AudioSource> sourcesToRemove = new List<AudioSource>();

        foreach (var kvp in spatialAudioSources)
        {
            AudioSource source = kvp.Key;
            GameObject soundObject = kvp.Value;

            if (source == null || !source.isPlaying || soundObject == null)
            {
                sourcesToRemove.Add(source);
                continue;
            }

            // Calculate distance
            float distance = Vector3.Distance(playerTransform.position, soundObject.transform.position);

            // Normalize distance between 0 and 1
            float normalizedDistance = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));

            // Apply volume falloff using curve
            float volumeMultiplier = falloffCurve.Evaluate(1 - normalizedDistance);

            // Set the new volume
            source.volume = source.volume * volumeMultiplier;
        }

        // Clean up any finished sources
        foreach (var source in sourcesToRemove)
        {
            spatialAudioSources.Remove(source);
        }
    }

    private void InitializeBGMPlayers()
    {
        // 清理现有的播放器
        foreach (var player in BGMPlayers)
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
        }
        BGMPlayers.Clear();

        // 创建新的播放器
        for (int i = 0; i < 5; i++)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + i);
            bgmPlayer.transform.parent = this.transform;
            AudioSource audioSource = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(audioSource);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 停止所有当前播放的音乐
        foreach (var player in BGMPlayers)
        {
            if (player != null && player.isPlaying)
            {
                player.Stop();
            }
        }

        // 重新初始化音频播放器
        InitializeBGMPlayers();

        // 根据场景名称播放对应的背景音乐
        switch (scene.name)
        {
            case "MainMenu":
                PlayMusic(0, true, true, 1.0f); // 播放主菜单音乐
                break;
            case "GameScene":
                PlayMusic(1, true, true, 1.0f); // 播放游戏场景音乐
                break;
            default:
                PlayMusic(0, true, true, 1.0f); // 默认音乐
                break;
        }

        Debug.Log($"场景 {scene.name} 加载完成，开始播放背景音乐");
    }

    public void PlayMusic(int index, bool isPlay, bool isLoop, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.clip == MyMusicList[index] && p.isPlaying == isPlay);
        if (player == null)
        {
            player = BGMPlayers.Find(p => p.isPlaying == false);
            if (player == null)
            {
                GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
                bgmPlayer.transform.parent = this.transform;
                player = bgmPlayer.AddComponent<AudioSource>();
                BGMPlayers.Add(player);
            }
            player.clip = MyMusicList[index];
        }
        player.loop = isLoop;
        player.volume = volume;
        if (isPlay)
        {
            player.Play();
        }
        else
        {
            player.Stop();
        }
    }

    public void PlayOneShotMusic(int index, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(player);
        }
        player.clip = MyMusicList[index];
        player.volume = volume;
        player.PlayOneShot(player.clip);
    }

    // Modified version with GameObject parameter for spatial audio
    public void PlaySFX(int index, bool isPlay, bool isLoop, GameObject soundLocation, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.clip == MySFXList[index] && p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(player);
        }
        player.clip = MySFXList[index];
        player.loop = isLoop;
        player.volume = volume;

        // Register this as a spatial audio source
        if (soundLocation != null)
        {
            spatialAudioSources[player] = soundLocation;
        }

        if (isPlay)
        {
            player.Play();
        }
        else
        {
            player.Stop();
        }
    }

    // Keep the original method for backward compatibility
    public void PlaySFX(int index, bool isPlay, bool isLoop, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.clip == MySFXList[index] && p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(player);
        }
        player.clip = MySFXList[index];
        player.loop = isLoop;
        player.volume = volume;
        if (isPlay)
        {
            player.Play();
        }
        else
        {
            player.Stop();
        }
    }

    // Modified version with GameObject parameter for spatial audio
    public void PlayOneShotSFX(int index, GameObject soundLocation, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(player);
        }
        player.clip = MySFXList[index];
        player.volume = volume;

        // Register this as a spatial audio source
        if (soundLocation != null)
        {
            spatialAudioSources[player] = soundLocation;
        }

        player.PlayOneShot(player.clip);
    }

    // Keep the original method for backward compatibility
    public void PlayOneShotSFX(int index, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            BGMPlayers.Add(player);
        }
        player.clip = MySFXList[index];
        player.volume = volume;
        player.PlayOneShot(player.clip);
    }
}