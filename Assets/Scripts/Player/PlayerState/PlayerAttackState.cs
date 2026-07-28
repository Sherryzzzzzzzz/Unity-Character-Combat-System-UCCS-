using UnityEngine;
using System.Collections;

enum AttackType
{
    light, heavy, skill, skyLight,defend
}

public class PlayerAttackState : PlayerStateBase
{
    private AttackType _currentAttackType;
    private bool _wasAimingOnEnter = false;
    
    // 【修改 1】新增一个布尔锁，防止多次触发翻滚
    private bool _isDodging = false;

    public override void Enter(object parameter = null)
    {
        base.Enter();
        
        // 【修改 2】进入状态时，重置锁
        _isDodging = false;

        if (parameter is AttackType attackType)
        {
            _currentAttackType = attackType;
        }
        else
        {
            _currentAttackType = AttackType.light;
            Debug.LogWarning("PlayerAttackState entered without a valid AttackType. Defaulting to Light.", playerModel.gameObject);
        }
        
        if (!playerModel.isComboChain)
        {
            playerModel.isAttacking = true;
        }

        SkillTimelineAsset startingSkill = GetStartingSkill(_currentAttackType);

        if (startingSkill == null)
        {
            Debug.LogError($"No starting skill found for AttackType: {_currentAttackType}. Reverting state.", playerModel.gameObject);
            ReturnToPreviousState(); 
            return;
        }
        
        playerModel.pac.PlaySkill(startingSkill);

        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnSkillEnd;
            playerModel.pac.OnSkillEnd += OnSkillEnd;
        }
    }
    
    private SkillTimelineAsset GetStartingSkill(AttackType type)
    {
        switch (type)
        {
            case AttackType.light: return playerModel.lightStart;
            case AttackType.heavy: return playerModel.heavyStart;
            case AttackType.skill: return playerModel.combatArtStart;
            case AttackType.skyLight: return playerModel.lightSkyStart;
            case AttackType.defend: return playerModel.defendStart;
            default: return null;
        }
    }
    
    public override void Update()
    {
        base.Update();
        
        // 如果已经触发了翻滚，就直接返回，不再执行后续的逻辑（移动取消、转向等）
        if (_isDodging) return;

        // --- 1. 优先处理“可取消出口”逻辑 ---
        if (playerController.dodge)
        {
            // 这里加了一个简单的非空判断，防止空引用
            if (playerModel.pac != null && playerModel.pac.CanBeCanceledBy(CancelActionType.Dodge))
            {
                OnDodgeButtonPressed(); 
                return; // 触发翻滚后直接退出 Update
            }
        }
        
        if (playerController.movement != Vector2.zero)
        {
            if (playerModel.pac.CanBeCanceledBy(CancelActionType.Move))
            {
                ReturnToPreviousState();
                return;
            }
        }
        
        // --- 2. 如果没有触发取消，则执行攻击状态的常规逻辑 ---
        
        if (playerModel.ts.HasTarget)
        {
            ForceRotationTowardsTarget();
        }
        
        if (playerController.lightAttack)
        {
            playerModel.tagComponent.AddTransientTag(playerModel.LightAttackInputTag);
        }
        else if (playerController.heavyAttack)
        {
            playerModel.tagComponent.AddTransientTag(playerModel.HeavyAttackInputTag);
        }
        else if (playerController.combatArt)
        {
            playerModel.tagComponent.AddTransientTag(playerModel.CombatArtInputTag);
        }
        else if (playerController.defend)
        {
            // 检查是否可以从当前攻击中取消进入格挡
            if (playerModel.pac != null && playerModel.pac.CanBeCanceledBy(CancelActionType.Guard))
            {
                // 停止当前技能并直接进入格挡状态
                playerModel.pac.StopAndCleanup(true, false);
                playerModel.ChangePlayerState(PlayerState.guard);
                return;
            }
            // 如果不可取消，则缓存输入（用于连招窗口）
            playerModel.tagComponent.AddTransientTag(playerModel.DefendInputTag);
        }
    }
    
    private void OnSkillEnd()
    {
        if (playerModel.isComboChain)
        {
            playerModel.isComboChain = false;
            if (playerModel.pac != null)
            {
                playerModel.pac.OnSkillEnd -= OnSkillEnd;
                playerModel.pac.OnSkillEnd += OnSkillEnd;
            }
        }
        else if (_currentAttackType == AttackType.defend && playerController.defendHeld)
        {
            playerModel.ChangePlayerState(PlayerState.guard);
        }
        else
        {
            ReturnToPreviousState();
        }
    }

    private void ReturnToPreviousState()
    {
        if (playerModel.ts.HasTarget)
        {
            playerModel.ChangePlayerState(PlayerState.aim);
        }
        else
        {
            if (playerController.isGround)
            {
                 playerModel.ChangePlayerState(PlayerState.ground);
            }
            else
            {
                 playerModel.ChangePlayerState(PlayerState.sky);
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        
        if (playerModel.pac != null)
        {
            playerModel.pac.OnSkillEnd -= OnSkillEnd;
        }
        
        playerModel.isAttacking = false;
        playerModel.isComboChain = false;
    }
    
    private void ForceRotationTowardsTarget()
    {
        if (playerModel.ts == null || !playerModel.ts.HasTarget) return;
        Vector3 directionToTarget = playerModel.ts.CurrentTarget.position - playerModel.transform.position;
        directionToTarget.y = 0;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, targetRotation, Time.deltaTime * 30f); 
    }
    
    void OnDodgeButtonPressed()
    {
        if (_isDodging) return;

        // ★ 确保体力已消耗（同帧幂等，不会重复扣）
        if (!playerController.TryConsumeDodgeStamina())
        {
            playerController.dodge = false;
            return;
        }

        _isDodging = true;

        // ★ 尝试完美闪避检测
        var dodgeAbility = playerModel.GetComponent<DodgeAbility>();
        if (dodgeAbility != null)
            dodgeAbility.AttemptDodge();

        Vector2 moveInput = playerController.movement;
        
        if (moveInput.magnitude < 0.1f) 
        { 
            playerModel.PlaySkill(playerModel.dodgeB); 
            return; 
        }

        Transform cameraTransform = PlayerController.Instance?.cameraTransform; if (cameraTransform == null) return;
        Transform playerTransform = playerModel.transform;
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        Vector3 desiredMoveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        Vector3 playerForward = playerTransform.forward;
        float angle = Vector3.Angle(playerForward, desiredMoveDirection);
        if (angle <= 45.0f) { playerModel.PlaySkill(playerModel.dodgeF); }
        else if (angle >= 135.0f) { playerModel.PlaySkill(playerModel.dodgeB); }
        else { float crossY = Vector3.Cross(playerForward, desiredMoveDirection).y; if (crossY > 0) { playerModel.PlaySkill(playerModel.dodgeR); } else { playerModel.PlaySkill(playerModel.dodgeL); } }
    }
}