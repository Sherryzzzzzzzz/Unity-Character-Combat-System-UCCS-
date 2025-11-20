// 文件名: PlaySkill.cs (最终升级版 - 驱动事件)
using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

public class PlaySkill : Action 
{
    [BehaviorDesigner.Runtime.Tasks.Tooltip("要播放的技能资产")]
    public SkillTimelineAsset skillToPlay;
    
    private EnemySkillComponent _skillComponent;
    private bool _hasSkillFinished;
    
    public override void OnStart()
    {
        // 1. 获取组件
        _skillComponent = this.GetComponent<EnemySkillComponent>();
        _hasSkillFinished = false;

        // 2. 健壮性检查
        if (_skillComponent == null)
        {
            Debug.LogError("PlaySkill Action: EnemySkillComponent not found!", this.gameObject);
            return;
        }
        if (skillToPlay == null)
        {
            Debug.LogError("PlaySkill Action: 'skillToPlay' parameter is not set!", this.gameObject);
            return;
        }

        // 3. 订阅结束事件
        _skillComponent.OnSkillEnd += HandleSkillFinished;
        
        // 4. 命令播放技能
        _skillComponent.PlaySkill(skillToPlay);
    }
    
    public override TaskStatus OnUpdate()
    {
        // 健壮性检查
        if (_skillComponent == null || skillToPlay == null)
        {
            return TaskStatus.Failure;
        }
        
        // *** 核心修改：由行为树节点来驱动事件更新 ***
        _skillComponent.ManualUpdate();
        
        // 检查是否收到了结束事件
        if (_hasSkillFinished)
        {
            return TaskStatus.Success;
        }
        
        // 如果没有结束，任务继续运行
        return TaskStatus.Running;
    }
    
    public override void OnEnd()
    {
        // 1. 取消订阅，防止内存泄漏
        if (_skillComponent != null)
        {
            _skillComponent.OnSkillEnd -= HandleSkillFinished;
        }

        // 2. (可选但推荐) 确保技能被停止
        // 如果行为树因为其他原因（比如被更高优先级的行为打断）而强制退出了这个节点，
        // 我们需要确保技能也被停止。
        if (_skillComponent != null && _skillComponent.IsPlaying)
        {
            _skillComponent.StopAndCleanup();
        }
    }
    
    private void HandleSkillFinished()
    {
        // 收到来自 EnemySkillComponent 的回调
        _hasSkillFinished = true;
    }
}