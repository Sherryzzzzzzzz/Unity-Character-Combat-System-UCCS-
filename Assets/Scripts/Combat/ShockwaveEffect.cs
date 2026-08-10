using System.Collections;
using UnityEngine;

/// <summary>
/// 气浪/冲击波 — 纯程序化粒子系统
/// </summary>
public class ShockwaveEffect : MonoBehaviour
{
    public static void SpawnSlashWave(Vector3 position, AttackForceType forceType)
    {
        float r = forceType switch
        {
            AttackForceType.Light => 2f,
            AttackForceType.Medium => 3.5f,
            AttackForceType.Heavy => 5f,
            AttackForceType.Blow => 7f,
            _ => 2f
        };
        Color c = forceType switch
        {
            AttackForceType.Light => new Color(1f, 0.95f, 0.9f),
            AttackForceType.Medium => new Color(1f, 0.9f, 0.85f),
            AttackForceType.Heavy => new Color(1f, 0.85f, 0.8f),
            AttackForceType.Blow => new Color(1f, 0.8f, 0.75f),
            _ => Color.white
        };
        Spawn(position, c, r, 0.5f);
    }

    public static void SpawnGuardBreak(Vector3 position)
    {
        Spawn(position, new Color(1f, 0.9f, 0.8f), 5f, 0.6f);
    }

    public static void SpawnPerfectDodgeWave(Vector3 position)
    {
        Spawn(position, new Color(1f, 1f, 0.95f), 4f, 0.45f);
    }

    private static void Spawn(Vector3 position, Color color, float radius, float duration)
    {
        var root = new GameObject("Shockwave");
        root.transform.position = position + Vector3.up * 0.3f;
        var fx = root.AddComponent<ShockwaveEffect>();
        fx.StartCoroutine(fx.Play(color, radius, duration));
        Destroy(root, duration + 1f);
    }

    private IEnumerator Play(Color color, float radius, float duration)
    {
        var psGo = new GameObject("Burst");
        psGo.transform.SetParent(transform);
        psGo.transform.localPosition = Vector3.zero;

        var ps = psGo.AddComponent<ParticleSystem>();

        // ★ 修复：ParticleSystem 的 playOnAwake 默认 true，AddComponent 后立即自动播放，
        //   此时再改 duration 会抛 “Setting the duration while system is still playing”。
        //   先停掉并显式关闭自动播放，再配置参数。
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // ── Main ──
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = duration * 0.8f;
        main.startSpeed = radius / (duration * 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.loop = false;
        main.gravityModifier = 0.1f;

        // ── Emission: 更多粒子 ──
        var emit = ps.emission;
        emit.rateOverTime = 0f;
        emit.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(radius * 20)) });

        // ── Shape: thin circle ring ──
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        shape.radiusThickness = 0f;

        // ── Velocity: radial outward + random ──
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.radial = 1f;
        vel.speedModifier = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        vel.space = ParticleSystemSimulationSpace.Local;

        // ── Size over lifetime: grow then shrink ──
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(0.2f, 1f);
        sizeCurve.AddKey(0.6f, 0.7f);
        sizeCurve.AddKey(1f, 0f);
        sz.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Rotation over lifetime: spin particles ──
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

        // ── Color over lifetime: fade alpha ──
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        // ── Noise: subtle turbulence for organic feel ──
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 1f;

        // ── Renderer: URP Additive 粒子材质 ──
        var rnd = ps.GetComponent<ParticleSystemRenderer>();
        rnd.renderMode = ParticleSystemRenderMode.Billboard;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader) { name = "Shockwave" };
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 1f);     // Additive (能量/剑气效果)
            mat.SetFloat("_Cull", 0f);
            rnd.material = mat;
            Destroy(mat, duration + 1f);
        }

        ps.Play();
        yield return new WaitForSeconds(duration + 0.5f);
        Destroy(gameObject);
    }
}
