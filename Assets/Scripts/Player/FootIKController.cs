using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Animator))]
// 我们不再需要 AnimancerComponent 的引用了
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
    [SerializeField] private float footRaycastDistance = 1.5f;
    [Tooltip("用于射线检测的地面层")]
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Debugging")]
    [Tooltip("在Scene视图中绘制所有调试信息")]
    public bool enableDebugDrawing = true;

    private Animator animator;
    
    // 我们将使用 Animator 参数的哈希值，这是非常稳定和高效的 API
    private static readonly int LeftFootIKWeightHash = Animator.StringToHash("LIK");
    private static readonly int RightFootIKWeightHash = Animator.StringToHash("RIK");

    // --- 用于 Gizmos 绘制的数据 ---
    private struct FootIKDebugData
    {
        public bool IsGrounded;
        public float Weight;
        public Vector3 AnimPosition;
        public Vector3 IKPosition;
        public Vector3 RayStart;
        public Vector3 RayDirection;
    }
    private FootIKDebugData _leftFootDebugData;
    private FootIKDebugData _rightFootDebugData;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[FootIKController] Animator component not found!", this);
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.enabled) return;
        
        // 1. 从 Animator Controller 的参数中获取权重
        float leftFootWeight = animator.GetFloat(LeftFootIKWeightHash) * globalIKWeight;
        float rightFootWeight = animator.GetFloat(RightFootIKWeightHash) * globalIKWeight;
        
        // 2. 分别处理双脚的IK
        ProcessFootIK(AvatarIKGoal.LeftFoot, leftFootWeight, ref _leftFootDebugData);
        ProcessFootIK(AvatarIKGoal.RightFoot, rightFootWeight, ref _rightFootDebugData);
    }
    
    void ProcessFootIK(AvatarIKGoal foot, float weight, ref FootIKDebugData debugData)
    {
        debugData.Weight = weight;
        debugData.AnimPosition = animator.GetIKPosition(foot);

        if (weight < 0.05f)
        {
            animator.SetIKPositionWeight(foot, 0);
            animator.SetIKRotationWeight(foot, 0);
            debugData.IsGrounded = false;
            return;
        }
        
        Vector3 animPosition = debugData.AnimPosition;
        Vector3 rayStart = animPosition + Vector3.up * 0.5f;
        Ray ray = new Ray(rayStart, Vector3.down);
        debugData.RayStart = rayStart;
        debugData.RayDirection = ray.direction;

        bool didHitGround = Physics.Raycast(ray, out RaycastHit hit, footRaycastDistance, groundLayer);
        debugData.IsGrounded = didHitGround;

        if (didHitGround)
        {
            animator.SetIKPositionWeight(foot, weight);
            animator.SetIKRotationWeight(foot, weight);
            Vector3 targetPosition = hit.point + Vector3.up * footOffset;
            Vector3 projectedDir = Vector3.ProjectOnPlane(transform.forward, hit.normal);
            Quaternion targetRotation = Quaternion.LookRotation(projectedDir, hit.normal);
            animator.SetIKPosition(foot, targetPosition);
            animator.SetIKRotation(foot, targetRotation);
            debugData.IKPosition = targetPosition;
        }
        else
        {
            animator.SetIKPositionWeight(foot, 0);
            animator.SetIKRotationWeight(foot, 0);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!enableDebugDrawing || !Application.isPlaying || animator == null) return;
        DrawFootGizmos(AvatarIKGoal.LeftFoot, _leftFootDebugData);
        DrawFootGizmos(AvatarIKGoal.RightFoot, _rightFootDebugData);
    }

    private void DrawFootGizmos(AvatarIKGoal foot, FootIKDebugData debugData)
    {
        #if UNITY_EDITOR
        Color rayColor = debugData.IsGrounded ? Color.green : Color.red;
        if (debugData.Weight > 0.05f)
        {
            Debug.DrawRay(debugData.RayStart, debugData.RayDirection * footRaycastDistance, rayColor);
        }
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        Handles.SphereHandleCap(0, debugData.AnimPosition, Quaternion.identity, 0.06f, EventType.Repaint);
        if (debugData.IsGrounded && debugData.Weight > 0.05f)
        {
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, debugData.IKPosition, Quaternion.identity, 0.1f, EventType.Repaint);
            Handles.color = Color.yellow;
            Handles.DrawLine(debugData.AnimPosition, debugData.IKPosition);
        }
        string groundedStatus = debugData.IsGrounded ? "Grounded" : "In Air";
        string label = $"{foot}\nParam Weight: {debugData.Weight:F2}\nRaycast: {groundedStatus}";
        Handles.color = (debugData.Weight > 0.05f && debugData.IsGrounded) ? Color.green : Color.white;
        Handles.Label(debugData.AnimPosition + Vector3.up * 0.3f, label);
        #endif
    }
}