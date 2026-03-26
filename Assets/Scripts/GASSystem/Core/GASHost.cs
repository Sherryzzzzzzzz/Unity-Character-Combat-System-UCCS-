using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GAS 全局管理器 — 集中 Tick 所有 ASC，提供全局时间控制
/// </summary>
public class GASHost : MonoBehaviour
{
    private static GASHost _instance;

    public static GASHost Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<GASHost>();
            return _instance;
        }
    }

    private readonly List<AbilitySystemComponent> _registeredASCs = new List<AbilitySystemComponent>();

    /// <summary>
    /// 全局时间缩放（1 = 正常，0 = 暂停，0.5 = 慢动作）
    /// </summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>
    /// 经过 TimeScale 缩放后的 DeltaTime
    /// </summary>
    public float DeltaTime => Time.deltaTime * TimeScale;

    /// <summary>
    /// 是否暂停
    /// </summary>
    public bool IsPaused => TimeScale <= 0f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (IsPaused) return;

        float dt = DeltaTime;
        for (int i = _registeredASCs.Count - 1; i >= 0; i--)
        {
            var asc = _registeredASCs[i];
            if (asc == null)
            {
                _registeredASCs.RemoveAt(i);
                continue;
            }
            asc.TickFromHost(dt);
        }
    }

    /// <summary>
    /// 注册 ASC
    /// </summary>
    public void RegisterASC(AbilitySystemComponent asc)
    {
        if (asc != null && !_registeredASCs.Contains(asc))
            _registeredASCs.Add(asc);
    }

    /// <summary>
    /// 注销 ASC
    /// </summary>
    public void UnregisterASC(AbilitySystemComponent asc)
    {
        _registeredASCs.Remove(asc);
    }
}
