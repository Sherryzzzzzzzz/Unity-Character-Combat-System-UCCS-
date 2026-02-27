using UnityEngine;

/// <summary>
/// GAS 系统测试组件 — 挂载到场景中任意有 ASC 的 GameObject 上
/// 在 Inspector 中配置测试效果，运行游戏后按键触发测试
///
/// 按键说明：
/// [T] 施加 Duration 效果（测试属性修改器和自动移除）
/// [Y] 施加周期伤害效果（测试 period Tick）
/// [U] 打印当前属性值（验证修改器聚合）
/// [I] 通过 EffectSpec 流程施加效果（测试快照和 Magnitude）
/// [O] 施加 Duration 效果后通过 Handle 移除（测试 Handle 系统）
/// </summary>
public class GASSystemTest : MonoBehaviour
{
    [Header("测试配置")]
    [Tooltip("Duration 测试效果（需在编辑器中创建并赋值）")]
    public GameplayEffect durationTestEffect;

    [Tooltip("周期伤害测试效果（需在编辑器中创建并赋值）")]
    public GameplayEffect periodicTestEffect;

    [Tooltip("用于 Spec 流程测试的 Instant 效果")]
    public GameplayEffect specTestEffect;

    private AbilitySystemComponent _asc;
    private AttributeSet _attr;
    private int _lastHandle = -1;

    private void Awake()
    {
        _asc = GetComponent<AbilitySystemComponent>();
        _attr = GetComponent<AttributeSet>();
    }

    private void Start()
    {
        // 监听属性变更事件
        if (_attr != null)
        {
            _attr.OnAttributeChanged += (attr, oldVal, newVal) =>
                Debug.Log($"[GAS Test] 属性变更: {attr} {oldVal:F1} → {newVal:F1}");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            TestDurationEffect();
        if (Input.GetKeyDown(KeyCode.Y))
            TestPeriodicEffect();
        if (Input.GetKeyDown(KeyCode.U))
            PrintAttributes();
        if (Input.GetKeyDown(KeyCode.I))
            TestEffectSpec();
        if (Input.GetKeyDown(KeyCode.O))
            TestHandleRemove();
    }

    private void TestDurationEffect()
    {
        if (durationTestEffect == null)
        {
            Debug.LogWarning("[GAS Test] durationTestEffect 未赋值，请在 Inspector 中配置");
            return;
        }

        Debug.Log($"[GAS Test] 施加 Duration 效果前 — AttackPower: {_attr.AttackPower}, Defense: {_attr.Defense}");
        try
        {
            _asc.ApplyGameplayEffect(durationTestEffect, _asc);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GASSystemTest: ApplyGameplayEffect 抛出异常: {e}");
        }
        Debug.Log($"[GAS Test] 施加 Duration 效果后 — AttackPower: {_attr.AttackPower}, Defense: {_attr.Defense}");
        Debug.Log($"[GAS Test] 效果将在 {durationTestEffect.duration} 秒后自动移除");
    }

    private void TestPeriodicEffect()
    {
        if (periodicTestEffect == null)
        {
            Debug.LogWarning("[GAS Test] periodicTestEffect 未赋值，请在 Inspector 中配置");
            return;
        }

        Debug.Log($"[GAS Test] 施加周期效果前 — Health: {_attr.Health}");
        try
        {
            _asc.ApplyGameplayEffect(periodicTestEffect, _asc);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GASSystemTest: ApplyGameplayEffect 抛出异常: {e}");
        }
        Debug.Log($"[GAS Test] 周期效果已施加 — 每 {periodicTestEffect.period} 秒触发一次，持续 {periodicTestEffect.duration} 秒");
    }

    private void PrintAttributes()
    {
        Debug.Log($"[GAS Test] Health: {_attr.Health}/{_attr.HealthMax}, " +
                  $"Poise: {_attr.Poise}/{_attr.PoiseMax}, " +
                  $"AttackPower: {_attr.AttackPower}, Defense: {_attr.Defense}");
    }

    /// <summary>
    /// 测试 EffectSpec 流程：MakeEffectSpec → 修改 → ApplyEffectSpec
    /// </summary>
    private void TestEffectSpec()
    {
        var testEffect = specTestEffect != null ? specTestEffect : durationTestEffect;
        if (testEffect == null)
        {
            Debug.LogWarning("[GAS Test] specTestEffect 和 durationTestEffect 均未赋值");
            return;
        }

        Debug.Log("[GAS Test] === EffectSpec 流程测试 ===");
        Debug.Log($"[GAS Test] 施加前 — Health: {_attr.Health}, AttackPower: {_attr.AttackPower}");

        // 通过 MakeEffectSpec 创建 Spec
        var spec = _asc.MakeEffectSpec(testEffect);
        Debug.Log($"[GAS Test] Spec 创建完成 — 快照 AttackPower: {(spec.CapturedAttackerAttributes.TryGetValue(GameplayAttribute.AttackPower, out float ap) ? ap : 0)}");

        // 施加 Spec
        int handle = -1;
        try
        {
            handle = _asc.ApplyEffectSpec(spec);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GASSystemTest: ApplyEffectSpec 抛出异常: {e}");
        }
        Debug.Log($"[GAS Test] ApplyEffectSpec 返回 Handle: {handle}");
        Debug.Log($"[GAS Test] 施加后 — Health: {_attr.Health}, AttackPower: {_attr.AttackPower}");
    }

    /// <summary>
    /// 测试 Handle 移除：施加 Duration 效果，记录 Handle，然后移除
    /// </summary>
    private void TestHandleRemove()
    {
        if (durationTestEffect == null)
        {
            Debug.LogWarning("[GAS Test] durationTestEffect 未赋值");
            return;
        }

        if (_lastHandle > 0)
        {
            // 已有 Handle，尝试移除
            Debug.Log($"[GAS Test] 尝试通过 Handle={_lastHandle} 移除效果...");
            Debug.Log($"[GAS Test] 移除前 — AttackPower: {_attr.AttackPower}, Defense: {_attr.Defense}");
            bool removed = _asc.RemoveActiveEffectByHandle(_lastHandle);
            Debug.Log($"[GAS Test] 移除结果: {(removed ? "成功" : "未找到")}");
            Debug.Log($"[GAS Test] 移除后 — AttackPower: {_attr.AttackPower}, Defense: {_attr.Defense}");
            _lastHandle = -1;
        }
        else
        {
            // 施加效果并记录 Handle
            var spec = _asc.MakeEffectSpec(durationTestEffect);
            _lastHandle = _asc.ApplyEffectSpec(spec);
            Debug.Log($"[GAS Test] 施加 Duration 效果，Handle={_lastHandle}");
            Debug.Log($"[GAS Test] 再次按 [O] 将通过 Handle 移除此效果");
        }
    }
}
