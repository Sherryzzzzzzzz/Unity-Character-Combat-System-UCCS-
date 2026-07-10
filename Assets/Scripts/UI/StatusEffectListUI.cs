using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 状态效果图标列表 UI。显示当前的 Buff/Debuff 图标及剩余时间。
/// 通过监听 TagComponent 和 AbilitySystemComponent 来追踪效果。
/// </summary>
public class StatusEffectListUI : MonoBehaviour
{
    [Header("配置")]
    public GameObject statusIconPrefab;  // 单个状态图标预制体
    public Transform iconContainer;       // 图标父容器
    public int maxIcons = 8;

    [Header("过滤")]
    [Tooltip("只显示拥有这些标签前缀的效果（如 'Buff.' / 'Debuff.'）")]
    public string tagPrefixFilter = "Buff.";

    private TagComponent _tagComponent;
    private AbilitySystemComponent _asc;
    private readonly Dictionary<GameplayTagSO, StatusEffectIcon> _activeIcons = new();
    private readonly Queue<StatusEffectIcon> _iconPool = new();

    private void Start()
    {
        if (iconContainer == null)
            iconContainer = transform;

        // 查找玩家的 TagComponent
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _tagComponent = player.GetComponent<TagComponent>();
            _asc = player.GetComponent<AbilitySystemComponent>();
        }
    }

    private void Update()
    {
        if (_tagComponent == null) return;

        // 刷新已有图标的时间
        RefreshIconDurations();
    }

    private void RefreshIconDurations()
    {
        if (_asc == null) return;

        var toRemove = new List<GameplayTagSO>();

        foreach (var kvp in _activeIcons)
        {
            var tag = kvp.Key;
            var icon = kvp.Value;

            // 检查标签是否仍然活跃
            if (!_tagComponent.HasTag(tag))
            {
                toRemove.Add(tag);
                continue;
            }

            // 更新剩余时间（从 ASC 的活跃效果中查找）
            float remainingTime = GetEffectRemainingTime(tag);
            icon.SetRemainingTime(remainingTime);
        }

        // 移除已过期的图标
        foreach (var tag in toRemove)
        {
            RemoveIcon(tag);
        }
    }

    private float GetEffectRemainingTime(GameplayTagSO tag)
    {
        // 通过反射访问 ASC 的活跃效果列表
        var effectsField = typeof(AbilitySystemComponent).GetField("_activeEffects",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (effectsField == null) return -1f;

        var activeEffects = effectsField.GetValue(_asc) as List<ActiveGameplayEffect>;
        if (activeEffects == null) return -1f;

        foreach (var effect in activeEffects)
        {
            if (effect.EffectData == null) continue;
            foreach (var grantedTag in effect.EffectData.grantedTags)
            {
                if (grantedTag == tag)
                {
                    return effect.TimeRemaining;
                }
            }
        }
        return -1f;
    }

    /// <summary>
    /// 由 TagComponent.OnTagAdded 事件驱动，添加效果图标
    /// </summary>
    public void OnTagAdded(GameplayTagSO tag)
    {
        if (tag == null) return;
        if (_activeIcons.ContainsKey(tag)) return;

        // 检查标签前缀过滤
        string tagName = tag.name;
        if (!string.IsNullOrEmpty(tagPrefixFilter) && !tagName.StartsWith(tagPrefixFilter))
            return;

        // 检查是否是父标签类型的过滤（遍历 parentTag 链）
        var current = tag;
        bool matchesFilter = false;
        while (current != null)
        {
            if (current.name.StartsWith(tagPrefixFilter))
            {
                matchesFilter = true;
                break;
            }
            current = current.parentTag;
        }
        if (!matchesFilter) return;

        if (_activeIcons.Count >= maxIcons) return;

        var icon = GetOrCreateIcon();
        icon.Initialize(tag);
        _activeIcons[tag] = icon;

        Debug.Log($"StatusEffectListUI: Added effect icon for tag '{tagName}'");
    }

    private void RemoveIcon(GameplayTagSO tag)
    {
        if (!_activeIcons.TryGetValue(tag, out var icon)) return;
        _activeIcons.Remove(tag);

        icon.gameObject.SetActive(false);
        if (_iconPool.Count < maxIcons * 2)
            _iconPool.Enqueue(icon);
        else
            Destroy(icon.gameObject);
    }

    private StatusEffectIcon GetOrCreateIcon()
    {
        if (_iconPool.Count > 0)
        {
            var icon = _iconPool.Dequeue();
            icon.gameObject.SetActive(true);
            return icon;
        }

        if (statusIconPrefab != null)
        {
            var go = Instantiate(statusIconPrefab, iconContainer);
            var icon = go.GetComponent<StatusEffectIcon>();
            if (icon == null) icon = go.AddComponent<StatusEffectIcon>();
            return icon;
        }

        // Fallback: 创建默认图标
        var defaultGo = new GameObject("EffectIcon", typeof(Image), typeof(StatusEffectIcon));
        defaultGo.transform.SetParent(iconContainer, false);
        var img = defaultGo.GetComponent<Image>();
        img.rectTransform.sizeDelta = new Vector2(40, 40);
        return defaultGo.GetComponent<StatusEffectIcon>();
    }

    private void OnDestroy()
    {
        if (_tagComponent != null)
            _tagComponent.OnTagAdded -= OnTagAdded;
    }
}

/// <summary>
/// 单个状态效果图标的 MonoBehaviour
/// </summary>
public class StatusEffectIcon : MonoBehaviour
{
    public Image iconImage;
    public Image durationFill;  // 径向遮罩
    public Text stackText;

    private GameplayTagSO _tag;

    private void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (durationFill == null)
        {
            var fill = transform.Find("DurationFill");
            if (fill != null) durationFill = fill.GetComponent<Image>();
        }
        if (stackText == null)
        {
            var st = transform.Find("StackText");
            if (st != null) stackText = st.GetComponent<Text>();
        }
    }

    public void Initialize(GameplayTagSO tag)
    {
        _tag = tag;
        if (iconImage != null)
            iconImage.color = Color.white;
        if (stackText != null)
            stackText.text = "";
    }

    public void SetRemainingTime(float remainingTime)
    {
        // remainingTime < 0 表示永久效果
        if (durationFill != null)
            durationFill.gameObject.SetActive(remainingTime >= 0f);
    }

    public void SetStackCount(int count)
    {
        if (stackText != null)
            stackText.text = count > 1 ? count.ToString() : "";
    }
}
