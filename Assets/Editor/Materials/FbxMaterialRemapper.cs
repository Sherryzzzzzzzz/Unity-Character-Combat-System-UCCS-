using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FbxMaterialRemapper : EditorWindow
{
    [MenuItem("Assets/Tools/FBX 材质重映射 (应用变体)")]
    public static void RemapFbxMaterials()
    {
        Object[] selection = Selection.objects;
        
        if (selection.Length == 0)
        {
            Debug.LogError("❌ 请选中 .fbx 文件！");
            return;
        }

        int count = 0;

        foreach (Object obj in selection)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            // 只处理 FBX 文件
            if (!path.ToLower().EndsWith(".fbx"))
            {
                continue;
            }

            // 获取模型导入器
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            Debug.Log($"🔧 正在处理 FBX: {obj.name} ...");

            // 临时加载一下这个模型，为了读取它里面原本叫什么材质
            GameObject rawAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer[] renderers = rawAsset.GetComponentsInChildren<Renderer>(true);
            
            bool needsImport = false;

            // 遍历模型里的所有材质引用
            // 我们用一个 HashSet 防止同一个名字的材质处理多次
            HashSet<string> processedMatNames = new HashSet<string>();

            foreach (Renderer r in renderers)
            {
                foreach (Material mat in r.sharedMaterials)
                {
                    if (mat == null) continue;

                    string originalMatName = mat.name;

                    // 有时候 Unity 加载进来的名字会带有 (Instance) 或者别的后缀，清理一下
                    // 如果是嵌入材质，名字通常就是原本的名字
                    
                    if (processedMatNames.Contains(originalMatName)) continue;
                    processedMatNames.Add(originalMatName);

                    // 寻找对应的 _Variant 材质
                    // 假设规则：在同级目录下寻找 "原名_Variant.mat"
                    string dir = Path.GetDirectoryName(path);
                    string targetVariantName = originalMatName + "_Variant.mat";
                    string targetPath = Path.Combine(dir, targetVariantName);

                    // 如果同级目录找不到，尝试去掉名字里可能存在的 "Material" 等后缀再搜（可选）
                    // 这里我们严格按照你之前的命名规则：Name -> Name_Variant

                    Material variantMat = AssetDatabase.LoadAssetAtPath<Material>(targetPath);

                    if (variantMat != null)
                    {
                        // 核心逻辑：添加重映射
                        // SourceAssetIdentifier 需要原本的材质名和类型
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), originalMatName);
                        importer.AddRemap(id, variantMat);
                        
                        Debug.Log($"   🔗 映射: {originalMatName} -> {variantMat.name}");
                        needsImport = true;
                    }
                }
            }

            if (needsImport)
            {
                // 确保材质模式是 External 或者 Legacy，这样 Remap 才能生效
                // 通常建议设为 UseExternalMaterials (Unity 2020+) 或保留原样
                importer.materialLocation = ModelImporterMaterialLocation.External;
                
                // 保存并重新导入（这步会花一点时间）
                importer.SaveAndReimport();
                count++;
            }
        }

        Debug.Log($"✅ 完成！重新映射了 {count} 个 FBX 文件。");
    }
}