using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : SingletonPatternMonoBase<PlayerController>
{
    public PlayerModel playerModel;

    public Transform cameraTransform;
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
    public bool defendHeld{ get;private set; }
    public bool dodge { get; set; }
    public bool combatArt { get;private set; }
    public bool aim { get;private set; }
    #endregion

    private TagComponent tagComponent;
    [Header("Input Actions")]
    public List<CustomInputAction> inputActions;
    public GameplayAbilitySO dodgeAbilitySO;
    private AbilitySystemComponent asc;

    [Header("Dodge Stamina")]
    [Tooltip("翻滚消耗的体力值")]
    public float staminaCostDodge = 20f;
    private int _dodgeStaminaConsumedFrame = -1;
    
    private InputActionWatcher _inputWatcher;
    public InputActionReference dodgeRunActionRef;
    public InputActionReference attackActionRef;
    public InputActionReference combatArtActionRef;
    
    private void Awake()
    {
        _inputWatcher = GetComponent<InputActionWatcher>();
        if (_inputWatcher != null)
        {
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

        }
        else
        {
            Debug.LogWarning("PlayerController: InputActionWatcher component not found on GameObject");
        }

        if (playerModel != null)
        {
            tagComponent = playerModel.tagComponent;
            asc = playerModel.GetComponent<AbilitySystemComponent>();
        }
        else
        {
            Debug.LogWarning("PlayerController: playerModel is not assigned");
        }

        input = new PlayerInputAction();

        // 战技：Q键 / 左扳机（必须在 input 初始化之后才能 FindAction）
        var combatArtAction = input.FindAction("CombatArt");
        if (combatArtAction != null)
            combatArtAction.performed += _ => combatArt = true;
        else
            Debug.LogWarning("PlayerController: CombatArt action not found in InputActionAsset");
        cameraTransform = (Camera.main != null) ? Camera.main.transform : null;
        if (cameraTransform == null)
            Debug.LogWarning("PlayerController: Main Camera not found; camera-dependent movement will be disabled");

        Cursor.lockState = CursorLockMode.Locked;
        aim = false;

        foreach (var action in inputActions)
        {
            if (action != null)
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
        defendHeld = input.Simple.Parry.IsPressed();
        aim = input.Simple.Aim.WasPressedThisFrame();

        // Dodge input: attempt perfect dodge if player pressed dodge
        // ★ 注意：状态机(PlayerGroundState/PlayerAttackState)也会处理dodge并调用AttemptDodge
        // PlayerController只做预检测，不消耗dodge标志
        if (dodge)
        {
            // 直接通过 AttributeSet 消耗体力（不再依赖 PlayerHUD UI 组件）
            if (!TryConsumeDodgeStamina())
            {
                dodge = false;
                return;
            }

            if (asc != null && dodgeAbilitySO != null)
            {
                asc.ActivateAbility(dodgeAbilitySO.abilityName);
                dodge = false;
            }
            // else: dodge will be handled by state machine (PlayerGroundState/PlayerAttackState)
        }
        #endregion
        
        #region 位置改变

        if (!playerModel.isAttacking)
        {
            // 摄像机（Awake 中已缓存）
            Transform cam = cameraTransform; if (cam == null) return;

            // 摄像机的前和右方向（投影到水平面）
            Vector3 camForward = Vector3.Scale(cam.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight = cam.right;

            // deadzone: 过滤摇杆漂移/微小输入
            Vector2 clampedMovement = movement;
            if (clampedMovement.magnitude < 0.15f)
                clampedMovement = Vector2.zero;

            // 把输入转换到世界空间
            Vector3 moveDir = (camForward * clampedMovement.y + camRight * clampedMovement.x).normalized;

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
        if(!playerModel.isAiming&&!playerModel.isAttacking)
        {
            //相机的方向向量
            Vector3 cameraForward = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
            //世界坐标下的方向向量
            worldMovement = cameraForward * movement.y + cameraTransform.right * movement.x;
            localMovement = playerModel.transform.InverseTransformVector(worldMovement);
        }
        #endregion
    }

    /// <summary>
    /// 尝试消耗翻滚体力。同帧内幂等（多次调用只消耗一次）。
    /// 供 PlayerController.Update() 和状态机（PlayerGroundState/PlayerAttackState 等）的 OnDodgeButtonPressed() 调用。
    /// </summary>
    /// <returns>true 如果体力足够（已消耗或本帧内已消耗过）</returns>
    public bool TryConsumeDodgeStamina()
    {
        // 同帧幂等：本帧已经消耗过，不再重复消耗
        if (_dodgeStaminaConsumedFrame == Time.frameCount)
            return true;

        var attrs = playerModel != null ? playerModel.GetComponent<AttributeSet>() : null;
        if (attrs == null)
        {
            // 没有 AttributeSet，允许翻滚（不做体力限制）
            Debug.LogWarning("[Dodge] 未找到 AttributeSet，跳过体力检查");
            _dodgeStaminaConsumedFrame = Time.frameCount;
            return true;
        }

        if (attrs.TryConsumeStamina(staminaCostDodge))
        {
            _dodgeStaminaConsumedFrame = Time.frameCount;
            return true;
        }

        return false;
    }

    private void LateUpdate()
    {
        // 重置一帧有效的输入
        dodge = false;
        combatArt = false;
        lightAttack = false;
        heavyAttack = false;
        defend = false;
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
