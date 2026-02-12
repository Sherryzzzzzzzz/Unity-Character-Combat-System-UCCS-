// 文件名: AudioManager.cs (最终增强版)

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // --- 单例模式 ---
    public static AudioManager Instance { get; private set; }

    // --- 内部引用 ---
    private AudioSource _bgmSource;
    private AudioSource _sfxSource; // 用于2D音效


    void Awake()
    {
        // --- 单例模式 ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- 自动查找并分配 AudioSource ---
        // 我们假设 AudioSource 组件直接挂在名为 "BGM Source" 和 "SFX Source" 的子对象上
        Transform bgmTransform = transform.Find("BGM Source");
        if (bgmTransform != null)
        {
            _bgmSource = bgmTransform.GetComponent<AudioSource>();
        }
        if (_bgmSource == null)
        {
            Debug.LogWarning("AudioManager: Could not find an AudioSource on a child GameObject named 'BGM Source'.");
        }

        Transform sfxTransform = transform.Find("SFX Source");
        if (sfxTransform != null)
        {
            _sfxSource = sfxTransform.GetComponent<AudioSource>();
        }
        if (_sfxSource == null)
        {
            Debug.LogWarning("AudioManager: Could not find an AudioSource on a child GameObject named 'SFX Source' for 2D sounds.");
        }
    }
    
    /// <summary>
    /// 播放一个2D的、全场可闻的一次性音效。
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (_sfxSource != null && clip != null)
        {
            _sfxSource.PlayOneShot(clip);
        }
    }
    
    /// <summary>
    /// 在指定的世界坐标播放一个3D的、有位置感的一次性音效。
    /// </summary>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1.0f)
    {
        if (clip != null)
        {
            // 使用 Unity 内置的便捷方法，它会自动处理对象的创建和销毁
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}