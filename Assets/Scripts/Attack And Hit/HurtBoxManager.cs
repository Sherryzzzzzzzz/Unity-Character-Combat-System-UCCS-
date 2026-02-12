using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;
using Cinemachine;

public enum GameBodyPart
{
    Root, Torso, Head, LeftArm, LeftHand, RightArm, RightHand,
    LeftLeg, LeftFoot, RightLeg, RightFoot, Custom1, Custom2
}

// Inspector中身体部位与GameObject的映射
[Serializable]
public class BodyPartMapping
{
    public GameBodyPart part;
    public GameObject hurtBoxObject;
    [HideInInspector] public Transform boneTransform;
}

// 攻击强度枚举
public enum HitStrength { Light, Medium, Heavy, Blow }

[RequireComponent(typeof(CharacterController), typeof(AnimancerComponent))]
public class HurtBoxManager : MonoBehaviour
{
    [Header("弹反与格挡的 Tag 配置")]
    [Tooltip("代表“完美弹反”窗口的状态 Tag")]
    public GameplayTagSO perfectParryTag; // 在 Inspector 中拖入 "State.Parrying.Perfect"

    [Tooltip("代表“普通弹反”窗口的状态 Tag")]
    public GameplayTagSO normalParryTag;   // 在 Inspector 中拖入 "State.Parrying.Normal"

    [Tooltip("代表“持续防御”状态的 Tag")]
    public GameplayTagSO guardingTag;      // 在 Inspector 中拖入 "State.Guarding"

    [Tooltip("弹反成功时，施加给攻击者的事件 Tag")]
    public GameplayTagSO parrySuccessTag;  // 在 Inspector 中拖入 "Event.ParrySuccess"
    
    private TagComponent _tagComponent;
    
    [Header("HurtBox 映射")]
    public List<BodyPartMapping> bodyPartMappings = new();

    [Header("受击时长 (硬直时间)")]
    public float hitDurationLight = 0.7f;
    public float hitDurationMedium = 1.0f;
    public float hitDurationHeavy = 1.5f;
    public float hitDurationBlow = 3.0f;

    [Header("击退 / 击飞")]
    [Tooltip("击退效果的持续时间")]
    public float knockbackDuration = 0.25f;
    [Tooltip("击退速度随时间衰减的曲线")]
    public AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); // 默认从快到慢

    [Header("帧冻结 / Camera Impulse")]
    public float hitFreezeFrame = 0.03f;
    public float hitImpulseAmplitude = 1f;

    [Header("组件引用")]
    [Tooltip("包含所有受击动画的动画集资产")]
    public ExpandableAnimationSet animationSet;
    public HealthSystem healthSystem;
    
    // 公开事件，供其他系统订阅
    public Action<AttackEvent, HitStrength> OnHitCallback;
    public Action<float> OnMomentaryFreeze;
    
    [Header("Animancer 层级")]
    [Tooltip("受击动画播放所在的层级索引。必须大于基础移动层(通常是0)。")]
    public int hitLayerIndex = 2; // 使用第2层，第1层可能被攻击动画占用
    
    public bool isHitting;
    public bool isInvincible = false;
    
    [Tooltip("所有自动生成的 HurtBox 游戏对象都将存放在此 Transform 下")]
    public Transform hurtBoxContainer;

    private CinemachineImpulseSource _impulseSource;

    // --- 内部数据 ---
    private readonly Dictionary<GameBodyPart, GameObject> hurtBoxDict = new();
    private CharacterController cc;
    private AnimancerComponent animancer;
    private AnimancerLayer _hitLayer;
    private Coroutine _hitFlowCoroutine; // 统一管理整个受击流程的协程
    private HitStrength _currentHitStrength = HitStrength.Light;
    private Transform tf;

    // ---------------- Init ----------------
    private void Awake()
    {
        tf = transform;
        cc = GetComponent<CharacterController>();
        animancer = GetComponent<AnimancerComponent>();
        _tagComponent = GetComponent<TagComponent>();
        healthSystem = GetComponent<HealthSystem>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();

        // 初始化受击层
        if (animancer.Layers.Count <= hitLayerIndex)
        {
            animancer.Layers.Count = hitLayerIndex + 1;
        }
        _hitLayer = animancer.Layers[hitLayerIndex];
        _hitLayer.SetWeight(0); 

        // 初始化HurtBox字典
        foreach (var m in bodyPartMappings)
        {
            if (!m.hurtBoxObject) continue;

            m.boneTransform = m.hurtBoxObject.transform;
            m.hurtBoxObject.SetActive(false);
            hurtBoxDict[m.part] = m.hurtBoxObject;
        }
    }

    public void ProcessHit(AttackEvent hit, GameObject attacker)
    {
        if(isInvincible)
            return;
        
        // --- 优先级 1: 完美弹反 / 普通弹反 ---
        // 检查角色是否拥有任一弹反窗口 Tag
        if (_tagComponent.HasTag(perfectParryTag) || _tagComponent.HasTag(normalParryTag))
        {
            Debug.Log("弹反成功！");
            
            // a. 给攻击者施加“被弹反”的事件 Tag
            var attackerTagComponent = attacker.GetComponent<TagComponent>();
            if (attackerTagComponent != null && parrySuccessTag != null)
            {
                attackerTagComponent.AddTransientTag(parrySuccessTag);
            }

            // b. (可选) 播放弹反成功的特效和音效
            // PlayParrySuccessVFX();
            // PlayParrySuccessSound();

            // c. 立即中断，不执行任何受伤或格挡逻辑
            return;
        }

        // --- 优先级 2: 持续防御 / 格挡 ---
        // 如果没有成功弹反，再检查是否处于防御状态
        if (_tagComponent.HasTag(guardingTag))
        {
            Debug.Log("攻击被格挡！");

            var blockHitClip = animationSet.GetClip("Guard_Hit");
            if(blockHitClip != null)
            {
                _hitLayer.SetWeight(1f);
                var state = _hitLayer.Play(blockHitClip, 0.1f, FadeMode.FromStart);
                
                state.Events(this).OnEnd = () => 
                {
                    _hitLayer.StartFade(0f, 0.1f);
                };
            }
            return;
        }

        // --- 优先级 3: 正常受击 ---
        // 如果既没有弹反也没有格挡，就执行你之前的正常受击流程
        HitStrength incomingStrength = EvaluateStrength(hit);
        if (_hitFlowCoroutine != null && incomingStrength <= _currentHitStrength)
        {
            return; // 霸体逻辑
        }
        if (_hitFlowCoroutine != null)
        {
            StopCoroutine(_hitFlowCoroutine);
        }
        _hitFlowCoroutine = StartCoroutine(HitFlow(hit));
    }

/// <summary>
/// "击退等级减1" 的辅助方法。
/// </summary>
private AttackForceType ReduceForceType(AttackForceType originalType)
{
    if (originalType <= AttackForceType.Light)
        return AttackForceType.None;
    
    // 将原始等级的整数值减 1
    int reducedLevel = (int)originalType - 1;
    return (AttackForceType)reducedLevel;
}

    // ---------------- Core Flow ----------------
    private IEnumerator HitFlow(AttackEvent hit)
    {
        // 1. 设置状态
        _currentHitStrength = EvaluateStrength(hit);
        isHitting = true;
        float duration = GetHitDuration(_currentHitStrength);

        // 2. 帧冻结
        if (hitFreezeFrame > 0f)
        {
            OnMomentaryFreeze?.Invoke(hitFreezeFrame);
            if (Time.timeScale > 0.1f) // 避免在已经冻结时再次冻结
            {
                Time.timeScale = 0.01f;
                yield return new WaitForSecondsRealtime(hitFreezeFrame);
                Time.timeScale = 1.0f;
            }
        }

        // 3. 激活受击层并播放动画
        _hitLayer.SetWeight(1f); // 立即激活权重
        int dir4 = Resolve4Direction(hit);
        string animName = Compose4DirAnimation(_currentHitStrength, dir4);
        PlayHitAnimation(animName);

        Vector3 forceDirection = hit.GetForceDirection();
        // 4. 触发相机震动和回调
        if (_impulseSource != null)
        {
            // 可以传递击退方向，让晃动更有方向感
            _impulseSource.GenerateImpulseWithVelocity(forceDirection);
        }
        
        // 5. 启动并等待击退协程完成
        yield return StartCoroutine(ApplyKnockbackForce(hit));
        
        // 6. 等待剩余的硬直时间
        float remainingDuration = duration;
        if (remainingDuration > 0)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        // 7. 流程结束，清理状态
        _hitLayer.StartFade(0f, 0.25f); // 平滑地隐藏受击层
        isHitting = false;
        _hitFlowCoroutine = null; 
    }

    // ---------------- 推力协程 ----------------
    private IEnumerator ApplyKnockbackForce(AttackEvent hit)
    {
        // 只有中等及以上强度的攻击才产生击退
        if (hit.forceType < AttackForceType.Medium)
            yield break; // 退出协程

        float timer = 0f;
        Vector3 forceDirection = hit.GetForceDirection();
        forceDirection.y = 0;
        forceDirection.Normalize();
    
        float curveValue = knockbackCurve.Evaluate(timer / knockbackDuration);
        Vector3 moveVector = -forceDirection * hit.hitForce * curveValue * Time.deltaTime;
        cc.Move(moveVector);
        timer += Time.deltaTime;
        yield return null;
    }

    // ---------------- Four Direction ----------------
    private int Resolve4Direction(AttackEvent hit)
    {
        Vector3 attackDir = hit.GetForceDirection();
        attackDir.y = 0;
        attackDir.Normalize();
        float dot = Vector3.Dot(tf.forward, attackDir);
        float cross = Vector3.Cross(tf.forward, attackDir).y;
        if (dot > 0.707f) return 0;  // 前方 (45度角内)
        if (dot < -0.707f) return 2; // 后方
        if (cross > 0) return 3;     // 左方
        return 1;                    // 右方
    }

    private string FourDirectionName(int dir) => dir switch { 0 => "F", 1 => "R", 2 => "B", 3 => "L", _ => "F" };

    // ---------------- Strength / Duration ----------------
    private HitStrength EvaluateStrength(AttackEvent hit) => hit.forceType switch
    {
        AttackForceType.Light => HitStrength.Light,
        AttackForceType.Medium => HitStrength.Medium,
        AttackForceType.Heavy => HitStrength.Heavy,
        AttackForceType.Blow => HitStrength.Blow,
        _ => HitStrength.Light
    };

    private float GetHitDuration(HitStrength s) => s switch
    {
        HitStrength.Light => hitDurationLight,
        HitStrength.Medium => hitDurationMedium,
        HitStrength.Heavy => hitDurationHeavy,
        HitStrength.Blow => hitDurationBlow,
        _ => hitDurationLight
    };

    private string StrengthLetter(HitStrength s) => s switch
    {
        HitStrength.Light => "L",
        HitStrength.Medium => "M",
        HitStrength.Heavy => "H",
        HitStrength.Blow => "B",
        _ => "L"
    };

    private string Compose4DirAnimation(HitStrength s, int dir)
    {
        // 生成 "F_L", "B_H" 这样的名字
        return $"{FourDirectionName(dir)}_{StrengthLetter(s)}";
    }
    
    // ---------------- Animation ----------------
    private void PlayHitAnimation(string name)
    {
        if (animationSet == null) { Debug.LogWarning("HurtBoxManager: AnimationSet is not assigned!", this); return; }
        var clip = animationSet.GetClip(name);
        if (clip != null)
        {
            _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
        }
        else
        {
            Debug.LogWarning($"HurtBoxManager: Animation clip '{name}' not found in the AnimationSet.", this);
        }
    }

    // ---------------- HurtBox Control ----------------
    public void ActivateHurtBox(GameBodyPart p)
    {
        if (hurtBoxDict.TryGetValue(p, out var o)) o.SetActive(true);
    }

    public void DeactivateHurtBox(GameBodyPart p)
    {
        if (hurtBoxDict.TryGetValue(p, out var o)) o.SetActive(false);
    }
    
    public void ClearMappings()
    {
    bodyPartMappings.Clear();
    hurtBoxDict.Clear();
    }
    
    private void LateUpdate()
    {
        if (bodyPartMappings == null || bodyPartMappings.Count == 0) return;

        foreach (var mapping in bodyPartMappings)
        {
            if (mapping.hurtBoxObject != null && mapping.boneTransform != null)
            {
                // 将 HurtBox 的世界位置和旋转，强制设置为对应骨骼的世界位置和旋转
                mapping.hurtBoxObject.transform.position = mapping.boneTransform.position;
                mapping.hurtBoxObject.transform.rotation = mapping.boneTransform.rotation;
            }
        }
    }
}