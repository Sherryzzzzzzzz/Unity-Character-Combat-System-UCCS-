using UnityEngine;
using System.Collections;

public class RFX4_CameraShake : MonoBehaviour
{
    public AnimationCurve ShakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float Duration = 2;
    public float Speed = 22;
    public float Magnitude = 1;
    public float DistanceForce = 100;
    public float RotationDamper = 2;
    public bool IsEnabled = true;

    bool isPlaying;
    [HideInInspector]
    public bool canUpdate;

    void PlayShake()
    {
        StopAllCoroutines();
        StartCoroutine(Shake());
    }

    void Update()
    {
        if (isPlaying && IsEnabled)
        {
            isPlaying = false;
            PlayShake();
        }
    }

    void OnEnable()
    {
        isPlaying = true;
        var shakes = FindObjectsOfType(typeof(RFX4_CameraShake)) as RFX4_CameraShake[];
        if (shakes != null)
            foreach (var shake in shakes)
            {
                shake.canUpdate = false;
            }
        canUpdate = true;
    }

    IEnumerator Shake()
    {
        var elapsed = 0.0f;
        var cam = Camera.main;
        if (cam == null) yield break;

        var camTransform = cam.transform;
        var originalCamRotation = camTransform.rotation.eulerAngles;
        var direction = (transform.position - camTransform.position).normalized;
        var time = 0f;
        var randomStart = Random.Range(-1000.0f, 1000.0f);
        var distanceDamper = 1 - Mathf.Clamp01((camTransform.position - transform.position).magnitude / DistanceForce);
        Vector3 oldRotation = Vector3.zero;

        // 优先通过 CombatCameraManager 发送震屏信号（与 Cinemachine 兼容）
        var cm = CombatCameraManager.Instance;
        if (cm != null)
        {
            // 计算初始 amplitude 并委托给 CombatCameraManager
            float initialAmp = Magnitude * distanceDamper;
            cm.TriggerShake(direction, initialAmp, 1);
        }

        while (elapsed < Duration && canUpdate)
        {
            elapsed += Time.deltaTime;
            var percentComplete = elapsed / Duration;
            var damper = ShakeCurve.Evaluate(percentComplete) * distanceDamper;
            time += Time.deltaTime * damper;

            // 不再直接操作 Camera.transform.parent！
            // 改用持续发送衰减的 Impulse（如果 CombatCameraManager 不可用则 fallback）
            if (cm == null)
            {
                // fallback: 操作 Camera 自身（而非 parent，避免与 Cinemachine 冲突）
                var alpha = randomStart + Speed * percentComplete / 10;
                var x = Mathf.PerlinNoise(alpha, 0.0f) * 2.0f - 1.0f;
                var y = Mathf.PerlinNoise(1000 + alpha, alpha + 1000) * 2.0f - 1.0f;
                var z = Mathf.PerlinNoise(0.0f, alpha) * 2.0f - 1.0f;

                if (Quaternion.Euler(originalCamRotation + oldRotation) != camTransform.rotation)
                    originalCamRotation = camTransform.rotation.eulerAngles;
                oldRotation = Mathf.Sin(time * Speed) * damper * Magnitude * new Vector3(0.5f + y, 0.3f + x, 0.3f + z) * RotationDamper;
                camTransform.rotation = Quaternion.Euler(originalCamRotation + oldRotation);
            }
            else
            {
                // CombatCameraManager 处理中，持续发送衰减震屏
                float currentAmp = Magnitude * damper * distanceDamper;
                if (currentAmp > 0.02f)
                    cm.TriggerShake(direction, currentAmp, 1);
            }

            yield return null;
        }
    }
}
