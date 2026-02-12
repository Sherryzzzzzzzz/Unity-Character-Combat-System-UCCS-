using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : SingletonPatternMonoBase<PlayerController>
{
    public PlayerModel playerModel;

    private Transform cameraTransform;
    public float rotationSpeed = 1f;
    
    [HideInInspector]
    public Vector3 localMovement{get;private set;}
    public Vector3 worldMovement{get;private set;}
    public float speed;
    [HideInInspector]
    public bool isGround{get;private set;}
    public LayerMask groundMask = ~0;
    public float groundDistance{get;private set;}
    
    #region 输入相关
    public PlayerInputAction input { get;private set; }
    public Vector2 movement{ get;private set; }
    public bool jump{ get;private set; }
    public bool running{ get;private set; } = false;
    public bool lightAttack{ get;private set; }
    public bool heavyAttack{ get;private set; }
    public bool defend{ get;private set; }
    public bool dodge { get;private set; }
    public bool aim { get;private set; }
    #endregion

    private TagComponent tagComponent;
    [Header("Input Actions")]
    public List<CustomInputAction> inputActions;
    
    private InputActionWatcher _inputWatcher;
    public InputActionReference dodgeRunActionRef;
    public InputActionReference attackActionRef;
    
    private void Awake()
    {
        _inputWatcher = GetComponent<InputActionWatcher>();
        var dodgeRunWatcher = _inputWatcher.GetWatchedAction(dodgeRunActionRef);
        if (dodgeRunWatcher != null)
        {
            // 为查找到的 WatchedAction 添加监听器
            dodgeRunWatcher.onShortPress.AddListener(() => dodge = true);
            dodgeRunWatcher.onLongPressStart.AddListener(() => running = true);
            dodgeRunWatcher.onLongPressEnd.AddListener(() => running = false);
        }
        var attackWatcher = _inputWatcher.GetWatchedAction(attackActionRef);
        if (attackWatcher != null)
        {
            attackWatcher.onShortPress.AddListener(() => lightAttack = true);
            attackWatcher.onLongPressStart.AddListener(() => heavyAttack = true);
        }
        tagComponent = playerModel.tagComponent;
        input = new PlayerInputAction();
        cameraTransform = Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;
        aim = false;
        
        foreach (var action in inputActions)
        {
            action.Initialize(tagComponent);
        }
    }

    private void OnEnable()
    {
        input.Enable();
    }

    private void OnDisable()
    {
        input.Disable();
    }
    
    public bool IsGrounded()
    {
        float radius = playerModel.cc.radius * 0.8f;
        Vector3 spherePos = playerModel.cc.bounds.center 
                            + Vector3.down * (playerModel.cc.height / 2 - playerModel.cc.radius + 0.5f);

        Collider[] hits = Physics.OverlapSphere(spherePos, radius, groundMask, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (hit.gameObject != gameObject)  // 过滤掉自己
                return true;
        }
        return false;
    }


    private void OnDrawGizmos()
    {
        
        Gizmos.color = Color.red;
        if (playerModel.cc == null) return;
        Vector3 origin = playerModel.cc.bounds.center;
        float rayLength = 1000f;
        Gizmos.DrawLine(origin,origin + Vector3.down*rayLength);
        
        if (playerModel != null && playerModel.cc != null)
        {
            Gizmos.color = Color.green;
            float radius = playerModel.cc.radius * 0.9f;
            Vector3 spherePos = playerModel.cc.bounds.center 
                                + Vector3.down * (playerModel.cc.height / 2 - playerModel.cc.radius + 0.5f);

            Gizmos.DrawWireSphere(spherePos, radius);
        }
    }


    public float GetDistanceToGround()
    {
        Vector3 origin = playerModel.cc.bounds.center;
        float rayLength = 1000f;
    
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, groundMask))
        {
            float bottomY = playerModel.cc.bounds.min.y;
            float distance =  bottomY - hit.point.y;
            if (distance < 0.001f) distance = 0f;
            return distance;
        }
        
        return Mathf.Infinity;
    }



    private void Update()
    {
        #region 获取玩家输入
        movement = input.Simple.Move.ReadValue<Vector2>();
        jump = input.Simple.Jump.WasCompletedThisFrame();
        defend = input.Simple.Parry.WasPressedThisFrame();
        aim = input.Simple.Aim.WasPressedThisFrame();
        #endregion
        
        #region 位置改变

        if (!playerModel.isAttacking)
        {
            // 摄像机
            Transform cam = Camera.main.transform;

            // 摄像机的前和右方向（投影到水平面）
            Vector3 camForward = Vector3.Scale(cam.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = cam.right;

            // 把输入转换到世界空间
            Vector3 moveDir = (camForward * movement.y + camRight * movement.x).normalized;
        
            playerModel.cc.Move(moveDir * speed * Time.deltaTime);
        }

        #endregion
        
        #region 人物旋转

        if (!playerModel.isAiming && !playerModel.isAttacking)
        {
            if (worldMovement.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldMovement.normalized);
                playerModel.transform.rotation = Quaternion.Slerp(playerModel.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        #endregion
        
        #region 控制相机
        if(!playerModel.isAiming||!playerModel.isAttacking)
        {
            //相机的方向向量
            Vector3 cameraForward = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
            //世界坐标下的方向向量
            worldMovement = cameraForward * movement.y + cameraTransform.right * movement.x;
            localMovement = playerModel.transform.InverseTransformVector(worldMovement);
        }
        #endregion
        
        playerModel.isDefending = defend;
    }
    
    private void LateUpdate()
    {
        // 重置一些一帧有效的输入
        dodge = false;
        lightAttack = false;
        heavyAttack = false;
    }

    private void FixedUpdate()
    {
        #region 地面检测
        isGround = IsGrounded();
        groundDistance = GetDistanceToGround();
        //Debug.LogWarning(groundDistance);
        #endregion
    }
}
