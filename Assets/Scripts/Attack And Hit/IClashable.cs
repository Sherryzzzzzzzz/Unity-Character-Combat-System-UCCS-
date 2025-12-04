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
// 文件名: IClashable.cs
public interface IClashable
{
    GameObject GetGameObject();
    int GetClashLevel();
    
    void FreezeAnimation();
    
    void ResumeAndExecuteClash(ClashResult result);
}