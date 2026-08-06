// 文件名: ClashManager.cs (已修复错误)
using UnityEngine;
using Cinemachine; // 确保导入了 Cinemachine
using System.Collections;

public class ClashManager : MonoBehaviour
{
    public static ClashManager Instance;

    [Header("效果")]
    public GameObject clashVFX;
    public AudioClip clashSound;
    [Tooltip("预先在场景中创建好一个虚拟相机，并拖到这里")]
    public CinemachineVirtualCamera clashCamera;

    [Header("拼刀参数")]
    public float baseStunDuration = 0.8f;
    public float baseKnockbackForce = 5f;
    public float levelMultiplier = 0.2f;

    [Header("鬼泣式拼刀强化")]
    [Tooltip("拼刀慢动作时间缩放 (0.05 = 5% 速度，极慢定格)")]
    public float clashTimeScale = 0.05f;
    [Tooltip("拼刀火花喷射数量（围绕碰撞点随机散布，0 = 不喷）")]
    public int clashSparkCount = 5;
    [Tooltip("额外金属音效（可选，与 clashSound 同时播放形成厚实金属感）")]
    public AudioClip clashSoundExtra;
    [Tooltip("★ 拼刀镜头最短间隔（秒）：连续拼刀时不重复切镜头，防止镜头反复切换/畸变。\n特效与慢动作不受影响，仅跳过特写镜头")]
    public float clashCameraCooldown = 1.2f;

    // --- 新增：用于 LookAt 的虚拟目标 ---
    private Transform _clashLookAtTarget;

    // --- 内部引用 ---
    private AudioSource audioSource;
    public float freezeDuration = 0.15f;

    // ★ 拼刀镜头冷却 + blend 瞬切（修复慢动作下 blend 被 timeScale 拖长导致摄像头畸变）
    private float _lastClashCameraTime = -999f;
    private CinemachineBrain _brain;
    private float _originalBlendTime = -1f; 

    private void Awake()
    {
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();

        // 创建一个用于相机 LookAt 的不可见的游戏对象
        var targetGO = new GameObject("ClashLookAtTarget");
        _clashLookAtTarget = targetGO.transform;
        _clashLookAtTarget.SetParent(this.transform); // 放在 Manager 下方便管理
    }

    public void ResolveClash(IClashable unitA, IClashable unitB)
    {
        if (unitA == null || unitB == null) return;
#if UNITY_EDITOR
        Debug.Log("ClashManager: Clash started between " + unitA.GetGameObject().name + " and " + unitB.GetGameObject().name);
#endif
        // 立即启动导演协程
        StartCoroutine(DirectClashSequence(unitA, unitB));
    }

    private IEnumerator DirectClashSequence(IClashable unitA, IClashable unitB)
    {
        // --- 准备阶段 ---
        GameObject unitA_GO = unitA.GetGameObject();
        GameObject unitB_GO = unitB.GetGameObject();

        // 播放通用效果（鬼泣式：蓝白火花 + 冲击波 + 多角度火花喷射）
        Vector3 clashPoint = (unitA_GO.transform.position + unitB_GO.transform.position) / 2;
        clashPoint.y += 0.3f;

        // 中心火花（走 GlobalVFXPool 统一管线：预制体 + 程序化冲击波）
        var pool = FindFirstObjectByType<GlobalVFXPool>();
        if (pool != null)
        {
            pool.SpawnClashVFX(clashPoint);
        }
        else if (clashVFX != null)
        {
            var vfx = Instantiate(clashVFX, clashPoint, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // 多角度火花喷射：围绕碰撞点随机散布，营造金属四溅的冲击感
        if (pool != null && clashSparkCount > 0)
        {
            for (int i = 0; i < clashSparkCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 0.5f;
                offset.y = Mathf.Abs(offset.y) * 0.5f + 0.1f;
                Quaternion rot = Quaternion.LookRotation((unitA_GO.transform.position - unitB_GO.transform.position + offset).normalized);
                pool.SpawnBlockSparks(clashPoint + offset, rot);
            }
        }

        if (clashSound != null) audioSource.PlayOneShot(clashSound);
        if (clashSoundExtra != null) audioSource.PlayOneShot(clashSoundExtra);

        // --- 导演喊"卡！" ---
        // 1. 清除双方武器剑气特效
        SlashTrailEffect.DeactivateAllOn(unitA.GetGameObject());
        SlashTrailEffect.DeactivateAllOn(unitB.GetGameObject());

        // 2. 命令双方立即冻结动画
        unitA.FreezeAnimation();
        unitB.FreezeAnimation();

        // 3. 切换到对决镜头（带冷却 + blend 瞬切，避免频繁切换/慢动作拖长过渡导致畸变）
        SwitchToClashCamera(unitA_GO.transform, unitB_GO.transform);

        // 4. 通过 CombatCameraManager 触发拼刀震屏 + FOV Kick
        var cm = CombatCameraManager.Instance;
        Vector3 attackDir = (unitB_GO.transform.position - unitA_GO.transform.position).normalized;
        cm?.TriggerShake(attackDir, 0.8f, 4);
        cm?.TriggerFOVKickRaw(4f);

        // ★ 鬼泣式：全局慢动作定格（时间冻结后回弹）。
        //   注意 freeze 阶段使用 WaitForSecondsRealtime，避免被自身 timeScale 拖长。
        TimeScaleDirector.Instance.DoSlowMotion(clashTimeScale, freezeDuration + 0.15f, restoreImmediately: false);

        // --- "卡肉"阶段 ---
        yield return new WaitForSecondsRealtime(freezeDuration);

        // --- 导演喊"开始！" ---
        int levelA = unitA.GetClashLevel();
        int levelB = unitB.GetClashLevel();
        int levelDifference = levelA - levelB;

        ClashResult resultA = new ClashResult
        {
            StunDuration = Mathf.Max(0.1f, baseStunDuration * (1f - (levelDifference * levelMultiplier))),
            KnockbackForce = baseKnockbackForce,
            KnockbackDirection = (unitA_GO.transform.position - clashPoint).normalized
        };

        ClashResult resultB = new ClashResult
        {
            StunDuration = Mathf.Max(0.1f, baseStunDuration * (1f + (levelDifference * levelMultiplier))),
            KnockbackForce = baseKnockbackForce,
            KnockbackDirection = (unitB_GO.transform.position - clashPoint).normalized
        };
        
        // 5. 命令双方恢复动画并执行后续效果
        unitA.ResumeAndExecuteClash(resultA);
        unitB.ResumeAndExecuteClash(resultB);
        
        // 6. 启动相机恢复计时
        float totalDuration = freezeDuration + Mathf.Max(resultA.StunDuration, resultB.StunDuration);
        StartCoroutine(ReturnToMainCamera(totalDuration));
    }
    
    private void SwitchToClashCamera(Transform targetA, Transform targetB)
    {
        if (clashCamera == null)
        {
            Debug.LogWarning("ClashManager: Clash Camera 未分配！", this);
            return;
        }

        // ★ 镜头冷却：间隔内不重复切（特效/慢动作仍触发）
        if (Time.time - _lastClashCameraTime < clashCameraCooldown)
            return;
        _lastClashCameraTime = Time.time;

        // 1. 计算中心点和方向
        Vector3 centerPoint = (targetA.position + targetB.position) / 2;
        Vector3 direction = (targetB.position - targetA.position).normalized;
        
        // 2. 移动我们创建的虚拟目标到中心点
        _clashLookAtTarget.position = centerPoint;

        // 3. 将 LookAt 属性设置为我们的虚拟目标 Transform
        clashCamera.LookAt = _clashLookAtTarget;
        
        // 4. 计算并设置相机的位置
        Vector3 sideDirection = Vector3.Cross(direction, Vector3.up).normalized;
        // 如果角色正好上下对齐，叉乘结果可能为0
        if (sideDirection.sqrMagnitude < 0.01f) sideDirection = targetA.right;

        clashCamera.transform.position = centerPoint - sideDirection * 5f + Vector3.up * 1.5f;

        // ★ 瞬切：临时把 Cinemachine Brain 的默认 blend 设为 0。
        //   根因：Brain 的 m_IgnoreTimeScale=0，拼刀慢动作(timeScale≈0.05)会把 0.3s blend
        //   拖长 20 倍(≈6s)，镜头卡在过渡中间 → 摄像头畸变/滑动。
        if (_brain == null)
            _brain = FindFirstObjectByType<CinemachineBrain>();
        if (_brain != null && _originalBlendTime < 0f)
        {
            _originalBlendTime = _brain.m_DefaultBlend.m_Time;
            _brain.m_DefaultBlend.m_Time = 0f; // 瞬切，无过渡
        }

        // 5. 激活对决相机，Cinemachine 会自动处理切换
        clashCamera.gameObject.SetActive(true);

        // （相机恢复计时由 DirectClashSequence 统一控制，避免重复 StartCoroutine 提前切回）
    }

    private IEnumerator ReturnToMainCamera(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clashCamera != null)
        {
            clashCamera.gameObject.SetActive(false);
        }

        // ★ 还原 Brain 默认 blend 时间（切回主相机用正常过渡）
        if (_brain != null && _originalBlendTime >= 0f)
        {
            _brain.m_DefaultBlend.m_Time = _originalBlendTime;
            _originalBlendTime = -1f;
        }

        // ★ P15: 恢复主相机后重新评估锁敌状态（若拼刀前处于锁敌，恢复锁敌机位与阻尼）
        if (LockOnCameraSwitcher.Instance != null)
            LockOnCameraSwitcher.Instance.RefreshLockOnState();
    }
}