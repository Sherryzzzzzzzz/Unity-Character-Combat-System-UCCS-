// 文件名: MoveTo.cs
using UnityEngine;
using System.Collections;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

[TaskCategory("My Enemy/Movement")]
[TaskDescription("控制敌人移动到随机点。可以选择全向移动或仅左右平移。")]
public class MoveTo : Action
{
    #region Public Inspector Variables
    // 使用枚举让 Inspector 更友好
    public enum MoveMode 
    { 
        [InspectorName("全向移动 (圆形区域)")]
        Omnidirectional, 
        
        [InspectorName("左右平移 (扫射)")]
        Strafe_LeftRight 
    }

    [Header("移动模式")]
    [BehaviorDesigner.Runtime.Tasks.Tooltip("Omnidirectional: 在圆形区域内寻找目标点。\nStrafe_LeftRight: 在相对于玩家的左右方向上寻找目标点。")]
    public MoveMode moveMode = MoveMode.Omnidirectional;

    [Header("全向移动参数")]
    [BehaviorDesigner.Runtime.Tasks.Tooltip("在 Omnidirectional 模式下，生成随机点的半径")]
    public float radius = 10f;

    [Header("左右平移参数")]
    [BehaviorDesigner.Runtime.Tasks.Tooltip("在 Strafe_LeftRight 模式下，左右移动的最大距离")]
    public float strafeDistance = 5f;

    [Header("通用参数")]
    [BehaviorDesigner.Runtime.Tasks.Tooltip("距离目标点多近时，算作“到达”")]
    public float stoppingDistance = 0.5f;
    #endregion

    #region Private Members
    // --- 组件引用 ---
    private CharacterController cc;
    private EnemyAnimationData animData;
    private Transform playerTransform;

    // --- 状态管理 ---
    private IEnumerator moveEnumerator;
    #endregion

    #region Behavior Designer Task Methods
    // OnStart 在任务开始时只调用一次
    public override void OnStart()
    {
        // 1. 初始化组件
        cc = GetComponent<CharacterController>();
        animData = GetComponent<EnemyAnimationData>();
        
        // 为了健壮性，缓存玩家引用
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        // 2. 启动协程
        moveEnumerator = MoveToTargetCoroutine();
        StartCoroutine(moveEnumerator);
        
        // 3. 通知动画系统进入移动状态
        if (animData != null)
        {
            animData.CurrentState = EnemyAnimationState.Move;
        }
    }

    // OnUpdate 在任务运行时每帧调用，只用来检查协程是否结束
    public override TaskStatus OnUpdate()
    {
        return (moveEnumerator != null) ? TaskStatus.Running : TaskStatus.Success;
    }

    // OnEnd 在任务被中断或完成时调用，用于清理
    public override void OnEnd()
    {
        // 1. 停止协程
        if (moveEnumerator != null)
        {
            StopCoroutine(moveEnumerator);
            moveEnumerator = null;
        }
        
        // 2. 清理移动状态
        if (animData != null && animData.Model != null)
        {
            animData.Model.moveDir = Vector2.zero;
        }
        
        // 3. (可选但推荐) 通知动画系统进入待机状态
        // 具体的切换逻辑可能由更高层的行为树节点（如Selector）控制
        if (animData != null)
        {
             animData.CurrentState = EnemyAnimationState.Idle;
        }
    }
    #endregion

    #region Coroutine and Logic
    // --- 负责移动的核心协程 ---
    private IEnumerator MoveToTargetCoroutine()
    {
        // --- 步骤 1: 根据模式计算目标点 ---
        Vector3 target = Vector3.zero;
        bool canMove = true;

        switch (moveMode)
        {
            case MoveMode.Omnidirectional:
                target = GetRandomPointInCircleXZ(radius);
                break;

            case MoveMode.Strafe_LeftRight:
                if (playerTransform == null)
                {
                    Debug.LogError("MoveTo (Strafe): 找不到标签为 'Player' 的对象！", this.gameObject);
                    canMove = false; // 标记为无法移动
                }
                else
                {
                    // 计算垂直于“敌人-玩家”方向的左右方向
                    Vector3 forwardToPlayer = playerTransform.position - transform.position;
                    Vector3 strafeDirection = Vector3.Cross(Vector3.up, forwardToPlayer).normalized; // 使用叉乘计算垂直向量

                    // 随机选择左或右
                    strafeDirection *= (Random.value > 0.5f ? 1 : -1);
                    
                    // 随机移动距离
                    float randomDistance = Random.Range(strafeDistance * 0.5f, strafeDistance);
                    target = transform.position + strafeDirection * randomDistance;
                }
                break;
        }

        // 如果无法移动（例如找不到玩家），则直接结束协程
        if (!canMove)
        {
            moveEnumerator = null;
            yield break; // 退出协程
        }

        // --- 步骤 2: 循环移动直到到达目标点 ---
        while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.x, 0, target.z)) > stoppingDistance)
        {
            Vector3 moveDirection = (target - transform.position).normalized;
            moveDirection.y = 0;

            // 使用 CharacterController 移动
            cc.SimpleMove(moveDirection * animData.Model.speed);

            // 更新动画系统所需的数据
            if (animData.Model != null)
            {
                animData.Model.moveDir = new Vector2(moveDirection.x, moveDirection.z);
            }
            
            yield return null; // 等待下一帧
        }

        // --- 步骤 3: 任务完成 ---
        moveEnumerator = null;
    }

    // --- 工具函数 ---
    private Vector3 GetRandomPointInCircleXZ(float radius)
    {
        Vector2 pointInUnitCircle = Random.insideUnitCircle;
        return transform.position + new Vector3(pointInUnitCircle.x, 0, pointInUnitCircle.y) * radius;
    }
    #endregion
}