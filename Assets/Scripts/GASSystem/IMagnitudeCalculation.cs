/// <summary>
/// 自定义 Magnitude 计算接口
/// 实现类应继承 ScriptableObject 以便在 Inspector 中引用
/// </summary>
public interface IMagnitudeCalculation
{
    /// <summary>
    /// 根据 GameplayEffectSpec 上下文计算 Magnitude 值
    /// </summary>
    float CalculateMagnitude(GameplayEffectSpec spec);
}
