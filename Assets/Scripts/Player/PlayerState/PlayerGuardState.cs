using UnityEngine;
using Animancer;

public class PlayerGuardState : PlayerStateBase
{
    private AnimancerLayer _attackLayer;
    private int _guardEffectHandle = -1;
    private bool _isExiting = false;
    private AttributeSet _attributes;
    private TagComponent _tagComponent;

    public override void Enter(object parameter = null)
    {
        base.Enter();
        _isExiting = false;

        // 标记为 attacking 状态，防止 StopAndCleanup 的 triggerDefaultStateChange 覆盖状态
        playerModel.isAttacking = true;

        var animancer = playerModel.animancer;
        _attackLayer = animancer.Layers[1];

        // Play guard loop animation on the attack layer
        if (playerModel.guardAnimation != null && playerModel.guardAnimation.Clip != null)
        {
            var state = _attackLayer.Play(playerModel.guardAnimation);
            // 强制攻击层权重为1（对抗 StopAndCleanup 的淡出）
            _attackLayer.SetWeight(1f);
        }

        // 获取组件引用
        _attributes = playerModel.GetComponent<AttributeSet>();
        _tagComponent = playerModel.GetComponent<TagComponent>();

        // Apply guard GameplayEffect via ASC（如果配置了）
        var asc = playerModel.GetComponent<AbilitySystemComponent>();
        var hbm = playerModel.GetComponent<HurtBoxManager>();

        if (asc != null && playerModel.guardEffect != null)
        {
            _guardEffectHandle = asc.ApplyGameplayEffect(playerModel.guardEffect, asc);
        }

        // 【CRITICAL FIX】Fallback: 如果 guardEffect 为空或未成功授予 guardingTag，
        // 直接手动添加 guardingTag，确保格挡逻辑能正常工作
        if (hbm != null && hbm.guardingTag != null && _tagComponent != null)
        {
            if (!_tagComponent.HasTag(hbm.guardingTag))
            {
                _tagComponent.AddTag(hbm.guardingTag);
            }
        }

        // 【弹反窗口】进入格挡的前 0.35 秒同时也是弹反窗口
        // 给玩家 normal parry tag，让 ProcessHit 的弹反检测生效
        if (hbm != null && hbm.normalParryTag != null && _tagComponent != null)
        {
            _tagComponent.AddTag(hbm.normalParryTag);
            playerModel.StartCoroutine(RemoveParryTagAfterDelay(hbm.normalParryTag, 0.35f));
        }
        if (hbm != null && hbm.perfectParryTag != null && _tagComponent != null)
        {
            _tagComponent.AddTag(hbm.perfectParryTag);
            playerModel.StartCoroutine(RemoveParryTagAfterDelay(hbm.perfectParryTag, 0.18f));
        }

        // 【鬼泣式 Just Guard 窗口】进入格挡的最初 justGuardWindow 秒内被命中 = 完美格挡
        // （无伤、零消耗、弹开攻击者、慢动作、反击加成）—— 比完美弹反更短的精确窗口
        if (hbm != null && hbm.justGuardTag != null && _tagComponent != null)
        {
            _tagComponent.AddTag(hbm.justGuardTag);
            playerModel.StartCoroutine(RemoveParryTagAfterDelay(hbm.justGuardTag, hbm.justGuardWindow));
        }

        // 监听 Poise 变化和破防
        if (_attributes != null)
        {
            _attributes.OnPoiseBreak += OnPoiseBreakHandler;
        }

        playerModel.isDefending = true;
    }

    private void OnPoiseBreakHandler()
    {
        // 破防 → 退出格挡状态
        if (_isExiting) return;
        _isExiting = true;

        Debug.Log($"{playerModel.gameObject.name}: Guard broken! Poise depleted.");

        // 移除格挡标签（包括 GE 授予的和 fallback 添加的）
        if (_tagComponent != null)
        {
            if (playerModel.guardEffect != null)
            {
                foreach (var tag in playerModel.guardEffect.grantedTags)
                {
                    if (tag != null)
                        _tagComponent.RemoveTag(tag);
                }
            }
            // 同时移除 fallback guardingTag
            var hbm = playerModel.GetComponent<HurtBoxManager>();
            if (hbm != null && hbm.guardingTag != null)
                _tagComponent.RemoveTag(hbm.guardingTag);
        }

        // 播放破防动画
        if (playerModel.guardEndAnimation != null && playerModel.guardEndAnimation.Clip != null)
        {
            var state = _attackLayer.Play(playerModel.guardEndAnimation);
            state.Events(playerModel).OnEnd = () => ExitToGround();
        }
        else
        {
            ExitToGround();
        }
    }

    public override void Update()
    {
        base.Update();

        if (_isExiting) return;

        // 【防御保护】持续强制攻击层权重为 1，防止被外部系统（HitStop/HitReaction）降低
        if (_attackLayer != null && _attackLayer.Weight < 0.99f)
            _attackLayer.SetWeight(1f);

        // 防御状态下不处理受击
        if (playerModel.isHitting) return;

        // 检查防御状态是否仍有效（以 isDefending 为权威来源）
        if (!playerModel.isDefending)
        {
            ExitToGround();
            return;
        }

        // Fallback: 如果 isDefending 为 true 但 guardingTag 丢失，重新添加
        if (_tagComponent != null)
        {
            var hbm = playerModel.GetComponent<HurtBoxManager>();
            if (hbm != null && hbm.guardingTag != null && !_tagComponent.HasTag(hbm.guardingTag))
            {
                _tagComponent.AddTag(hbm.guardingTag);
            }
        }

        // Face lock-on target
        if (playerModel.ts != null && playerModel.ts.HasTarget)
        {
            Vector3 dir = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                playerModel.transform.rotation = Quaternion.Slerp(
                    playerModel.transform.rotation, targetRot, Time.deltaTime * 30f);
            }
        }

        // Dodge cancel
        if (playerController.dodge)
        {
            ExitToGround();
            return;
        }

        // Release defend → play end animation → exit
        if (!playerController.defendHeld)
        {
            if (playerModel.guardEndAnimation != null && playerModel.guardEndAnimation.Clip != null)
            {
                _isExiting = true;
                var state = _attackLayer.Play(playerModel.guardEndAnimation);
                state.Events(playerModel).OnEnd = () => ExitToGround();
            }
            else
            {
                ExitToGround();
            }
        }
    }

    public override void Exit()
    {
        base.Exit();

        // 取消 Poise 事件订阅
        if (_attributes != null)
        {
            _attributes.OnPoiseBreak -= OnPoiseBreakHandler;
        }

        // Remove guard GE
        if (_guardEffectHandle > 0)
        {
            var asc = playerModel.GetComponent<AbilitySystemComponent>();
            if (asc != null)
            {
                asc.RemoveActiveEffectByHandle(_guardEffectHandle);
            }
            _guardEffectHandle = -1;
        }

        // 【CRITICAL FIX】移除 fallback 添加的 guardingTag 和弹反标签
        if (_tagComponent != null)
        {
            var hbm = playerModel.GetComponent<HurtBoxManager>();
            if (hbm != null)
            {
                if (hbm.guardingTag != null) _tagComponent.RemoveTag(hbm.guardingTag);
                if (hbm.normalParryTag != null) _tagComponent.RemoveTag(hbm.normalParryTag);
                if (hbm.perfectParryTag != null) _tagComponent.RemoveTag(hbm.perfectParryTag);
                if (hbm.justGuardTag != null) _tagComponent.RemoveTag(hbm.justGuardTag);
            }
        }

        playerModel.isAttacking = false;
        playerModel.isDefending = false;

        // Fade out attack layer
        if (_attackLayer != null)
        {
            _attackLayer.StartFade(0f, 0.25f);
        }
    }

    private void ExitToGround()
    {
        if (playerController.isGround)
            playerModel.ChangePlayerState(PlayerState.ground);
        else
            playerModel.ChangePlayerState(PlayerState.sky);
    }

    private System.Collections.IEnumerator RemoveParryTagAfterDelay(GameplayTagSO tag, float delay)
    {
        yield return new WaitForSeconds(delay);
        _tagComponent?.RemoveTag(tag);
    }
}
