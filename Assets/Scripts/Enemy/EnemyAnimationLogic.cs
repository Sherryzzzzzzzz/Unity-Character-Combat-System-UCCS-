using UnityEngine;
using Animancer;

public static class EnemyAnimationLogic
{
    private const float BLEND_SMOOTH_SPEED = 8f;

    /// <summary>
    /// 更新处于“移动”状态的敌人的动画。
    /// </summary>
    /// <param name="data">需要被更新的那个敌人的数据组件。</param>
    public static void UpdateMoveState(EnemyAnimationData data)
    {
        float moveMagnitude = data.Model.moveDir.magnitude;

        if (data.Model.isRunning)
        {
            // 奔跑逻辑
            data.Animancer.Play(data.RunClip, 0.25f, FadeMode.FixedSpeed);
            Vector3 moveDirection = new Vector3(data.Model.moveDir.x, 0, data.Model.moveDir.y);
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                data.transform.rotation = Quaternion.Slerp(data.transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
        else
        {
            // 行走/扫射逻辑
            data.Animancer.Play(data.WalkMixer, 0.25f, FadeMode.FixedSpeed);
            float angleRad = data.Model.angle * Mathf.Deg2Rad;
            float targetX = Mathf.Sin(angleRad);
            float targetY = Mathf.Cos(angleRad);
            Vector2 targetParameter = new Vector2(targetX, targetY) * Mathf.Clamp01(moveMagnitude);
            data.MixerParameter = Vector2.MoveTowards(data.MixerParameter, targetParameter, BLEND_SMOOTH_SPEED * Time.deltaTime);
            data.WalkMixer.Parameter = data.MixerParameter;
        }
    }
    
    public static void UpdateIdleState(EnemyAnimationData data)
    {
        data.Animancer.Play(data.IdleClip, 0.25f, FadeMode.FixedSpeed);
    }
}