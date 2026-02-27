using System.Collections;
using UnityEngine;
using Animancer;
using Cinemachine;

[RequireComponent(typeof(CharacterController), typeof(AnimancerComponent))]
public class HitReactionController : MonoBehaviour
{
    public float hitDurationLight = 0.7f;
    public float hitDurationMedium = 1.0f;
    public float hitDurationHeavy = 1.5f;
    public float hitDurationBlow = 3.0f;

    public float knockbackDuration = 0.25f;
    public AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    public float hitFreezeFrame = 0.03f;
    public float hitImpulseAmplitude = 1f;

    public ExpandableAnimationSet animationSet;

    public int hitLayerIndex = 2;

    public bool isHitting;

    private CharacterController cc;
    private AnimancerComponent animancer;
    private AnimancerLayer _hitLayer;
    private CinemachineImpulseSource _impulseSource;
    private Coroutine _hitFlowCoroutine;
    private HitStrength _currentHitStrength;
    private Transform tf;

    private void Awake()
    {
        tf = transform;
        cc = GetComponent<CharacterController>();
        animancer = GetComponent<AnimancerComponent>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();

        if (animancer.Layers.Count <= hitLayerIndex)
            animancer.Layers.Count = hitLayerIndex + 1;

        _hitLayer = animancer.Layers[hitLayerIndex];
        _hitLayer.SetWeight(0);
    }

    public void PlayHit(AttackEvent hit)
    {
        HitStrength incomingStrength = EvaluateStrength(hit);

        if (_hitFlowCoroutine != null && incomingStrength <= _currentHitStrength)
            return;

        if (_hitFlowCoroutine != null)
            StopCoroutine(_hitFlowCoroutine);

        _hitFlowCoroutine = StartCoroutine(HitFlow(hit));
    }

    private IEnumerator HitFlow(AttackEvent hit)
    {
        _currentHitStrength = EvaluateStrength(hit);
        isHitting = true;

        float duration = GetHitDuration(_currentHitStrength);

        // 冻帧
        if (hitFreezeFrame > 0f)
        {
            Time.timeScale = 0.01f;
            yield return new WaitForSecondsRealtime(hitFreezeFrame);
            Time.timeScale = 1f;
        }

        // 播动画
        _hitLayer.SetWeight(1f);
        int dir4 = Resolve4Direction(hit);
        string animName = Compose4DirAnimation(_currentHitStrength, dir4);
        PlayHitAnimation(animName);

        // 相机震动
        if (_impulseSource != null)
            _impulseSource.GenerateImpulseWithVelocity(hit.GetForceDirection());

        // 击退
        yield return StartCoroutine(ApplyKnockbackForce(hit));

        yield return new WaitForSeconds(duration);

        _hitLayer.StartFade(0f, 0.25f);
        isHitting = false;
        _hitFlowCoroutine = null;
    }

    private IEnumerator ApplyKnockbackForce(AttackEvent hit)
    {
        if (hit.attackData?.forceType < AttackForceType.Medium)
            yield break;

        float timer = 0f;

        while (timer < knockbackDuration)
        {
            Vector3 dir = hit.GetForceDirection();
            dir.y = 0;
            dir.Normalize();

            float curve = knockbackCurve.Evaluate(timer / knockbackDuration);
            Vector3 move = -dir * hit.attackData.hitForce * curve * Time.deltaTime;
            cc.Move(move);

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private HitStrength EvaluateStrength(AttackEvent hit) => hit.attackData?.forceType switch
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

    private int Resolve4Direction(AttackEvent hit)
    {
        Vector3 attackDir = hit.GetForceDirection();
        attackDir.y = 0;
        attackDir.Normalize();

        float dot = Vector3.Dot(tf.forward, attackDir);
        float cross = Vector3.Cross(tf.forward, attackDir).y;

        if (dot > 0.707f) return 0;
        if (dot < -0.707f) return 2;
        if (cross > 0) return 3;
        return 1;
    }

    private string FourDirectionName(int dir) => dir switch
    {
        0 => "F",
        1 => "R",
        2 => "B",
        3 => "L",
        _ => "F"
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
        => $"{FourDirectionName(dir)}_{StrengthLetter(s)}";

    private void PlayHitAnimation(string name)
    {
        if (animationSet == null) return;

        var clip = animationSet.GetClip(name);
        if (clip != null)
            _hitLayer.Play(clip, 0.1f, FadeMode.FromStart);
    }
}

public enum HitStrength { Light, Medium, Heavy, Blow }