using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键配置 Just Guard / 反击标签到场景中所有 HurtBoxManager。
/// 用法：菜单 Tools > Combat > Auto-Configure Just Guard Tags
/// 将 ScriptObjects/Tag 下的 State.Guarding.JustGuard 与 State.Counter.Ready
/// 资产引用写入场景内所有 HurtBoxManager（字段为空时才写），并标记场景待保存。
/// </summary>
public static class JustGuardAutoConfig
{
    private const string JustGuardTagPath = "Assets/ScriptObjects/Tag/State.Guarding.JustGuard.asset";
    private const string CounterTagPath = "Assets/ScriptObjects/Tag/State.Counter.Ready.asset";

    [MenuItem("Tools/Combat/Auto-Configure Just Guard Tags")]
    public static void Configure()
    {
        var justGuard = AssetDatabase.LoadAssetAtPath<GameplayTagSO>(JustGuardTagPath);
        var counter = AssetDatabase.LoadAssetAtPath<GameplayTagSO>(CounterTagPath);

        if (justGuard == null)
        {
            Debug.LogError($"[JustGuardAutoConfig] 未找到标签资产: {JustGuardTagPath}");
            return;
        }
        if (counter == null)
        {
            Debug.LogError($"[JustGuardAutoConfig] 未找到标签资产: {CounterTagPath}");
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        int updated = 0;
        foreach (var root in activeScene.GetRootGameObjects())
        {
            var managers = root.GetComponentsInChildren<HurtBoxManager>(true);
            foreach (var hbm in managers)
            {
                bool dirty = false;
                if (hbm.justGuardTag == null)
                {
                    hbm.justGuardTag = justGuard;
                    dirty = true;
                }
                if (hbm.counterReadyTag == null)
                {
                    hbm.counterReadyTag = counter;
                    dirty = true;
                }
                if (dirty)
                {
                    EditorUtility.SetDirty(hbm);
                    updated++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log($"[JustGuardAutoConfig] 完成：更新了 {updated} 个 HurtBoxManager 的 Just Guard 标签配置。");
    }
}
