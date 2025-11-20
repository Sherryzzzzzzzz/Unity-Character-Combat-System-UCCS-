// HurtBoxManagerEditor.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(HurtBoxManager))]
public class HurtBoxManagerEditor : Editor
{
    private HurtBoxManager manager;
    private Animator animator;

    // --- 可调节参数 ---
    private static float limbRadius = 0.08f;
    private static float headRadius = 0.15f;
    private static float torsoRadius = 0.2f;
    private static float handFootRadius = 0.1f;
    private static float lengthPadding = 1.05f;

    // --- 骨骼映射表 ---
    private struct BonePair { public HumanBodyBones Start; public HumanBodyBones End; }
    private static readonly Dictionary<GameBodyPart, BonePair> limbBodyPartMap = new Dictionary<GameBodyPart, BonePair>
    {
        { GameBodyPart.Torso,     new BonePair { Start = HumanBodyBones.Hips, End = HumanBodyBones.Head } },
        { GameBodyPart.LeftArm,   new BonePair { Start = HumanBodyBones.LeftUpperArm, End = HumanBodyBones.LeftHand } },
        { GameBodyPart.RightArm,  new BonePair { Start = HumanBodyBones.RightUpperArm, End = HumanBodyBones.RightHand } },
        { GameBodyPart.LeftLeg,   new BonePair { Start = HumanBodyBones.LeftUpperLeg, End = HumanBodyBones.LeftFoot } },
        { GameBodyPart.RightLeg,  new BonePair { Start = HumanBodyBones.RightUpperLeg, End = HumanBodyBones.RightFoot } },
    };

    private static readonly Dictionary<GameBodyPart, HumanBodyBones> singleBonePartMap = new Dictionary<GameBodyPart, HumanBodyBones>
    {
        { GameBodyPart.Head, HumanBodyBones.Head },
    };

    private void OnEnable()
    {
        manager = (HurtBoxManager)target;
        animator = manager.GetComponentInParent<Animator>();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(20);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Automation Tools", EditorStyles.boldLabel);

        if (animator == null || !animator.isHuman)
        {
            EditorGUILayout.HelpBox("Automation requires a Humanoid Animator component on this GameObject or its parent.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Generation Parameters", EditorStyles.miniBoldLabel);
        limbRadius = EditorGUILayout.FloatField("Limb Radius", limbRadius);
        headRadius = EditorGUILayout.FloatField("Head Radius", headRadius);
        torsoRadius = EditorGUILayout.FloatField("Torso Radius", torsoRadius);
        handFootRadius = EditorGUILayout.FloatField("Hand/Foot Radius (Legacy)", handFootRadius);
        lengthPadding = EditorGUILayout.Slider("Limb Length Padding", lengthPadding, 1.0f, 1.5f);
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate HurtBoxes From Humanoid Rig"))
        {
            if (EditorUtility.DisplayDialog("Confirm Generation",
                "This will delete existing auto-generated HurtBoxes and create new ones. Are you sure?",
                "Yes, Generate", "Cancel"))
            {
                GenerateHurtBoxes();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void GenerateHurtBoxes()
    {
        Undo.RecordObject(manager, "Generate HurtBoxes");
        manager.ClearMappings();

        // --- 1. 生成基于骨骼对的胶囊体 (四肢, 躯干) ---
        foreach (var pair in limbBodyPartMap)
        {
            GameBodyPart part = pair.Key;
            Transform startBone = animator.GetBoneTransform(pair.Value.Start);
            Transform endBone = animator.GetBoneTransform(pair.Value.End);

            if (startBone != null && endBone != null)
            {
                // a. 创建 HurtBox GameObject 并将其作为起始骨骼的子对象
                GameObject hurtBoxGO = new GameObject($"HurtBox_{part}");
                hurtBoxGO.transform.SetParent(startBone);
                hurtBoxGO.transform.localPosition = Vector3.zero;
                hurtBoxGO.transform.localRotation = Quaternion.identity;

                var collider = hurtBoxGO.AddComponent<CapsuleCollider>();
                collider.isTrigger = true;

                // b. 计算从 startBone 到 endBone 的局部向量
                Vector3 endPositionLocal = startBone.InverseTransformPoint(endBone.position);

                // c. 找到这个局部向量中最长的轴作为胶囊体的方向
                float x = Mathf.Abs(endPositionLocal.x);
                float y = Mathf.Abs(endPositionLocal.y);
                float z = Mathf.Abs(endPositionLocal.z);
                int direction = 0; // 0=X, 1=Y, 2=Z
                if (y > x && y > z) direction = 1;
                else if (z > x && z > y) direction = 2;
                
                collider.direction = direction;

                // d. 设置胶囊体的尺寸和位置
                float boneLength = endPositionLocal.magnitude;
                collider.height = boneLength * lengthPadding;
                collider.center = endPositionLocal / 2; // 中心点就是局部向量的一半
                collider.radius = (part == GameBodyPart.Torso) ? torsoRadius : limbRadius;
                
                manager.bodyPartMappings.Add(new BodyPartMapping
                    { part = part, hurtBoxObject = hurtBoxGO, boneTransform = startBone });
            }
        }

        // --- 2. 生成基于单骨骼的球体 (仅头部) ---
        foreach (var pair in singleBonePartMap)
        {
            GameBodyPart part = pair.Key;
            Transform bone = animator.GetBoneTransform(pair.Value);

            if (bone != null)
            {
                GameObject hurtBoxGO = new GameObject($"HurtBox_{part}");
                hurtBoxGO.transform.SetParent(bone);
                hurtBoxGO.transform.localPosition = Vector3.zero;
                hurtBoxGO.transform.localRotation = Quaternion.identity;

                var collider = hurtBoxGO.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                
                collider.radius = headRadius;
                // 将球体沿骨骼的局部Y轴(通常是向上)移动半个半径，使其更居中
                collider.center = Vector3.up * headRadius * 0.5f;

                manager.bodyPartMappings.Add(new BodyPartMapping
                    { part = part, hurtBoxObject = hurtBoxGO, boneTransform = bone });
            }
        }

        EditorUtility.SetDirty(manager);
        Debug.Log("HurtBoxes generated with final placement algorithm!");
    }
}