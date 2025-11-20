using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundEvent : TimelineEventBase, ITimelineEventRuntime
{
    public AudioClip soundClip;
    public float volume = 1.0f;
    public bool loop = false; // 是否循环播放

    private AudioSource audioSource;

    public override TimelineEventType Type => TimelineEventType.Sound;

    public override string GetSummary()
    {
        if (soundClip == null)
        {
            return "音效: [未设置]";
        }
        return $"音效: {soundClip.name}";
    }

    public void OnStart(GameObject owner)
    {
        if (soundClip == null) return;

        audioSource = owner.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = owner.AddComponent<AudioSource>();
        }

        audioSource.clip = soundClip;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.Play();
    }

    public void OnEnd(GameObject owner)
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    public override TimelineEventBase Clone()
    {
        return new SoundEvent
        {
            StartFrame = this.StartFrame,
            EndFrame = this.EndFrame,
            soundClip = this.soundClip,
            volume = this.volume,
            loop = this.loop
        };
    }
}
