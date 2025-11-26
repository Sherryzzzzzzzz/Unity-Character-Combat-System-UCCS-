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

    // --- 新增：用于 LookAt 的虚拟目标 ---
    private Transform _clashLookAtTarget;

    // --- 内部引用 ---
    private AudioSource audioSource;
    // CinemachineBrain 通常在主相机上，不再需要手动引用

    private void Awake()
    {
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();

        // 创建一个用于相机 LookAt 的不可见的游戏对象
        var targetGO = new GameObject("ClashLookAtTarget");
        _clashLookAtTarget = targetGO.transform;
        _clashLookAtTarget.SetParent(this.transform); // 放在 Manager 下方便管理
    }

    /// <summary>
    /// 裁定一次拼刀的结果。
    /// </summary>
    public void ResolveClash(IClashable unitA, IClashable unitB)
    {
        if (unitA == null || unitB == null) return;

        GameObject unitA_GO = unitA.GetGameObject();
        GameObject unitB_GO = unitB.GetGameObject();

        // 播放通用效果
        Vector3 clashPoint = (unitA_GO.transform.position + unitB_GO.transform.position) / 2;
        if (clashVFX != null) Instantiate(clashVFX, clashPoint, Quaternion.identity);
        if (clashSound != null) audioSource.PlayOneShot(clashSound);

        // --- 计算结果 ---
        int levelA = unitA.GetClashLevel();
        int levelB = unitB.GetClashLevel();
        int levelDifference = levelA - levelB;

        ClashResult resultA = new ClashResult
        {
            StunDuration = Mathf.Max(0.1f, baseStunDuration * (1f - (levelDifference * levelMultiplier))),
            KnockbackForce = baseKnockbackForce,
            KnockbackDirection = (unitA_GO.transform.position - unitB_GO.transform.position).normalized
        };

        ClashResult resultB = new ClashResult
        {
            StunDuration = Mathf.Max(0.1f, baseStunDuration * (1f + (levelDifference * levelMultiplier))),
            KnockbackForce = baseKnockbackForce,
            KnockbackDirection = (unitB_GO.transform.position - unitA_GO.transform.position).normalized
        };

        // --- 命令双方执行结果 ---
        unitA.OnClash(resultA);
        unitB.OnClash(resultB);
        
        // --- 运镜 ---
        // *** 修复 2：确保这是直接的方法调用 ***
        SwitchToClashCamera(unitA_GO.transform, unitB_GO.transform);
    }
    
    private void SwitchToClashCamera(Transform targetA, Transform targetB)
    {
        if (clashCamera == null)
        {
            Debug.LogWarning("ClashManager: Clash Camera 未分配！", this);
            return;
        }

        // 1. 计算中心点和方向
        Vector3 centerPoint = (targetA.position + targetB.position) / 2;
        Vector3 direction = (targetB.position - targetA.position).normalized;
        
        // 2. 移动我们创建的虚拟目标到中心点
        _clashLookAtTarget.position = centerPoint;

        // 3. *** 修复 1：将 LookAt 属性设置为我们的虚拟目标 Transform ***
        clashCamera.LookAt = _clashLookAtTarget;
        
        // 4. 计算并设置相机的位置
        Vector3 sideDirection = Vector3.Cross(direction, Vector3.up).normalized;
        // 如果角色正好上下对齐，叉乘结果可能为0
        if (sideDirection.sqrMagnitude < 0.01f) sideDirection = targetA.right;

        clashCamera.transform.position = centerPoint - sideDirection * 5f + Vector3.up * 1.5f;

        // 5. 激活对决相机，Cinemachine 会自动处理切换
        clashCamera.gameObject.SetActive(true);
        
        // 6. 启动协程，在一段时间后切回主相机
        StartCoroutine(ReturnToMainCamera(baseStunDuration * 1.5f)); 
    }

    private IEnumerator ReturnToMainCamera(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clashCamera != null)
        {
            clashCamera.gameObject.SetActive(false);
        }
    }
}