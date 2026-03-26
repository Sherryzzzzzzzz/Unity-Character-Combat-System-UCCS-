using UnityEngine;

/// <summary>
/// 音效 Cue — 管理音效的播放和停止
/// </summary>
public class SoundCue : MonoBehaviour, IGameplayCue
{
    [Tooltip("Instant 效果触发时播放的音效")]
    public AudioClip executeClip;

    [Tooltip("Duration 效果施加时循环播放的音效")]
    public AudioClip loopClip;

    [Range(0f, 1f)]
    public float volume = 1f;

    private AudioSource _loopSource;

    public void OnExecute(GameObject target, GameplayEffectSpec spec)
    {
        if (executeClip == null) return;

        var audioSource = target.GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(executeClip, volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(executeClip, target.transform.position, volume);
        }
    }

    public void OnAdd(GameObject target, GameplayEffectSpec spec)
    {
        if (loopClip == null) return;

        _loopSource = target.GetComponent<AudioSource>();
        if (_loopSource == null)
            _loopSource = target.AddComponent<AudioSource>();

        _loopSource.clip = loopClip;
        _loopSource.loop = true;
        _loopSource.volume = volume;
        _loopSource.Play();
    }

    public void OnRemove(GameObject target)
    {
        if (_loopSource != null && _loopSource.isPlaying)
        {
            _loopSource.Stop();
            _loopSource.loop = false;
            _loopSource.clip = null;
        }
        _loopSource = null;
    }
}
