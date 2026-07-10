using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

/// <summary>
/// HitStop 独立卡肉控制器 — 减速动画层而非冻结全局 Time.timeScale
/// </summary>
public class HitStopController : MonoBehaviour
{
    private AnimancerComponent _animancer;
    private Coroutine _hitStopRoutine;

    [System.Serializable]
    public struct HitStopConfig
    {
        public AttackForceType ForceType;
        public float AttackerDuration;
        public float VictimDuration;
    }

    public HitStopConfig[] configs = new[]
    {
        new HitStopConfig { ForceType = AttackForceType.Light,  AttackerDuration = 0.03f, VictimDuration = 0.04f },
        new HitStopConfig { ForceType = AttackForceType.Medium, AttackerDuration = 0.05f, VictimDuration = 0.08f },
        new HitStopConfig { ForceType = AttackForceType.Heavy,  AttackerDuration = 0.08f, VictimDuration = 0.12f },
        new HitStopConfig { ForceType = AttackForceType.Blow,   AttackerDuration = 0.12f, VictimDuration = 0.18f },
    };

    private void Awake()
    {
        _animancer = GetComponent<AnimancerComponent>();
    }

    public void ApplyAttackerHitStop(AttackForceType forceType)
    {
        var cfg = GetConfig(forceType);
        if (cfg.AttackerDuration > 0f)
            StartCoroutine(HitStopRoutine(cfg.AttackerDuration));
    }

    public void ApplyVictimHitStop(AttackForceType forceType)
    {
        var cfg = GetConfig(forceType);
        if (cfg.VictimDuration > 0f)
            StartCoroutine(HitStopRoutine(cfg.VictimDuration));
    }

    private HitStopConfig GetConfig(AttackForceType type)
    {
        foreach (var c in configs)
            if (c.ForceType == type) return c;
        return configs.Length > 0 ? configs[0] : new HitStopConfig();
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
        _hitStopRoutine = null;

        // 保存并减速所有 Animancer 层
        var savedSpeeds = new List<(AnimancerLayer layer, float speed)>();
        if (_animancer != null)
        {
            for (int i = 0; i < _animancer.Layers.Count; i++)
            {
                var layer = _animancer.Layers[i];
                var state = layer.CurrentState;
                if (state != null)
                {
                    savedSpeeds.Add((layer, state.Speed));
                    state.Speed = 0.05f;
                }
            }
        }

        yield return new WaitForSecondsRealtime(duration);

        // 恢复速度
        foreach (var (layer, speed) in savedSpeeds)
        {
            var state = layer.CurrentState;
            if (state != null)
                state.Speed = speed;
        }
    }
}
