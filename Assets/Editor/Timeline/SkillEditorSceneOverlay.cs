using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SceneView 可视化预览 — 在场景中绘制事件范围形状
/// </summary>
public class SkillEditorSceneOverlay
{
    private bool _enabled = true;
    private GameObject _previewObj;
    private int _currentFrame;
    private List<TimelineData> _timelines;

    public bool Enabled { get => _enabled; set => _enabled = value; }
    public GameObject PreviewObject { set => _previewObj = value; }
    public int CurrentFrame { set => _currentFrame = value; }
    public List<TimelineData> Timelines { set => _timelines = value; }

    public void OnSceneGUI(SceneView sceneView)
    {
        if (!_enabled || _previewObj == null || _timelines == null) return;

        Matrix4x4 originalMatrix = Handles.matrix;
        Transform t = _previewObj.transform;

        foreach (var timeline in _timelines)
        {
            foreach (var evt in timeline.events)
            {
                if (_currentFrame < evt.StartFrame || _currentFrame >= evt.EndFrame) continue;

                if (evt is AttackEvent atk)
                {
                    DrawAttackEvent(t, atk);
                }
                else if (evt is GameplayEffectEvent gasEvt && gasEvt.effectTarget == EffectTargetType.AllInRange)
                {
                    Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.7f); // Green for GASEffect
                    DrawSearchParameters(t.position, t.forward, gasEvt.searchParameters);
                    DrawInteractiveHandles(t.position, t.forward, gasEvt.searchParameters);
                }
                else if (evt is TargetSearchEvent searchEvt)
                {
                    Handles.color = new Color(0.3f, 0.5f, 1f, 0.7f); // Blue for TargetSearch
                    DrawSearchParameters(t.position, t.forward, searchEvt.searchParameters);
                    DrawInteractiveHandles(t.position, t.forward, searchEvt.searchParameters);
                }
            }
        }

        Handles.matrix = originalMatrix;
    }

    private void DrawAttackEvent(Transform t, AttackEvent atk)
    {
        if (atk.attackData == null) return;
        var data = atk.attackData;
        if (data.radius <= 0 && data.length <= 0) return;

        Vector3 center;
        Vector3 forward;

        if (atk.useLocalOffset)
        {
            center = t.position + t.rotation * atk.localOffset;
            forward = t.rotation * atk.localForward;
        }
        else
        {
            center = t.position;
            forward = t.forward;
        }

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Handles.color = new Color(1f, 0.2f, 0.2f, 0.7f); // Red for Attack

        switch (data.shape)
        {
            case AttackShape.Sphere:
                Handles.DrawWireDisc(center, Vector3.up, data.radius);
                Handles.DrawWireDisc(center, Vector3.right, data.radius);
                Handles.DrawWireDisc(center, Vector3.forward, data.radius);
                break;

            case AttackShape.Capsule:
                Vector3 end = center + forward * data.length;
                Vector3 right = Vector3.Cross(forward, Vector3.up);
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                right.Normalize();
                Handles.DrawWireDisc(center, forward, data.radius);
                Handles.DrawWireDisc(end, forward, data.radius);
                Handles.DrawLine(center + right * data.radius, end + right * data.radius);
                Handles.DrawLine(center - right * data.radius, end - right * data.radius);
                break;

            case AttackShape.Cone:
                float halfAngle = data.angle * 0.5f;
                Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
                Vector3 rightDir = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
                Handles.DrawLine(center, center + left * data.length);
                Handles.DrawLine(center, center + rightDir * data.length);
                Handles.DrawWireArc(center, Vector3.up, left, data.angle, data.length);
                break;
        }

        // Draw HitBox if available
        if (!string.IsNullOrEmpty(atk.hitBoxName))
        {
            DrawHitBox(t, atk.hitBoxName);
        }
    }

    private void DrawHitBox(Transform root, string hitBoxName)
    {
        if (string.IsNullOrEmpty(hitBoxName)) return;
        var hitTransform = FindDeepChild(root, hitBoxName);
        if (hitTransform == null) return;

        var hitCollider = hitTransform.GetComponent<Collider>();
        if (hitCollider == null) return;

        Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Handles.matrix = hitTransform.localToWorldMatrix;

        if (hitCollider is BoxCollider box)
        {
            Handles.DrawWireCube(box.center, box.size);
        }
        else if (hitCollider is SphereCollider sphere)
        {
            Handles.DrawWireDisc(sphere.center, Vector3.up, sphere.radius);
            Handles.DrawWireDisc(sphere.center, Vector3.right, sphere.radius);
            Handles.DrawWireDisc(sphere.center, Vector3.forward, sphere.radius);
        }

        Handles.matrix = Matrix4x4.identity;
    }

    private void DrawSearchParameters(Vector3 center, Vector3 forward, SearchParameters sp)
    {
        if (sp == null) return;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        switch (sp.Shape)
        {
            case SearchShape.Circle:
                Handles.DrawWireDisc(center, Vector3.up, sp.Radius);
                // Draw filled disc
                Color fill = Handles.color;
                fill.a = 0.1f;
                Handles.DrawSolidDisc(center, Vector3.up, sp.Radius);
                Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.7f);
                break;

            case SearchShape.Sector:
                float halfAngle = sp.Angle * 0.5f;
                Vector3 leftDir = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
                Handles.DrawLine(center, center + leftDir * sp.Radius);
                Vector3 rightDir = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;
                Handles.DrawLine(center, center + rightDir * sp.Radius);
                Handles.DrawWireArc(center, Vector3.up, leftDir, sp.Angle, sp.Radius);
                break;

            case SearchShape.Line:
                Vector3 lineEnd = center + forward * sp.Length;
                Handles.DrawLine(center, lineEnd);
                Handles.DrawWireDisc(lineEnd, Vector3.up, 0.15f);
                break;

            case SearchShape.Rectangle:
                Vector3 right = Vector3.Cross(forward, Vector3.up);
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
                right.Normalize();
                float halfW = sp.Width * 0.5f;
                Vector3 p0 = center + right * halfW;
                Vector3 p1 = center - right * halfW;
                Vector3 p2 = center - right * halfW + forward * sp.Length;
                Vector3 p3 = center + right * halfW + forward * sp.Length;
                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p3);
                Handles.DrawLine(p3, p0);
                break;
        }
    }

    private void DrawInteractiveHandles(Vector3 center, Vector3 forward, SearchParameters sp)
    {
        if (sp == null) return;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        EditorGUI.BeginChangeCheck();

        switch (sp.Shape)
        {
            case SearchShape.Circle:
                float newRadius = Handles.RadiusHandle(Quaternion.identity, center, sp.Radius);
                if (EditorGUI.EndChangeCheck())
                {
                    sp.Radius = Mathf.Max(0.1f, newRadius);
                }
                break;

            case SearchShape.Sector:
                float sectorRadius = Handles.RadiusHandle(Quaternion.identity, center, sp.Radius);
                if (EditorGUI.EndChangeCheck())
                {
                    sp.Radius = Mathf.Max(0.1f, sectorRadius);
                }
                break;

            case SearchShape.Line:
                Vector3 lineEndPos = center + forward * sp.Length;
                Vector3 newEnd = Handles.FreeMoveHandle(lineEndPos, 0.2f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    sp.Length = Mathf.Max(0.1f, Vector3.Distance(center, newEnd));
                }
                break;

            case SearchShape.Rectangle:
                Vector3 rectEnd = center + forward * sp.Length;
                Vector3 newRectEnd = Handles.FreeMoveHandle(rectEnd, 0.2f, Vector3.zero, Handles.DotHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    sp.Length = Mathf.Max(0.1f, Vector3.Distance(center, newRectEnd));
                }
                break;
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        var result = parent.Find(name);
        if (result != null) return result;
        foreach (Transform child in parent)
        {
            result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
