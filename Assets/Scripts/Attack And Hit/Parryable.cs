// 文件名: Parryable.cs
using UnityEngine;
using System.Collections;
using Animancer;

/// <summary>
/// 为一个角色赋予“可被弹反”的能力。
/// 监听 Event.ParrySuccess Tag，并触发“被弹反”状态。
/// 这是一个敌我通用的组件。
/// </summary>
[RequireComponent(typeof(TagComponent))]
public class Parryable : MonoBehaviour
{
    [Header("核心资产")]
    [Tooltip("监听这个Tag，以确定自己是否被弹反了")]
    public GameplayTagSO parrySuccessEventTag; // 在 Inspector 中拖入 "Event.ParrySuccess"

    [Tooltip("被弹反后，施加给自己的硬直Buff")]
    public BuffSO parriedStunBuff; // 在 Inspector 中拖入 "Buff_ParriedStun"

    [Header("动画配置")]
    [Tooltip("被弹反时播放的大硬直动画")]
    public ClipTransition parriedAnimation; // 这个动画片段可以直接在这里配置

    // --- 组件引用 ---
    private TagComponent _tagComponent;
    private AnimancerComponent _animancer;
    private Coroutine _parryRecoveryCoroutine;
    private bool _parryRecovered;
    
    // --- (可选) 行为控制接口 ---
    // 为了解耦，我们可以定义一个接口来中断行为
    public interface IBehaviorController
    {
        void InterruptAndDisableBehavior();
        void ResumeBehavior();
    }
    private IBehaviorController _behaviorController;


    private void Awake()
    {
        _tagComponent = GetComponent<TagComponent>();
        _animancer = GetComponent<AnimancerComponent>();
        
        // 尝试获取行为控制器 (可以是玩家的 PlayerController 或敌人的 AI Brain)
        _behaviorController = GetComponent<IBehaviorController>();
    }

    private void Update()
    {
        // 核心：每帧检查并消耗“弹反成功”事件 Tag
        if (parrySuccessEventTag != null && _tagComponent.ConsumeTag(parrySuccessEventTag))
        {
            // 我被弹反了！
            OnParried();
        }
    }

    /// <summary>
    /// 执行被弹反后的所有逻辑。
    /// </summary>
    private void OnParried()
    {
        Debug.Log($"'{gameObject.name}' 的攻击被弹反了！");

        // 1. 施加”被弹反”硬直 Buff
        if (parriedStunBuff != null)
        {
            _tagComponent.ApplyBuff(parriedStunBuff, this.gameObject);
        }

        // 2. 立即打断当前所有行为
        //    优先使用直接引用，fallback 到 GetComponent
        var playerSkill = GetComponent<PlayerSkillComponent>();
        var enemySkill = GetComponent<EnemySkillComponent>();
        if (playerSkill != null)
        {
            playerSkill.StopAndCleanup(true, false);
        }
        else if (enemySkill != null)
        {
            enemySkill.StopAndCleanup();
        }
        else
        {
            // Fallback: 尝试通过 SendMessage 调用（向后兼容）
            var attackComponent = (Component)playerSkill ?? (Component)enemySkill;
            if (attackComponent == null)
                attackComponent = GetComponent<PlayerSkillComponent>() ?? (Component)GetComponent<EnemySkillComponent>();
            attackComponent?.SendMessage("StopAndCleanup", SendMessageOptions.DontRequireReceiver);
        }

        // (可选) 通过接口禁用更高级的行为，如行为树
        _behaviorController?.InterruptAndDisableBehavior();

        // 取消之前的超时协程（如果有）
        if (_parryRecoveryCoroutine != null)
        {
            StopCoroutine(_parryRecoveryCoroutine);
            _parryRecoveryCoroutine = null;
        }
        _parryRecovered = false;

        // 3. 播放”被弹反”的大硬直动画
        if (_animancer != null && parriedAnimation != null && parriedAnimation.Clip != null)
        {
            // 确保 Layer 3 存在（最高优先级的效果层）
            int parryLayerIndex = 3;
            if (_animancer.Layers.Count <= parryLayerIndex)
                _animancer.Layers.Count = parryLayerIndex + 1;

            var state = _animancer.Layers[parryLayerIndex].Play(parriedAnimation);
            _animancer.Layers[parryLayerIndex].SetWeight(1f);

            // 动画播放结束后，渐隐层并允许行为恢复
            state.Events(this).OnEnd = () =>
            {
                if (_parryRecovered) return;
                _parryRecovered = true;
                _animancer.Layers[3].StartFade(0f, 0.25f);
                _behaviorController?.ResumeBehavior();
                // 取消超时协程
                if (_parryRecoveryCoroutine != null)
                {
                    StopCoroutine(_parryRecoveryCoroutine);
                    _parryRecoveryCoroutine = null;
                }
            };

            // 启动超时兜底协程
            float timeoutDuration = (parriedAnimation.Clip != null ? parriedAnimation.Clip.length : 2f) + 1f;
            _parryRecoveryCoroutine = StartCoroutine(ParryRecoveryTimeout(timeoutDuration));
        }
    }

    private IEnumerator ParryRecoveryTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);

        if (!_parryRecovered)
        {
            _parryRecovered = true;
#if UNITY_EDITOR
            Debug.LogWarning($"Parryable: OnEnd 未触发，超时 ({timeout:F1}s) 强制恢复 ResumeBehavior! ({gameObject.name})", this);
#endif
            if (_animancer != null)
                _animancer.Layers[3].StartFade(0f, 0.25f);
            _behaviorController?.ResumeBehavior();
        }
        _parryRecoveryCoroutine = null;
    }
}