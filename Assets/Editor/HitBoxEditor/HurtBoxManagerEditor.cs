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
        
        if (manager.hurtBoxContainer == null)
        {
            EditorGUILayout.HelpBox("Please assign a 'Hurt Box Container' Transform. This will be the parent for all generated HurtBoxes.", MessageType.Error);
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

        // --- 核心逻辑修改：不再将 HurtBox 作为骨骼的子对象 ---
        
        // --- 1. 生成基于骨骼对的胶囊体 (四肢, 躯干) ---
        foreach (var pair in limbBodyPartMap)
        {
            GameBodyPart part = pair.Key;
            Transform startBone = animator.GetBoneTransform(pair.Value.Start);
            Transform endBone = animator.GetBoneTransform(pair.Value.End);

            if (startBone != null && endBone != null)
            {
                // a. 创建 HurtBox GameObject，并将其作为 hurtBoxContainer 的子对象
                GameObject hurtBoxGO = new GameObject($"HurtBox_{part}");
                // 【修改】设置父对象
                hurtBoxGO.transform.SetParent(manager.hurtBoxContainer); 
                
                // 位置和旋转将在 LateUpdate 中同步，这里不需要设置

                var collider = hurtBoxGO.AddComponent<CapsuleCollider>();
                collider.isTrigger = true;
                
                // b. 计算从 startBone 到 endBone 的世界向量
                Vector3 endPositionWorld = endBone.position;
                Vector3 startPositionWorld = startBone.position;
                Vector3 boneVector = endPositionWorld - startPositionWorld;
                
                // c. 找到这个世界向量在 startBone 局部坐标系下的表示
                Vector3 boneVectorLocal = startBone.InverseTransformDirection(boneVector);

                // d. 找到局部向量中最长的轴作为胶囊体的方向 (这部分逻辑可以保持)
                float x = Mathf.Abs(boneVectorLocal.x);
                float y = Mathf.Abs(boneVectorLocal.y);
                float z = Mathf.Abs(boneVectorLocal.z);
                int direction = 0;
                if (y > x && y > z) direction = 1;
                else if (z > x && z > y) direction = 2;
                
                collider.direction = direction;

                // e. 设置胶囊体的尺寸和中心点 (基于局部向量)
                float boneLength = boneVectorLocal.magnitude;
                collider.height = boneLength * lengthPadding;
                collider.center = boneVectorLocal / 2; // 中心点在两个骨骼的中间
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
                // 【修改】设置父对象
                hurtBoxGO.transform.SetParent(manager.hurtBoxContainer);

                var collider = hurtBoxGO.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                
                collider.radius = headRadius;
                // 中心点偏移仍然在局部坐标系中定义
                collider.center = Vector3.up * headRadius * 0.5f;

                manager.bodyPartMappings.Add(new BodyPartMapping
                    { part = part, hurtBoxObject = hurtBoxGO, boneTransform = bone });
            }
        }

        EditorUtility.SetDirty(manager);
        Debug.Log("HurtBoxes generated with decoupled structure!");
    }
}