using System.Collections;
using UnityEngine;

/// <summary>
/// HitStop 卡肉控制器 — 通过 AnimancerSpeedController 减速动画层。
/// 修复了旧版多请求竞态导致动画卡在慢动作的 bug。
/// </summary>
public class HitStopController : MonoBehaviour
{
    [System.Serializable]
    public struct HitStopConfig
    {
        public AttackForceType ForceType;
        public float AttackerDuration;
        public float VictimDuration;
        public float VictimFreezeDuration; // 命中硬冻结（动画速度=0），仅重击/吹飞制造“定格”
    }

    public HitStopConfig[] configs = new[]
    {
        new HitStopConfig { ForceType = AttackForceType.Light,  AttackerDuration = 0.03f, VictimDuration = 0.04f, VictimFreezeDuration = 0f },
        new HitStopConfig { ForceType = AttackForceType.Medium, AttackerDuration = 0.05f, VictimDuration = 0.08f, VictimFreezeDuration = 0.02f },
        new HitStopConfig { ForceType = AttackForceType.Heavy,  AttackerDuration = 0.08f, VictimDuration = 0.12f, VictimFreezeDuration = 0.05f },
        new HitStopConfig { ForceType = AttackForceType.Blow,   AttackerDuration = 0.12f, VictimDuration = 0.18f, VictimFreezeDuration = 0.08f },
    };

    private const float HIT_STOP_SPEED = 0.05f;
    private const int HIT_STOP_PRIORITY = 100; // 高于 Dodge 慢动作

    private Coroutine _attackerRoutine;
    private Coroutine _victimRoutine;

    private void Awake()
    {
        // ★ 自装配：卡肉依赖 AnimancerSpeedController 减速动画层，
        // 场景/预制体未挂载时自动补全（RequireComponent(AnimancerComponent) 由角色提供）
        if (GetComponent<AnimancerSpeedController>() == null)
            gameObject.AddComponent<AnimancerSpeedController>();
    }

    public void ApplyAttackerHitStop(AttackForceType forceType)
    {
        var cfg = GetConfig(forceType);
        if (cfg.AttackerDuration > 0f)
        {
            if (_attackerRoutine != null) StopCoroutine(_attackerRoutine);
            // ★ 防卡速：StopCoroutine 不会执行协程后续的 ReleaseSpeed，
            //   重启前先释放旧请求，避免动画永久卡在慢速（“贴地飞行/滑行”症状）
            GetComponent<AnimancerSpeedController>()?.ReleaseSpeed(this);
            _attackerRoutine = StartCoroutine(HitStopRoutine(cfg.AttackerDuration, "attacker", 0f));
        }
    }

    public void ApplyVictimHitStop(AttackForceType forceType)
    {
        var cfg = GetConfig(forceType);
        if (cfg.VictimDuration > 0f || cfg.VictimFreezeDuration > 0f)
        {
            if (_victimRoutine != null) StopCoroutine(_victimRoutine);
            // ★ 防卡速：同上，重启前释放旧请求
            GetComponent<AnimancerSpeedController>()?.ReleaseSpeed(this);
            _victimRoutine = StartCoroutine(HitStopRoutine(cfg.VictimDuration, "victim", cfg.VictimFreezeDuration));
        }
    }

    /// <summary>
    /// ★ 强制释放全部卡肉请求并停止卡肉协程（HitFlow 兜底清理用），
    /// 保证动画速度立即恢复正常——防止受击流程异常导致“移动慢动作”。
    /// </summary>
    public void ReleaseAll()
    {
        var ctrl = GetComponent<AnimancerSpeedController>();
        if (ctrl != null) ctrl.ReleaseSpeed(this);

        if (_attackerRoutine != null)
        {
            StopCoroutine(_attackerRoutine);
            _attackerRoutine = null;
        }
        if (_victimRoutine != null)
        {
            StopCoroutine(_victimRoutine);
            _victimRoutine = null;
        }
    }

    private IEnumerator HitStopRoutine(float duration, string role, float freezeDuration)
    {
        var ctrl = GetComponent<AnimancerSpeedController>();
        if (ctrl != null)
        {
            // 阶段 1：硬冻结（速度=0）——制造“命中定格”的戏剧感，仅重击/吹飞
            if (freezeDuration > 0f)
            {
                ctrl.RequestSpeed(this, 0f, HIT_STOP_PRIORITY);
                yield return new WaitForSecondsRealtime(freezeDuration);
            }
            // 阶段 2：慢速卡肉
            ctrl.RequestSpeed(this, HIT_STOP_SPEED, HIT_STOP_PRIORITY);
        }

        yield return new WaitForSecondsRealtime(duration);

        if (ctrl != null)
            ctrl.ReleaseSpeed(this);

        if (role == "attacker") _attackerRoutine = null;
        else _victimRoutine = null;
    }

    private HitStopConfig GetConfig(AttackForceType type)
    {
        foreach (var c in configs)
            if (c.ForceType == type) return c;
        return configs.Length > 0 ? configs[0] : new HitStopConfig();
    }
}
