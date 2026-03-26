/// <summary>
/// 结束技能 Task — Activate 时立即调用 OwnerAbility.End()
/// </summary>
public class EndAbilityTask : AbilityTask
{
    public override void Activate()
    {
        base.Activate();
        OwnerAbility?.End();
        Complete();
    }
}
