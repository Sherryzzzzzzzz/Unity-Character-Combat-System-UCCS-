using System.Collections;
using UnityEngine;

/// <summary>
/// 全局慢动作导演（鬼泣式战斗时间感）。
/// 负责管理 Time.timeScale 的临时缩放：拼刀定格、Just Guard（完美格挡）慢动作等。
/// 使用"最后请求者优先"策略，多个慢动作请求并发时不会互相踩踏导致时间卡死。
/// 恢复协程使用 WaitForSecondsRealtime，避免被自身缩放影响。
/// </summary>
public class TimeScaleDirector : MonoBehaviour
{
    private static TimeScaleDirector _instance;

    /// <summary>全局单例。场景中不存在时惰性创建（无场景配置成本）。</summary>
    public static TimeScaleDirector Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[TimeScaleDirector]");
                _instance = go.AddComponent<TimeScaleDirector>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private Coroutine _activeRoutine;
    private float _baseTimeScale = 1f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _baseTimeScale = Time.timeScale;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// 触发一次慢动作。
    /// </summary>
    /// <param name="targetScale">目标时间缩放（0.05 = 5% 速度，极慢定格；0.15 适合完美格挡）</param>
    /// <param name="duration">持续真实秒数（不受缩放影响）</param>
    /// <param name="restoreImmediately">是否跳过缓动直接恢复（false 时使用 SmoothRestore）</param>
    public void DoSlowMotion(float targetScale, float duration, bool restoreImmediately = true)
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        targetScale = Mathf.Clamp(targetScale, 0.01f, 1f);
        Time.timeScale = targetScale;

        _activeRoutine = StartCoroutine(restoreImmediately
            ? RestoreRoutine(duration)
            : SmoothRestoreRoutine(duration, targetScale));
    }

    /// <summary>立即恢复正常时间</summary>
    public void RestoreNow()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        Time.timeScale = _baseTimeScale;
    }

    private IEnumerator RestoreRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = _baseTimeScale;
        _activeRoutine = null;
    }

    /// <summary>
    /// 缓动恢复：慢动作结束后用约 0.18s 从 targetScale 平滑回到基准，
    /// 产生"时间回弹"的冲击感（鬼泣 Just Guard 的招牌效果）。
    /// </summary>
    private IEnumerator SmoothRestoreRoutine(float duration, float fromScale)
    {
        yield return new WaitForSecondsRealtime(duration);

        const float smoothDuration = 0.18f;
        float elapsed = 0f;
        while (elapsed < smoothDuration)
        {
            float t = elapsed / smoothDuration;
            // ease-out：先快后慢，避免时间"回弹"显得生硬
            float eased = 1f - (1f - t) * (1f - t);
            Time.timeScale = Mathf.Lerp(fromScale, _baseTimeScale, eased);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = _baseTimeScale;
        _activeRoutine = null;
    }

    private void OnApplicationQuit()
    {
        // 编辑器退出/停止时恢复，防止 timeScale 卡在慢动作
        Time.timeScale = 1f;
    }
}
