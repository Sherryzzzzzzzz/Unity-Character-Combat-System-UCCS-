using UnityEngine;

/// <summary>
/// GAS 全局配置 — 对应 UE5 UAbilitySystemGlobals
/// 单例，通过 Resources.Load 或在 Awake 中自动创建
/// </summary>
[CreateAssetMenu(menuName = "GAS-like/AbilitySystemGlobals", fileName = "AbilitySystemGlobals")]
public class AbilitySystemGlobals : ScriptableObject
{
    private static AbilitySystemGlobals _instance;

    public static AbilitySystemGlobals Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AbilitySystemGlobals>("AbilitySystemGlobals");
                if (_instance == null)
                {
                    Debug.LogWarning("AbilitySystemGlobals: Not found in Resources. Using default settings.");
                    _instance = CreateInstance<AbilitySystemGlobals>();
                }
            }
            return _instance;
        }
    }

    [Header("Prediction")]
    [Tooltip("是否启用客户端预测（单机永远为false）")]
    public bool EnablePrediction = false;
    [Tooltip("预测Key超时时间（秒）")]
    public float PredictionKeyTimeout = 5f;

    [Header("GameplayEffect")]
    [Tooltip("全局 CurveTable 资源（暂未实现，预留）")]
    public ScriptableObject GlobalCurveTable;
    [Tooltip("GE 最大等级")]
    public int MaxGameplayEffectLevel = 20;

    [Header("GameplayCue")]
    [Tooltip("Cue 的最大同时活跃数")]
    public int MaxSimultaneousGameplayCues = 50;
    [Tooltip("Cue 回收池大小")]
    public int GameplayCuePoolSize = 10;

    [Header("Debug")]
    [Tooltip("启用 GAS 调试日志")]
    public bool EnableGASDebugLog = false;
    [Tooltip("显示 GameplayEffect 应用的详细日志")]
    public bool VerboseEffectApplication = false;

    [Header("Actor Info")]
    [Tooltip("全局 Replicate ActivationOwnedTags（单机永远为false）")]
    public bool ReplicateActivationOwnedTags = false;

    /// <summary>
    /// 检查是否允许预测
    /// </summary>
    public bool ShouldPredict() => EnablePrediction;

    /// <summary>
    /// 获取 GE 的最大有效等级
    /// </summary>
    public int GetMaxLevel() => MaxGameplayEffectLevel;

    /// <summary>
    /// 全局日志（仅在 EnableGASDebugLog 时输出）
    /// </summary>
    public static void Log(string message, Object context = null)
    {
        if (Instance.EnableGASDebugLog)
            Debug.Log($"[GAS] {message}", context);
    }

    public static void LogVerbose(string message, Object context = null)
    {
        if (Instance.EnableGASDebugLog && Instance.VerboseEffectApplication)
            Debug.Log($"[GAS:Verbose] {message}", context);
    }
}
