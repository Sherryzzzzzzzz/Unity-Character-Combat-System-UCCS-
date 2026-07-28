using UnityEngine;
using Animancer;

public static class EnemyAnimationLogic
{
    private const float BLEND_SMOOTH_SPEED = 8f;

    /// <summary>
    /// 更新处于“移动”状态的敌人的动画。
    /// </summary>
    /// <param name="data">需要被更新的那个敌人的数据组件。</param>
    private const float RUN_SPEED_THRESHOLD = 4f; // moveDir.magnitude > 此值 → 跑

    public static void UpdateMoveState(EnemyAnimationData data)
    {
        float speed = data.Model.moveDir.magnitude; // moveDir 现在是 direction*speed
        bool isRunning = speed > RUN_SPEED_THRESHOLD;

        if (isRunning)
        {
            if (!data.Animancer.IsPlaying(data.RunClip))
                data.Animancer.Play(data.RunClip, 0.25f, FadeMode.FixedSpeed);

            Vector3 dir = new Vector3(data.Model.moveDir.x, 0, data.Model.moveDir.y).normalized;
            if (dir.sqrMagnitude > 0.01f)
            {
                data.transform.rotation = Quaternion.Slerp(
                    data.transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }
        }
        else
        {
            if (!data.WalkMixer.IsPlaying)
                data.Animancer.Play(data.WalkMixer, 0.25f, FadeMode.FixedSpeed);

            float angleRad = data.Model.angle * Mathf.Deg2Rad;
            float targetX = Mathf.Sin(angleRad);
            float targetY = Mathf.Cos(angleRad);
            Vector2 targetParameter = new Vector2(targetX, targetY) * Mathf.Clamp01(speed / data.Model.speed);
            data.MixerParameter = Vector2.MoveTowards(data.MixerParameter, targetParameter, BLEND_SMOOTH_SPEED * Time.deltaTime);
            data.WalkMixer.Parameter = data.MixerParameter;
        }
    }

    public static void UpdateIdleState(EnemyAnimationData data)
    {
        if (!data.Animancer.IsPlaying(data.IdleClip))
            data.Animancer.Play(data.IdleClip, 0.25f, FadeMode.FixedSpeed);
    }

    /// <summary>
    /// 死亡动画：播放一次死亡动画（不循环），使用 FromStart 确保从头播放
    /// </summary>
    public static void UpdateDeathState(EnemyAnimationData data)
    {
        if (data.DeathClip != null)
        {
            // 只在首次进入 Death 状态时播放（避免每帧重新开始）
            if (!data.Animancer.IsPlaying(data.DeathClip))
                data.Animancer.Play(data.DeathClip, 0.1f, FadeMode.FromStart);
        }
    }
}