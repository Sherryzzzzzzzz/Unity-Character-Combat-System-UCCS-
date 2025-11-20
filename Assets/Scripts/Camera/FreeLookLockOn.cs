using UnityEngine;
using Cinemachine;

public class FreeLookLockOn : MonoBehaviour
{
    [Header("References")]
    public CinemachineFreeLook freeLook;
    public CinemachineTargetGroup targetGroup;
    public PlayerModel playerModel;
    public Transform player;

    [Header("Lock Settings")]
    public float lockBlendSpeed = 5f;
    public float enemyWeight = 0.6f;
    public float sideOffset = 2f;
    public float heightOffset = 1.5f;
    public float switchCooldown = 0.5f;

    private Transform currentEnemy;
    private float lastSwitchTime;
    private float currentEnemyWeight = 0f;
    private float currentScreenX = 0.5f;

    void Start()
    {
        if (!freeLook) freeLook = GetComponent<CinemachineFreeLook>();
        if (!targetGroup) Debug.LogError("需要在Inspector指定 CinemachineTargetGroup！");
        freeLook.Follow = targetGroup.transform;
        freeLook.LookAt = targetGroup.transform;

        // 初始化TargetGroup为两个槽位（即使暂时没敌人）
        targetGroup.m_Targets = new CinemachineTargetGroup.Target[]
        {
            new CinemachineTargetGroup.Target { target = player, weight = 1f, radius = 0.5f },
            new CinemachineTargetGroup.Target { target = null, weight = 0f, radius = 0.5f }
        };
    }

    void LateUpdate()
    {
        Transform nearest = playerModel.nearestEnemy;

        if (nearest != currentEnemy && Time.time - lastSwitchTime > switchCooldown)
        {
            currentEnemy = nearest;
            lastSwitchTime = Time.time;
            if (currentEnemy != null)
                targetGroup.m_Targets[1].target = currentEnemy;
        }

        // 平滑调整敌人权重
        float targetWeight = currentEnemy != null ? enemyWeight : 0f;
        currentEnemyWeight = Mathf.Lerp(currentEnemyWeight, targetWeight, Time.deltaTime * lockBlendSpeed);
        targetGroup.m_Targets[1].weight = currentEnemyWeight;

        // 计算目标ScreenX
        float targetScreenX = currentEnemy != null ? 0.35f : 0.5f;
        currentScreenX = Mathf.Lerp(currentScreenX, targetScreenX, Time.deltaTime * lockBlendSpeed);

        // 更新所有Rig的m_ScreenX
        for (int i = 0; i < 3; i++)
        {
            var composer = freeLook.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
                composer.m_ScreenX = currentScreenX;
        }

        // 平滑调整 targetGroup 位置（主要调高度）
        Vector3 targetPos = player.position + Vector3.up * heightOffset;
        targetGroup.transform.position = Vector3.Lerp(targetGroup.transform.position, targetPos, Time.deltaTime * lockBlendSpeed);

        // 如果敌人完全淡出（权重几乎为0），清空引用（防止残留）
        if (currentEnemyWeight < 0.01f && currentEnemy == null)
            targetGroup.m_Targets[1].target = null;
    }
}
