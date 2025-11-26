// 文件名: PlayerParryState.cs (最终简化版 - 直接使用Tag)
using UnityEngine;
using System.Collections;

public class PlayerParryState : PlayerStateBase
{
    // --- 直接引用 GameplayTagSO ---
    private GameplayTagSO _perfectParryTag;
    private GameplayTagSO _normalParryTag;
    private GameplayTagSO _guardStanceTag;

    private Coroutine _parryWindowCoroutine;
    private bool _initializedCorrectly = false;

    public override void Init(IStateOwner owner)
    {
        base.Init(owner);
        
        _perfectParryTag = playerModel.PerfectParryTag;
        _normalParryTag = playerModel.NormalParryTag;
        _guardStanceTag = playerModel.GuardStanceTag;

        if (_perfectParryTag == null || _normalParryTag == null || _guardStanceTag == null)
        {
            Debug.LogError("PlayerParryState: 一个或多个弹反/防御 GameplayTagSO 未在 PlayerModel 中分配！", playerModel.gameObject);
            _initializedCorrectly = false;
        }
        else
        {
            _initializedCorrectly = true;
        }
    }

    public override void Enter()
    {
        base.Enter();
        if (!_initializedCorrectly)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
            return;
        }

        // 请求表现层播放动画
        playerModel.ChangeAnimationState(PlayerAnimationState.parry);
        
        // 启动协程来管理 Tag 的生命周期
        if (_parryWindowCoroutine != null) playerModel.StopCoroutine(_parryWindowCoroutine);
        _parryWindowCoroutine = playerModel.StartCoroutine(ParryWindowCoroutine());
    }

    private IEnumerator ParryWindowCoroutine()
    {
        // --- 核心修改: 直接使用 AddTag 和 RemoveTag ---

        // --- 完美弹反窗口 (0-30帧) ---
        playerModel.tagComponent.AddTag(_perfectParryTag);
        yield return new WaitForSeconds(30f / 60f); // 假设 60 FPS
        playerModel.tagComponent.RemoveTag(_perfectParryTag);

        // --- 普通弹反窗口 (31-80帧) ---
        playerModel.tagComponent.AddTag(_normalParryTag);
        yield return new WaitForSeconds(50f / 60f);
        playerModel.tagComponent.RemoveTag(_normalParryTag);

        // --- 弹反窗口结束，自动进入持续防御状态 ---
        playerModel.tagComponent.AddTag(_guardStanceTag);
        
        _parryWindowCoroutine = null; // 标记协程任务已完成
    }
    
    public override void Update()
    {
        // 退出逻辑保持不变
        if (!playerController.defend)
        {
            playerModel.ChangePlayerState(PlayerState.ground);
        }
    }

    public override void Exit()
    {
        base.Exit();
        // 清理所有协程
        if (_parryWindowCoroutine != null)
        {
            playerModel.StopCoroutine(_parryWindowCoroutine);
            _parryWindowCoroutine = null;
        }
        if (playerModel != null && playerModel.tagComponent != null)
        {
            playerModel.tagComponent.RemoveTag(_perfectParryTag);
            playerModel.tagComponent.RemoveTag(_normalParryTag);
            playerModel.tagComponent.RemoveTag(_guardStanceTag);
        }
    }
}