using System;
using UnityEngine;
using Animancer;

public class WaitAbilityActivateTask : AbilityTask
{
    public GameplayTagSO AbilityTagFilter;
    public Action<GameplayAbility> OnAbilityActivated;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitAbilityActivateTask Create(GameplayTagSO tagFilter, Action<GameplayAbility> onActivated)
    {
        return new WaitAbilityActivateTask
        {
            AbilityTagFilter = tagFilter, OnAbilityActivated = onActivated,
            OnlyTriggerOnce = true, WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        if (OwnerASC != null) OwnerASC.OnAbilityActivated += HandleAbilityActivated;
    }

    private void HandleAbilityActivated(GameplayAbility ability)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        if (AbilityTagFilter == null || ability.AbilityTags.Contains(AbilityTagFilter))
        {
            _triggered = true;
            OnAbilityActivated?.Invoke(ability);
            if (OnlyTriggerOnce) EndTask();
        }
    }

    protected override void OnDestroy()
    {
        if (OwnerASC != null) OwnerASC.OnAbilityActivated -= HandleAbilityActivated;
    }
}

public class WaitAbilityCommitTask : AbilityTask
{
    public Action<GameplayAbility> OnAbilityCommitted;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;

    public static WaitAbilityCommitTask Create(Action<GameplayAbility> onCommitted)
    {
        return new WaitAbilityCommitTask { OnAbilityCommitted = onCommitted, WaitState = EAbilityTaskWaitState.WaitingOnGame };
    }

    public override void Activate()
    {
        if (OwnerASC != null) OwnerASC.OnAbilityCommitted += Handle;
    }

    private void Handle(GameplayAbility a)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished) return;
        _triggered = true;
        OnAbilityCommitted?.Invoke(a);
        if (OnlyTriggerOnce) EndTask();
    }

    protected override void OnDestroy() { if (OwnerASC != null) OwnerASC.OnAbilityCommitted -= Handle; }
}

public class PlayAnimAndWaitTask : AbilityTask
{
    public ClipTransition Animation;
    public int LayerIndex = 1;
    public float FadeDuration = 0.2f;
    public Action OnCompleted;
    public Action OnBlendOut;
    public Action OnInterrupted;
    private AnimancerComponent _animancer;
    private AnimancerLayer _layer;
    private AnimancerState _state;

    public static PlayAnimAndWaitTask Create(ClipTransition anim, int layerIndex = 1,
        Action onCompleted = null, Action onBlendOut = null, Action onInterrupted = null)
    {
        return new PlayAnimAndWaitTask
        {
            Animation = anim, LayerIndex = layerIndex,
            OnCompleted = onCompleted, OnBlendOut = onBlendOut, OnInterrupted = onInterrupted,
            WaitState = EAbilityTaskWaitState.WaitingOnAvatar
        };
    }

    public override void Activate()
    {
        if (Animation?.Clip == null) { EndTask(); return; }
        _animancer = Owner?.GetComponent<AnimancerComponent>();
        if (_animancer == null) { EndTask(); return; }
        if (_animancer.Layers.Count <= LayerIndex) _animancer.Layers.Count = LayerIndex + 1;
        _layer = _animancer.Layers[LayerIndex];
        _layer.SetWeight(1f);
        _state = _layer.Play(Animation, FadeDuration);
        _state.Events(this).OnEnd = () => { OnCompleted?.Invoke(); EndTask(); };
    }

    public override void Tick(float deltaTime)
    {
        if (IsFinished) return;
        if (_state != null && _state.NormalizedTime >= 1f)
        {
            OnBlendOut?.Invoke();
            _layer?.StartFade(0f, 0.25f);
            EndTask();
        }
    }

    public override void Cancel()
    {
        OnInterrupted?.Invoke();
        _layer?.StartFade(0f, 0.15f);
        base.Cancel();
    }
}

public class SpawnActorTask : AbilityTask
{
    public GameObject ActorClass;
    public Vector3 SpawnLocation;
    public Quaternion SpawnRotation;
    public Transform ParentTransform;
    public Action<GameObject> OnSpawned;

    public static SpawnActorTask Create(GameObject actorClass, Vector3 location, Quaternion rotation,
        Transform parent, Action<GameObject> onSpawned)
    {
        return new SpawnActorTask
        {
            ActorClass = actorClass, SpawnLocation = location, SpawnRotation = rotation,
            ParentTransform = parent, OnSpawned = onSpawned,
            WaitState = EAbilityTaskWaitState.WaitingOnGame
        };
    }

    public override void Activate()
    {
        if (ActorClass == null) { EndTask(); return; }
        var spawned = UnityEngine.Object.Instantiate(ActorClass, SpawnLocation, SpawnRotation, ParentTransform);
        OnSpawned?.Invoke(spawned);
        EndTask();
    }
}

public class WaitMovementModeChangeTask : AbilityTask
{
    public enum MovementMode { Ground, Air, AnyChange }
    public MovementMode WaitForMode;
    public Action<MovementMode> OnModeChanged;
    public bool OnlyTriggerOnce = true;
    private bool _triggered;
    private bool _wasGrounded;
    private PlayerController _pc;

    public static WaitMovementModeChangeTask Create(MovementMode waitFor, Action<MovementMode> onChanged)
    {
        return new WaitMovementModeChangeTask
        {
            WaitForMode = waitFor, OnModeChanged = onChanged,
            WaitState = EAbilityTaskWaitState.WaitingOnAvatar
        };
    }

    public override void Activate()
    {
        _pc = Owner?.GetComponent<PlayerController>();
        _wasGrounded = _pc != null ? _pc.isGround : true;
    }

    public override void Tick(float deltaTime)
    {
        if (_triggered && OnlyTriggerOnce) return;
        if (IsFinished || _pc == null) return;
        bool isGrounded = _pc.isGround;
        if (isGrounded == _wasGrounded) return;
        _wasGrounded = isGrounded;
        var newMode = isGrounded ? MovementMode.Ground : MovementMode.Air;
        if (WaitForMode == MovementMode.AnyChange || WaitForMode == newMode)
        {
            _triggered = true;
            OnModeChanged?.Invoke(newMode);
            if (OnlyTriggerOnce) EndTask();
        }
    }
}
