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
        if (effectPrefab != null)
        {
            effectInstance = Object.Instantiate(effectPrefab, target.transform.position + effectPosition, effectRotation);
            
            // 如果有粒子系统，播放它
            ParticleSystem ps = effectInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
        }
    }
    
    public override TimelineEventBase Clone()
    {
        var newEvent = new EffectEvent();
        newEvent.StartFrame = StartFrame;
        newEvent.EndFrame = EndFrame;
        newEvent.effectPrefab = effectPrefab;
        return newEvent;
    }

    public void OnEnd(GameObject target)
    {
        if (effectInstance != null)
        {
            // 停止粒子系统
            ParticleSystem ps = effectInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop();
            }
            
            Object.Destroy(effectInstance);
        }
    }
}
