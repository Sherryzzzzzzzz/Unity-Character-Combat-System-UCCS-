using UnityEngine;

/// <summary>
/// 运行时攻击形状 Gizmos 可视化组件
/// 由 AttackEvent 在 OnStart/OnEnd 时驱动，在 Scene 视图中绘制当前攻击检测形状
/// </summary>
public class AttackShapeDebugger : MonoBehaviour
{
    private AttackShape _shapeType;
    private Vector3 _center;
    private float _radius;
    private Vector3 _forward;
    private float _length;
    private float _angle;
    private bool _isActive;

    public void SetSphere(Vector3 center, float radius)
    {
        _shapeType = AttackShape.Sphere;
        _center = center;
        _radius = radius;
        _isActive = true;
    }

    public void SetCapsule(Vector3 center, Vector3 forward, float radius, float length)
    {
        _shapeType = AttackShape.Capsule;
        _center = center;
        _forward = forward;
        _radius = radius;
        _length = length;
        _isActive = true;
    }

    public void SetCone(Vector3 center, Vector3 forward, float length, float angle)
    {
        _shapeType = AttackShape.Cone;
        _center = center;
        _forward = forward;
        _length = length;
        _angle = angle;
        _isActive = true;
    }

    public void Clear()
    {
        _isActive = false;
    }

    private void OnDrawGizmos()
    {
        if (!_isActive)
            return;

        switch (_shapeType)
        {
            case AttackShape.Sphere:
                DrawSphere();
                break;
            case AttackShape.Capsule:
                DrawCapsule();
                break;
            case AttackShape.Cone:
                DrawCone();
                break;
        }
    }

    private void DrawSphere()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(_center, _radius);
    }

    private void DrawCapsule()
    {
        Vector3 point1 = _center;
        Vector3 point2 = _center + _forward * _length;

        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(point1, _radius);
        Gizmos.DrawWireSphere(point2, _radius);

        // 连接线段（上下左右四条）
        Vector3 right = Vector3.Cross(_forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(_forward, Vector3.right).normalized;
        Vector3 up = Vector3.Cross(right, _forward).normalized;

        Gizmos.DrawLine(point1 + right * _radius, point2 + right * _radius);
        Gizmos.DrawLine(point1 - right * _radius, point2 - right * _radius);
        Gizmos.DrawLine(point1 + up * _radius, point2 + up * _radius);
        Gizmos.DrawLine(point1 - up * _radius, point2 - up * _radius);
    }

    private void DrawCone()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

        float halfAngle = _angle * 0.5f * Mathf.Deg2Rad;
        float endRadius = Mathf.Tan(halfAngle) * _length;

        // 锥形外围线段
        Vector3 right = Vector3.Cross(_forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(_forward, Vector3.right).normalized;
        Vector3 up = Vector3.Cross(right, _forward).normalized;

        Vector3 endCenter = _center + _forward * _length;

        // 四条边线
        Gizmos.DrawLine(_center, endCenter + right * endRadius);
        Gizmos.DrawLine(_center, endCenter - right * endRadius);
        Gizmos.DrawLine(_center, endCenter + up * endRadius);
        Gizmos.DrawLine(_center, endCenter - up * endRadius);

        // 末端圆弧（用线段近似）
        int segments = 24;
        float step = 360f / segments;
        Vector3 prevPoint = endCenter + right * endRadius;

        for (int i = 1; i <= segments; i++)
        {
            float rad = i * step * Mathf.Deg2Rad;
            Vector3 point = endCenter + (right * Mathf.Cos(rad) + up * Mathf.Sin(rad)) * endRadius;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }
    }
}
