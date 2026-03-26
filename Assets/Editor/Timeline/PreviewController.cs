using UnityEditor;
using UnityEngine;

/// <summary>
/// 预览控制器 — 负责动画预览和 SceneView 集成
/// </summary>
public class PreviewController
{
    private AnimationClip _clip;
    private GameObject _previewObj;
    private AnimationClip _defaultPoseClip;
    private AudioSource _previewAudioSource;

    private bool _isPlaying;
    private double _playTimeSec;
    private double _lastEditorTime;
    private float _playbackSpeed = 1f;
    private int _currentFrame;
    private int _totalFrames;

    public bool IsPlaying => _isPlaying;
    public int CurrentFrame => _currentFrame;
    public GameObject PreviewObject { get => _previewObj; set => _previewObj = value; }
    public AnimationClip DefaultPoseClip { get => _defaultPoseClip; set => _defaultPoseClip = value; }
    public float PlaybackSpeed { get => _playbackSpeed; set => _playbackSpeed = Mathf.Max(0f, value); }

    public void Initialize()
    {
        if (_previewAudioSource == null)
        {
            var previewer = new GameObject("Skill Editor Audio Previewer");
            previewer.hideFlags = HideFlags.HideAndDontSave;
            _previewAudioSource = previewer.AddComponent<AudioSource>();
        }
    }

    public void Cleanup()
    {
        if (_previewAudioSource != null)
        {
            Object.DestroyImmediate(_previewAudioSource.gameObject);
            _previewAudioSource = null;
        }
        RestoreToDefaultPose();
        if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
    }

    public void SetClipData(AnimationClip clip, int totalFrames)
    {
        _clip = clip;
        _totalFrames = totalFrames;
    }

    public void StartPlayback()
    {
        if (_clip == null || _previewObj == null) return;
        _isPlaying = true;
        _playTimeSec = (float)_currentFrame / _clip.frameRate;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
    }

    public void StopPlayback()
    {
        _isPlaying = false;
    }

    public void TogglePlayback()
    {
        if (_isPlaying) StopPlayback();
        else StartPlayback();
    }

    /// <summary>
    /// 跳转到指定帧，返回当前帧
    /// </summary>
    public int JumpToFrame(int frame)
    {
        if (_clip == null)
        {
            _currentFrame = 0;
            _playTimeSec = 0;
            return 0;
        }
        _currentFrame = Mathf.Clamp(frame, 0, _totalFrames > 0 ? _totalFrames - 1 : 0);
        _playTimeSec = _totalFrames > 0 ? _currentFrame / (float)_clip.frameRate : 0;
        if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        SampleAtTime(_playTimeSec);
        return _currentFrame;
    }

    public string GetTimeLabel()
    {
        if (_clip == null) return "时间: -- | 帧: --";
        return $"时间: {_playTimeSec:F2}s | 帧: {_currentFrame} / {_totalFrames}";
    }

    /// <summary>
    /// 每帧更新播放，返回是否帧有变化
    /// </summary>
    public bool UpdatePlayback(out int newFrame)
    {
        newFrame = _currentFrame;
        if (!_isPlaying || _clip == null || _previewObj == null) return false;

        double now = EditorApplication.timeSinceStartup;
        double deltaTime = now - _lastEditorTime;
        _playTimeSec += deltaTime * _playbackSpeed;
        _lastEditorTime = now;

        if (_playTimeSec >= _clip.length) _playTimeSec %= _clip.length;
        if (_playTimeSec < 0) _playTimeSec = 0;

        newFrame = Mathf.FloorToInt((float)(_playTimeSec * _clip.frameRate));
        if (newFrame != _currentFrame)
        {
            _currentFrame = newFrame;
            return true;
        }

        SampleAtTime(_playTimeSec);
        return false;
    }

    public void PreviewSoundAtFrame(int frame, System.Collections.Generic.List<TimelineData> timelines)
    {
        if (_previewAudioSource == null) return;

        foreach (var timeline in timelines)
        {
            foreach (var evt in timeline.events)
            {
                if (evt is SoundEvent soundEvent)
                {
                    if (frame == soundEvent.StartFrame && soundEvent.soundClip != null)
                    {
                        if (soundEvent.loop)
                        {
                            _previewAudioSource.clip = soundEvent.soundClip;
                            _previewAudioSource.volume = soundEvent.volume;
                            _previewAudioSource.loop = true;
                            _previewAudioSource.Play();
                        }
                        else
                        {
                            _previewAudioSource.PlayOneShot(soundEvent.soundClip, soundEvent.volume);
                        }
                    }
                    else if (frame == soundEvent.EndFrame && soundEvent.loop)
                    {
                        if (_previewAudioSource.isPlaying && _previewAudioSource.clip == soundEvent.soundClip)
                            _previewAudioSource.Stop();
                    }
                }
            }
        }
    }

    private void SampleAtTime(double timeSec)
    {
        if (_clip == null || _previewObj == null || !AnimationMode.InAnimationMode()) return;
        AnimationMode.SampleAnimationClip(_previewObj, _clip, (float)timeSec);
        SceneView.RepaintAll();
    }

    private void RestoreToDefaultPose()
    {
        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();

        if (_previewObj != null && _defaultPoseClip != null)
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(_previewObj, _defaultPoseClip, 0);
            AnimationMode.StopAnimationMode();
        }
    }
}
