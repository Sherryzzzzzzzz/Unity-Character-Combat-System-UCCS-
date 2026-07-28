using UnityEngine;

/// <summary>行为树运行器 — 挂载在敌人 GameObject 上执行行为树</summary>
public class BTreeRunner : MonoBehaviour
{
    [Header("行为树资产")]
    public BTreeAsset treeAsset;

    [Header("运行参数")]
    [Tooltip("Tick 间隔（秒），0 = 每帧。建议 AI 用 0.1~0.2")]
    public float tickInterval = 0.15f;
    [Tooltip("Start 时自动开始运行")]
    public bool runOnStart = true;

    /// <summary>运行时黑板</summary>
    public BTBlackboard Blackboard { get; private set; } = new();

    /// <summary>是否已启动（Play 后为 true，Stop 后为 false）</summary>
    public bool IsRunning => _running;

    private BTNode _rootInstance;
    private bool _running;
    private float _tickTimer;
    private Transform _cachedPlayer;

    /// <summary>缓存常用组件引用</summary>
    public TagComponent Tags { get; private set; }

    // ── Unity 生命周期 ──────────────────────────────

    private void Start()
    {
        // 缓存常用组件，避免行为树节点每帧 GetComponent
        Tags = GetComponent<TagComponent>();

        if (runOnStart) Play();
    }

    private void Update()
    {
        if (!_running || _rootInstance == null) return;

        if (tickInterval > 0f)
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < tickInterval) return;
            _tickTimer -= tickInterval;
        }

        // Tick
        var result = _rootInstance.OnTick();

        // 树完成一轮 → 立即重入
        if (result != BTNodeState.Running)
        {
            _rootInstance.OnExit();
            _rootInstance.Reset();
            _rootInstance.OnEnter(this);
        }
    }

    // ── 公共 API ────────────────────────────────────

    public void Play()
    {
        if (treeAsset == null)
        {
            Debug.LogWarning($"BTreeRunner on '{gameObject.name}': treeAsset is null", this);
            return;
        }

        if (_running) return; // 防止重复 Play
        Stop();

        Blackboard.Initialize(treeAsset.blackboard);

        // 缓存 player 引用（只在 Play 时查找一次，不在每帧 Update 里找）
        if (_cachedPlayer == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _cachedPlayer = playerGo.transform;
        }
        if (_cachedPlayer != null) Blackboard.Set("player", _cachedPlayer);

        _rootInstance = CloneNode(treeAsset.rootNode);
        if (_rootInstance != null)
        {
            _rootInstance.OnEnter(this);
            _running = true;
        }
    }

    public void Pause()
    {
        if (_running && _rootInstance != null)
            _rootInstance.OnExit();
        _running = false;
    }

    public void Stop()
    {
        if (_running && _rootInstance != null)
        {
            _rootInstance.OnExit();
            _rootInstance.Reset();
        }
        _running = false;
        _rootInstance = null;
        _tickTimer = 0f;
    }

    // ── 辅助 ────────────────────────────────────────

    /// <summary>
    /// 深拷贝整棵行为树节点。
    /// [SerializeReference] + JsonUtility 会自动递归处理子节点，无需手动遍历。
    /// 注意：先操作子节点引用再序列化会直接修改原始 asset 数据，是严重 bug。
    /// </summary>
    private static BTNode CloneNode(BTNode node)
    {
        if (node == null) return null;

        // JsonUtility 配合 [SerializeReference] 会自动递归深拷贝整个子树
        var json = JsonUtility.ToJson(node);
        return JsonUtility.FromJson(json, node.GetType()) as BTNode;
    }

    private void OnDestroy()
    {
        Stop();
    }
}
