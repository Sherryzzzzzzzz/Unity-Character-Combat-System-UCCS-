using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectEvent : TimelineEventBase, ITimelineEventRuntime
{
    public GameObject effectPrefab;
    public Vector3 effectPosition;
    public Quaternion effectRotation;
    private GameObject effectInstance;

    public override TimelineEventType Type => TimelineEventType.Effect;
    public override string GetSummary()
    {
        if (effectPrefab == null)
        {
            return "特效: [未设置]";
        }

        return $"Effect [{StartFrame}-{EndFrame}] Eff:{effectPrefab.name}";
    }
    public void OnStart(GameObject target)
    {
        if (effectPrefab == null) return;

        if (effectInstance != null)
            DestroyInstance(effectInstance);

        var position = target.transform.TransformPoint(effectPosition);
        var rotation = target.transform.rotation * effectRotation;
        effectInstance = Object.Instantiate(effectPrefab, position, rotation);
        effectInstance.SetActive(true);
        PlayParticles(effectInstance);
    }

    public override TimelineEventBase Clone()
    {
        var newEvent = new EffectEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.effectPrefab = effectPrefab;
        newEvent.effectPosition = effectPosition;
        newEvent.effectRotation = effectRotation;
        return newEvent;
    }

    public void OnEnd(GameObject target)
    {
        if (effectInstance == null) return;

        var instance = effectInstance;
        effectInstance = null;

        float destroyDelay = StartFrame == EndFrame
            ? GetParticleDuration(instance)
            : StopParticles(instance);

        DestroyInstance(instance, destroyDelay);
    }

    private static void PlayParticles(GameObject instance)
    {
        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private static float StopParticles(GameObject instance)
    {
        float destroyDelay = 0f;
        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            destroyDelay = Mathf.Max(destroyDelay, GetParticleDuration(main));
            particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        return destroyDelay;
    }

    private static float GetParticleDuration(GameObject instance)
    {
        float duration = 0f;
        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            duration = Mathf.Max(duration, GetParticleDuration(particle.main));
        return duration;
    }

    private static float GetParticleDuration(ParticleSystem.MainModule main)
    {
        return main.duration + main.startDelay.constantMax + main.startLifetime.constantMax;
    }

    private static void DestroyInstance(GameObject instance, float delay = 0f)
    {
        if (Application.isPlaying)
        {
            if (delay > 0f)
                Object.Destroy(instance, delay);
            else
                Object.Destroy(instance);
        }
        else
        {
            Object.DestroyImmediate(instance);
        }
    }
}
