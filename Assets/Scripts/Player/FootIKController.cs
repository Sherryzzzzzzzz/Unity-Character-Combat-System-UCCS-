using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FootIKController : MonoBehaviour
{
    [Header("IK Global Settings")]
    [Range(0, 1)]
    [Tooltip("IK效果的总权重 (全局最大值)")]
    public float globalIKWeight = 1.0f;
    
    [Header("Foot IK Settings")]
    [Tooltip("脚部离地面的高度偏移")]
    [SerializeField] private float footOffset = 0.05f;
    [Tooltip("脚部射线检测的最大垂直距离")]
    [SerializeField] private float footRaycastDistance = 0.5f;
    [Tooltip("用于射线检测的地面层")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Stairs & Body IK Settings")]
    [Tooltip("身体重心（臀部）向上调整的最大高度")]
    [SerializeField] private float maxPelvisUpOffset = 0.3f;
    [Tooltip("身体重心调整的平滑速度")]
    [SerializeField] private float pelvisLerpSpeed = 10f;
    [Tooltip("前方台阶检测射线的起点，在角色根部前方")]
    [SerializeField] private Vector3 stepDetectorOffset = new Vector3(0, 0.1f, 0.4f);
    [Tooltip("前方台阶检测射线的长度")]
    [SerializeField] private float stepDetectorRayLength = 0.6f;
    
    private Animator animator;
    
    private static readonly int LeftFootIKWeightHash = Animator.StringToHash("LIK");
    private static readonly int RightFootIKWeightHash = Animator.StringToHash("RIK");
    
    // 内部状态
    private Vector3 _pelvisOffset = Vector3.zero; // 臀部的当前偏移量
    private Vector3 _leftFootPosition;
    private Vector3 _rightFootPosition;
    private Quaternion _leftFootRotation;
    private Quaternion _rightFootRotation;
    private float _leftFootWeight;
    private float _rightFootWeight;
    
    
    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // 使用 FixedUpdate 来进行物理检测，结果更稳定
    void FixedUpdate()
    {
        if (animator == null || !animator.enabled) return;

        // --- 1. 前向台阶检测 ---
        DetectUpcomingStep();
    }
    
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.enabled) return;

        // --- 2. 身体重心（臀部）调整 ---
        AdjustPelvisHeight();

        // --- 3. 计算双脚的IK权重 ---
        _leftFootWeight = GetFootIKWeight(LeftFootIKWeightHash);
        _rightFootWeight = GetFootIKWeight(RightFootIKWeightHash);

        // --- 4. 分别处理双脚的IK ---
        ProcessFootIK(AvatarIKGoal.LeftFoot, ref _leftFootPosition, ref _leftFootRotation, _leftFootWeight);
        ProcessFootIK(AvatarIKGoal.RightFoot, ref _rightFootPosition, ref _rightFootRotation, _rightFootWeight);

        // --- 5. 将计算结果应用到Animator ---
        ApplyIK(AvatarIKGoal.LeftFoot, _leftFootPosition, _leftFootRotation, _leftFootWeight);
        ApplyIK(AvatarIKGoal.RightFoot, _rightFootPosition, _rightFootRotation, _rightFootWeight);
    }
    
    /// <summary>
    /// (在 FixedUpdate 中调用) 检测前方的台阶并计算身体需要抬升的高度。
    /// </summary>
    private void DetectUpcomingStep()
    {
        // 从角色前下方发射射线
        Vector3 rayStart = transform.TransformPoint(stepDetectorOffset);
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, stepDetectorRayLength, groundLayer))
        {
            // 计算检测到的地面高度与角色根部高度的差值
            float heightDifference = hit.point.y - transform.position.y;
            
            // 我们只关心需要向上抬升的情况
            float targetPelvisOffset = Mathf.Max(0, heightDifference);
            
            // 限制最大抬升高度
            targetPelvisOffset = Mathf.Min(targetPelvisOffset, maxPelvisUpOffset);

            // 平滑地更新臀部偏移量
            _pelvisOffset = Vector3.Lerp(_pelvisOffset, new Vector3(0, targetPelvisOffset, 0), Time.fixedDeltaTime * pelvisLerpSpeed);
        }
        else
        {
            // 如果前方没有检测到地面（比如走下楼梯或平地），则平滑地恢复臀部位置
            _pelvisOffset = Vector3.Lerp(_pelvisOffset, Vector3.zero, Time.fixedDeltaTime * pelvisLerpSpeed);
        }
    }
    
    /// <summary>
    /// (在 OnAnimatorIK 中调用) 将计算出的臀部偏移应用到身体上。
    /// </summary>
    private void AdjustPelvisHeight()
    {
        if (_pelvisOffset.y > 0.01f)
        {
            // animator.bodyPosition 是一个强大的属性，它可以移动整个身体的根
            // 我们将动画原始的身体位置与我们的偏移量相加
            Vector3 newBodyPosition = animator.bodyPosition + _pelvisOffset;
            animator.bodyPosition = newBodyPosition;
        }
    }

    private float GetFootIKWeight(int parameterHash)
    {
        float weightFromAnimator = animator.GetFloat(parameterHash);
        return weightFromAnimator * globalIKWeight;
        
    }
    
    /// <summary>
    /// (在 OnAnimatorIK 中调用) 计算单只脚的IK目标位置和旋转。
    /// </summary>
    private void ProcessFootIK(AvatarIKGoal foot, ref Vector3 position, ref Quaternion rotation, float weight)
    {
        position = animator.GetIKPosition(foot);
        rotation = animator.GetIKRotation(foot);
        
        if (weight < 0.05f) return;

        // 【上楼梯优化】射线起点更高，方向稍微向前，更容易“捕捉”到台阶边缘
        Vector3 rayStart = position + Vector3.up * 0.5f + transform.forward * 0.1f;
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, footRaycastDistance + 0.5f, groundLayer))
        {
            position = hit.point + new Vector3(0, footOffset, 0);
            
            Vector3 projectedLookDir = Vector3.ProjectOnPlane(transform.forward, hit.normal);
            rotation = Quaternion.LookRotation(projectedLookDir, hit.normal);
        }
        else
        {
             // 如果找不到地面，则不应用IK
             // 通过将 weight 设为0 来实现
             if (foot == AvatarIKGoal.LeftFoot) _leftFootWeight = 0;
             else _rightFootWeight = 0;
        }
    }

    /// <summary>
    /// (在 OnAnimatorIK 中调用) 将最终计算出的IK数据应用到Animator。
    /// </summary>
    private void ApplyIK(AvatarIKGoal foot, Vector3 position, Quaternion rotation, float weight)
    {
        animator.SetIKPositionWeight(foot, weight);
        animator.SetIKRotationWeight(foot, weight);
        if (weight > 0.05f)
        {
            animator.SetIKPosition(foot, position);
            animator.SetIKRotation(foot, rotation);
        }
    }
}