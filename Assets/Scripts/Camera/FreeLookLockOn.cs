using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

/// <summary>
/// Cinemachine 输入重定向器。
/// 锁敌时：屏蔽垂直轴 + 限制水平旋转速度，实现固定角度慢转。
/// </summary>
public class FreeLookLockOn : MonoBehaviour
{
    [Header("输入动作")]
    [Tooltip("手柄右摇杆 Vector2 动作")]
    public InputActionReference gamepadLookAction;

    [Header("自由视角灵敏度")]
    public Vector2 gamepadSensitivity = new Vector2(120f, 1.5f);
    public float mouseSensitivityX = 1f;
    public float mouseSensitivityY = 1f;

    [Header("锁敌时旋转限制")]
    [Tooltip("锁敌时水平旋转最大速度（度/秒）")]
    public float lockOnMaxHorizontalSpeed = 90f;
    [Tooltip("锁敌时是否完全锁定垂直视角")]
    public bool lockVerticalInLockOn = true;
    [Tooltip("锁敌时的固定垂直偏移（0=水平，正值=俯视）")]
    [Range(-30f, 60f)]
    public float lockOnFixedPitch = 10f;

    // 引用
    private TargetingSystem _targeting;

    // 用于累积锁敌时的偏航角
    private float _lockOnYaw;
    private bool _wasLockedOn;

    private const string MouseXAxisName = "Mouse X";
    private const string MouseYAxisName = "Mouse Y";

    void Awake()
    {
        CinemachineCore.GetInputAxis = GetAxisOverride;
    }

    void Start()
    {
        _targeting = GetComponent<TargetingSystem>();
        if (_targeting == null)
            _targeting = FindFirstObjectByType<TargetingSystem>();
    }

    private void OnEnable()
    {
        gamepadLookAction?.action.Enable();
    }

    private void OnDisable()
    {
        gamepadLookAction?.action.Disable();
    }

    private void OnDestroy()
    {
        if (CinemachineCore.GetInputAxis == GetAxisOverride)
        {
            CinemachineCore.GetInputAxis = null;
        }
    }

    private float GetAxisOverride(string axisName)
    {
        bool isLockedOn = _targeting != null && _targeting.HasTarget;

        Vector2 gamepadInput = gamepadLookAction?.action.ReadValue<Vector2>() ?? Vector2.zero;
        float rawMouseX = Input.GetAxis(MouseXAxisName);
        float rawMouseY = Input.GetAxis(MouseYAxisName);

        if (axisName == MouseXAxisName)
        {
            float gamepadX = gamepadInput.x * gamepadSensitivity.x * Time.deltaTime;
            float mouseX = rawMouseX * mouseSensitivityX;
            float totalX = mouseX + gamepadX;

            if (isLockedOn)
            {
                // ── 锁敌：限制水平旋转速度 ──
                float maxDelta = lockOnMaxHorizontalSpeed * Time.deltaTime;
                totalX = Mathf.Clamp(totalX, -maxDelta, maxDelta);
            }

            return totalX;
        }
        else if (axisName == MouseYAxisName)
        {
            if (isLockedOn && lockVerticalInLockOn)
            {
                // ── 锁敌：完全屏蔽垂直输入 ──
                return 0f;
            }

            return rawMouseY * mouseSensitivityY
                 - gamepadInput.y * gamepadSensitivity.y * Time.deltaTime;
        }

        return Input.GetAxis(axisName);
    }
}
