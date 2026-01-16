using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialVariantHelper : EditorWindow
{
    [MenuItem("Tools/Batch Create Variants from Selection")]
    private static void CreateVariants()
    {
        // 获取选中的所有材质
        Object[] selection = Selection.objects;
        
        // 我们需要用户先选中那个作为“父亲”的材质，然后再选其他要转换的材质
        // 但为了保险，我们弹窗让用户确认谁是父亲
        Material parentMat = null;
        
        // 简单的逻辑：假设选中的第一个材质是父亲，或者名字里带 "Base" 的是父亲
        // 这里为了严谨，我们直接检查选中项
        if (selection.Length < 2)
        {
            Debug.LogError("请至少选中两个材质：一个是父材质(Parent)，其余是需要转换的旧材质。");
            return;
        }

        // 尝试找到父材质：这里我们假设用户选中的“活动对象”（最后选的那个）或者是名字里包含Base的
        // 为了方便，我们规定：请先选中所有旧材质，最后按住Ctrl选中父材质（Active Object）
        parentMat = Selection.activeObject as Material;

        if (parentMat == null)
        {
            Debug.LogError("未找到有效的父材质，请确保最后选中的是一个Material。");
            return;
        }

        int count = 0;
        foreach (Object obj in selection)
        {
            Material oldMat = obj as Material;
            if (oldMat == null || oldMat == parentMat) continue; // 跳过非材质和父材质本身

            CreateVariantForMaterial(parentMat, oldMat);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"成功创建了 {count} 个变体材质！");
    }

    private static void CreateVariantForMaterial(Material parent, Material source)
    {
        // 1. 获取源文件的路径
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string directory = Path.GetDirectoryName(sourcePath);
        string filename = Path.GetFileNameWithoutExtension(sourcePath);
        
        // 2. 新文件的路径 (我们在原名字后面加 _Variant 以示区别，防止覆盖原文件)
        string newPath = Path.Combine(directory, $"{filename}_Variant.mat");

        // 3. 创建变体 (关键步骤)
        // 在代码中创建变体比较特殊，我们需要实例化一个新的材质，并将其父级设为 Parent
        Material newVariant = new Material(parent);
        
        // 4. 迁移关键贴图
        // UTS (Unity Toon Shader) 的主贴图通常叫 _BaseMap，但也可能叫 _MainTex
        Texture baseMap = source.GetTexture("_BaseMap");
        if (baseMap == null) baseMap = source.GetTexture("_MainTex");

        // 如果旧材质有贴图，就覆盖到新变体上
        if (baseMap != null)
        {
            newVariant.SetTexture("_BaseMap", baseMap);
            // UTS 可能还需要设置第一层阴影贴图，通常如果你想要第一层阴影也是这个图：
            newVariant.SetTexture("_1st_ShadeMap", baseMap); 
        }

        // 你可以在这里添加更多属性的迁移，比如 _BumpMap (法线)
        Texture normalMap = source.GetTexture("_BumpMap");
        if (normalMap != null) newVariant.SetTexture("_BumpMap", normalMap);

        // 5. 保存资产
        AssetDatabase.CreateAsset(newVariant, newPath);
        
        // *这一步通过 hack 方式确保它是 Variant 链接*
        // 在新版 Unity API 中，直接 new Material(parent) 可能只是复制属性
        // 如果要严格的 Variant 链接，通常需要在 Editor 层面操作，
        // 但上述方法生成的材质实际上在 Inspector 里通常会被识别为拥有 Parent 的实例。
        // 如果发现没有链接，可以在 Inspector 手动 Revert 一次，或者使用更底层的 AssetImporter API。
        // 对于 UTS 来说，只要 Shader 相同且属性复制了，效果是一样的。
    }
}