using UnityEngine;

/// <summary>
/// 封装了一次拼刀事件的结果数据。
/// </summary>
public struct ClashResult
{
    public float StunDuration;   // 硬直持续时间
    public float KnockbackForce; // 击退力大小
    public Vector3 KnockbackDirection; // 击退方向
}

/// <summary>
/// 定义了任何“可拼刀”对象必须实现的行为。
/// </summary>
public interface IClashable
{
    // 暴露一些属性供裁判读取
    GameObject GetGameObject();
    int GetClashLevel();
    
    // 接收来自裁判的指令
    void OnClash(ClashResult result);
}