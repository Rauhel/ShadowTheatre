using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    [Header("BGMPlayer:\n" +
    "1. This will clear all music when scene changes\n" +
    "2. When the scene started, it will play the ELEMENT 0 with isLoop = True and volume = 1.0f\n" +
    "(That is to say, Element 0 is default BGM)\n" +
    "---------------\n" +
    "3. This Function Provides 4 ways to play music:\n" +
    "--- SoundManager.Instance.PlayMusic(index, isPlay, isLoop, volume), Play a constant Music\n" +
    "--- SoundManager.Instance.PlayOneShotMusic(index, volume), Play a Music only once\n" +
    "--- SoundManager.Instance.PlaySFX(index, isPlay, isLoop, volume), Play a constant SFX\n" +
    "--- SoundManager.Instance.PlayOneShotSFX(index, volume), Play a SFX only once\n" +
    "Use these functions anywhere to play music and sound effects\n")]
    [Space(20)]
    public List<AudioSource> BGMPlayers = new List<AudioSource>();
    public List<AudioClip> MyMusicList;
    public List<AudioClip> MySFXList;

    private void Start()
    {
        Debug.Log("=== SoundManager Start() 开始 ===");
        
        // 检查音频列表
        if (MyMusicList == null || MyMusicList.Count == 0)
        {
            Debug.LogWarning("音乐列表为空，无法播放背景音乐");
        }
        else
        {
            Debug.Log($"音乐列表包含 {MyMusicList.Count} 个音频文件");
            for (int i = 0; i < MyMusicList.Count; i++)
            {
                if (MyMusicList[i] != null)
                {
                    AudioClip clip = MyMusicList[i];
                    Debug.Log($"  索引 {i}: {clip.name}");
                    Debug.Log($"    - 长度: {clip.length}秒");
                    Debug.Log($"    - 频率: {clip.frequency}Hz");
                    Debug.Log($"    - 通道数: {clip.channels}");
                    Debug.Log($"    - 状态: {clip.loadState}");
                    Debug.Log($"    - 音频类型: {clip.loadType}");
                }
                else
                {
                    Debug.LogError($"  索引 {i}: null音频文件!");
                }
            }
        }

        // 检查Unity音频设置
        Debug.Log($"Unity音频设置:");
        Debug.Log($"  - Master Volume: {AudioListener.volume}");
        Debug.Log($"  - AudioSettings DSP Buffer Size: {AudioSettings.GetConfiguration().dspBufferSize}");
        Debug.Log($"  - AudioSettings Sample Rate: {AudioSettings.GetConfiguration().sampleRate}");

        for (int i = 0; i < 5; i++)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + i);
            bgmPlayer.transform.parent = this.transform;
            AudioSource audioSource = bgmPlayer.AddComponent<AudioSource>();
            
            // 确保音频不受时间缩放影响
            audioSource.ignoreListenerPause = true;
            audioSource.ignoreListenerVolume = false; // 仍然受音量控制
            
            // 检查AudioSource默认设置
            Debug.Log($"BGMPlayer{i} AudioSource设置:");
            Debug.Log($"  - Volume: {audioSource.volume}");
            Debug.Log($"  - Pitch: {audioSource.pitch}");
            Debug.Log($"  - Spatial Blend: {audioSource.spatialBlend}");
            Debug.Log($"  - Priority: {audioSource.priority}");
            Debug.Log($"  - Mute: {audioSource.mute}");
            Debug.Log($"  - Enabled: {audioSource.enabled}");
            Debug.Log($"  - Ignore Listener Pause: {audioSource.ignoreListenerPause}");
            
            BGMPlayers.Add(audioSource);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // 检查时间缩放
        Debug.Log($"当前时间缩放: {Time.timeScale}");
        if (Time.timeScale < 0.001f)
        {
            Debug.LogWarning($"检测到极小的时间缩放值: {Time.timeScale}，这可能影响音频播放");
            Debug.LogWarning("但AudioSource已设置为忽略监听器暂停，音频应该仍能播放");
        }
        
        // 检查AudioListener
        AudioListener audioListener = FindObjectOfType<AudioListener>();
        if (audioListener == null)
        {
            Debug.LogError("场景中没有找到AudioListener！音频将无法播放。");
        }
        else
        {
            Debug.Log($"找到AudioListener: {audioListener.name}");
            Debug.Log($"  - AudioListener Volume: {AudioListener.volume}");
            Debug.Log($"  - AudioListener Pause: {AudioListener.pause}");
            Debug.Log($"  - AudioListener Enabled: {audioListener.enabled}");
            Debug.Log($"  - AudioListener GameObject Active: {audioListener.gameObject.activeInHierarchy}");
        }
        
        // 播放默认背景音乐
        if (MyMusicList != null && MyMusicList.Count > 0)
        {
            Debug.Log("开始播放默认背景音乐 (索引0)");
            PlayMusic(0, true, true, 1.0f);
        }
        
        Debug.Log("=== SoundManager Start() 结束 ===");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"场景 {scene.name} 加载完成");
        
        foreach (var player in BGMPlayers)
        {
            // 这里应该检查player.gameObject是否是场景中的对象
            if (player.gameObject.scene == SceneManager.GetActiveScene())
            {
                DestroyImmediate(player.gameObject, true);
            }
        }
        BGMPlayers.Clear();

        // 重新创建BGMPlayers
        for (int i = 0; i < 5; i++)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + i);
            bgmPlayer.transform.parent = this.transform;
            AudioSource audioSource = bgmPlayer.AddComponent<AudioSource>();
            
            // 确保音频不受时间缩放影响
            audioSource.ignoreListenerPause = true;
            audioSource.ignoreListenerVolume = false;
            
            BGMPlayers.Add(audioSource);
        }

        // 播放默认背景音乐
        if (MyMusicList != null && MyMusicList.Count > 0)
        {
            Debug.Log("场景切换后播放默认背景音乐");
            PlayMusic(0, true, true, 1.0f);
        }
    }

    public void PlayMusic(int index, bool isPlay, bool isLoop, float volume = 1.0f)
    {
        Debug.Log($"PlayMusic调用: index={index}, isPlay={isPlay}, volume={volume}");
        
        if (MyMusicList == null || index >= MyMusicList.Count || index < 0)
        {
            Debug.LogError($"音乐索引 {index} 超出范围或列表为空");
            return;
        }
        
        AudioSource player = BGMPlayers.Find(p => p.clip == MyMusicList[index] && p.isPlaying == isPlay);
        if (player == null)
        {
            player = BGMPlayers.Find(p => p.isPlaying == false);
            if (player == null)
            {
                GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
                bgmPlayer.transform.parent = this.transform;
                player = bgmPlayer.AddComponent<AudioSource>();
                
                // 确保新创建的AudioSource不受时间缩放影响
                player.ignoreListenerPause = true;
                player.ignoreListenerVolume = false;
                
                BGMPlayers.Add(player);
            }
            player.clip = MyMusicList[index];
        }
        player.loop = isLoop;
        player.volume = volume;
        if (isPlay)
        {
            player.Play();
            Debug.Log($"音乐开始播放: {player.clip.name}, 播放状态: {player.isPlaying}");
            
            // 播放后立即验证状态
            StartCoroutine(VerifyPlaybackAfterDelay(player, 0.1f));
        }
        else
        {
            player.Stop();
            Debug.Log("音乐停止播放");
        }
    }
    
    // 延迟验证播放状态的协程
    private System.Collections.IEnumerator VerifyPlaybackAfterDelay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"=== 播放状态验证 ===");
        Debug.Log($"AudioSource isPlaying: {source.isPlaying}");
        Debug.Log($"AudioSource time: {source.time}");
        Debug.Log($"AudioSource volume: {source.volume}");
        Debug.Log($"AudioSource enabled: {source.enabled}");
        Debug.Log($"AudioSource gameObject active: {source.gameObject.activeInHierarchy}");
        Debug.Log($"AudioSource mute: {source.mute}");
        Debug.Log($"Clip length: {source.clip.length}");
        Debug.Log($"Clip loadState: {source.clip.loadState}");
        
        if (!source.isPlaying)
        {
            Debug.LogError("音频源显示未在播放！可能的问题:");
            Debug.LogError("1. 音频文件损坏或格式不支持");
            Debug.LogError("2. AudioListener问题");
            Debug.LogError("3. Unity音频设置问题");
            Debug.LogError("4. 系统音频问题");
        }
    }
    
    // 手动检查所有音频源状态的方法（可在Inspector中调用）
    [ContextMenu("检查所有音频源状态")]
    public void CheckAllAudioSourcesStatus()
    {
        Debug.Log("=== 检查所有BGM播放器状态 ===");
        for (int i = 0; i < BGMPlayers.Count; i++)
        {
            AudioSource source = BGMPlayers[i];
            if (source != null)
            {
                Debug.Log($"BGMPlayer{i}:");
                Debug.Log($"  - isPlaying: {source.isPlaying}");
                Debug.Log($"  - clip: {(source.clip != null ? source.clip.name : "null")}");
                Debug.Log($"  - volume: {source.volume}");
                Debug.Log($"  - time: {source.time}");
                Debug.Log($"  - enabled: {source.enabled}");
                Debug.Log($"  - mute: {source.mute}");
            }
            else
            {
                Debug.LogError($"BGMPlayer{i}: AudioSource is null!");
            }
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
            
            // 确保新创建的AudioSource不受时间缩放影响
            player.ignoreListenerPause = true;
            player.ignoreListenerVolume = false;
            
            BGMPlayers.Add(player);
        }
        player.clip = MyMusicList[index];
        player.volume = volume;
        player.PlayOneShot(player.clip);
    }

    public void PlaySFX(int index, bool isPlay, bool isLoop, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.clip == MySFXList[index] && p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            
            // 确保新创建的AudioSource不受时间缩放影响
            player.ignoreListenerPause = true;
            player.ignoreListenerVolume = false;
            
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

    public void PlayOneShotSFX(int index, float volume = 1.0f)
    {
        AudioSource player = BGMPlayers.Find(p => p.isPlaying == false);
        if (player == null)
        {
            GameObject bgmPlayer = new GameObject("BGMPlayer" + BGMPlayers.Count);
            bgmPlayer.transform.parent = this.transform;
            player = bgmPlayer.AddComponent<AudioSource>();
            
            // 确保新创建的AudioSource不受时间缩放影响
            player.ignoreListenerPause = true;
            player.ignoreListenerVolume = false;
            
            BGMPlayers.Add(player);
        }
        player.clip = MySFXList[index];
        player.volume = volume;
        //player.PlayOneShot(player.clip, volume);
        // 修正：PlayOneShot不需要音量参数，音量通过AudioSource.volume设置
        player.PlayOneShot(player.clip);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}