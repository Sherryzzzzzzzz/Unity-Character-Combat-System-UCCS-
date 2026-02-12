using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class FreeLookLockOn : MonoBehaviour
{
    [Header("输入动作引用")]
    [Tooltip("用于手柄相机控制的 Vector2 动作 (Right Stick)")]
    public InputActionReference gamepadLookAction;

    [Header("灵敏度设置")]
    [Tooltip("手柄的旋转速度")]
    public Vector2 gamepadSensitivity = new Vector2(120f, 1.5f);
    
    private const string MouseXAxisName = "Mouse X";
    private const string MouseYAxisName = "Mouse Y";

    void Awake()
    {
        CinemachineCore.GetInputAxis = GetAxisOverride;
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
        Vector2 gamepadInput = gamepadLookAction?.action.ReadValue<Vector2>() ?? Vector2.zero;
        
        float mouseX = Input.GetAxis(MouseXAxisName);
        float mouseY = Input.GetAxis(MouseYAxisName);
        
        if (axisName == MouseXAxisName)
        {
            return (gamepadInput.x * gamepadSensitivity.x * Time.deltaTime) + mouseX;
        }
        else if (axisName == MouseYAxisName)
        {
            return -(gamepadInput.y * gamepadSensitivity.y * Time.deltaTime) + mouseY;
        }
        
        return Input.GetAxis(axisName);
    }
}