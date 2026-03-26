/// <summary>
/// 等待延迟 Task — 到期后触发 OnTaskCompleted
/// </summary>
public class WaitDelayTask : AbilityTask
{
    private float _delaySeconds;
    private float _elapsedTime;

    public WaitDelayTask(float delaySeconds)
    {
        _delaySeconds = delaySeconds;
    }

    public override void Activate()
    {
        base.Activate();
        _elapsedTime = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (!IsActive || IsFinished) return;

        _elapsedTime += deltaTime;
        if (_elapsedTime >= _delaySeconds)
        {
            Complete();
        }
    }
}
