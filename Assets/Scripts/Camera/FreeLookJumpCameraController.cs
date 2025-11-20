using UnityEngine;
using Cinemachine;

/// <summary>
/// Modern jump camera controller for CinemachineFreeLook
/// 实现跳跃相机垂直缓冲和平滑过渡（新版 Cinemachine 兼容）
/// </summary>
[RequireComponent(typeof(CinemachineFreeLook))]
public class FreeLookJumpCameraController : MonoBehaviour
{
    [Header("References")]
    public PlayerModel player;               // 你的 PlayerModel（必须）
    public Transform cameraFollowTarget;     // 相机跟随目标（通常是Player下的CameraPivot）

    [Header("Jump Camera Settings")]
    public float jumpUpLag = 1f;           // 上升时相机滞后感（越大越慢）
    public float fallCatchUp = 3f;           // 下落时相机追赶速度
    public float maxVerticalOffset = 2.5f;   // 最大延迟距离
    public float smoothing = 5f;             // 平滑插值速度
    public float dampingBoostOnJump = 2.0f;  // 跳跃时阻尼倍率

    private CinemachineFreeLook freeLook;
    private float defaultYDamping;           // 默认垂直阻尼
    private float currentYOffset;
    private bool wasGrounded;
    private bool isJumping;
    private Vector3 targetFollowPos;

    void Start()
    {
        freeLook = GetComponent<CinemachineFreeLook>();

        if (!player)
        {
            Debug.LogError("FreeLookJumpCameraController 缺少 PlayerModel 引用！");
            enabled = false;
            return;
        }

        if (!cameraFollowTarget)
        {
            Debug.LogError("请在 Inspector 指定 cameraFollowTarget（例如 Player 下的 CameraPivot）！");
            enabled = false;
            return;
        }

        // 获取默认 Y 阻尼（取中间 Rig 代表）
        var transposer = freeLook.GetRig(1).GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
            defaultYDamping = transposer.m_YDamping;
        else
            defaultYDamping = 0.5f;

        targetFollowPos = cameraFollowTarget.localPosition;
        wasGrounded = true;
    }

    void LateUpdate()
    {
        bool isGrounded = PlayerController.Instance.isGround;
        float verticalVelocity = player.gravityVector.y;

        // 检测跳跃
        if (wasGrounded && !isGrounded && verticalVelocity > 0)
        {
            isJumping = true;
            SetYDamping(defaultYDamping * dampingBoostOnJump);
        }

        // 跳跃过程中的相机缓动
        if (isJumping)
        {
            if (verticalVelocity > 0)
            {
                // 上升 → 相机延后
                currentYOffset = Mathf.Lerp(currentYOffset, -maxVerticalOffset, Time.deltaTime * jumpUpLag);
            }
            else
            {
                // 下落 → 相机追上
                currentYOffset = Mathf.Lerp(currentYOffset, 0, Time.deltaTime * fallCatchUp);
            }
        }

        // 落地时恢复
        if (!wasGrounded && isGrounded)
        {
            isJumping = false;
            RestoreYDamping();
            currentYOffset = Mathf.Lerp(currentYOffset, 0, Time.deltaTime * 8f);
        }

        // 平滑移动相机跟随目标
        Vector3 desiredPos = new Vector3(0, currentYOffset, 0);
        cameraFollowTarget.localPosition = Vector3.Lerp(cameraFollowTarget.localPosition, desiredPos, Time.deltaTime * smoothing);

        wasGrounded = isGrounded;
    }

    /// <summary>
    /// 统一设置三个 Rig 的 Y 阻尼
    /// </summary>
    void SetYDamping(float value)
    {
        for (int i = 0; i < 3; i++)
        {
            var transposer = freeLook.GetRig(i).GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
                transposer.m_YDamping = value;
        }
    }

    /// <summary>
    /// 恢复默认阻尼
    /// </summary>
    void RestoreYDamping()
    {
        SetYDamping(defaultYDamping);
    }
}
