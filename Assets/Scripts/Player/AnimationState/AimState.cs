using UnityEngine;

/// <summary>
/// 锁敌动画态（补全 PlayerAnimationState.aim）：
/// 继承 MoveState 的移动混合，叠加"面向目标"平滑转向。
/// 转向职责收敛到动画层——逻辑层 GroundAimState 不再重复 Slerp。
/// 目标丢失时自动回 idle（由 AimState 自身检测，无需逻辑层干预）。
/// </summary>
public class AimState : MoveState
{
    public override void Update()
    {
        base.Update();

        // 锁敌转向：面向目标（只有目标存在时保持 aim 态）
        if (playerModel.ts != null && playerModel.ts.HasTarget && playerModel.ts.CurrentTarget != null)
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
        else
        {
            // 目标丢失 → 回地面动画态
            playerModel.ChangeAnimationState(PlayerAnimationState.idle);
        }
    }
}
