using UnityEngine;
using UCCS;

/// <summary>播放技能 — 通过 ISkillPlayer 接口播放，不依赖具体组件</summary>
[System.Serializable]
public class BTA_PlaySkill : BTAction
{
    [Tooltip("要播放的技能资产")]
    public SkillTimelineAsset skillAsset;

    private ISkillPlayer _skillPlayer;
    private bool _skillFinished;

    public override void OnEnter(IBTRunner runner)
    {
        base.OnEnter(runner);

        _skillPlayer = runner.GetComponent<ISkillPlayer>();
        _skillFinished = false;

        if (_skillPlayer == null || skillAsset == null)
        {
            _state = BTNodeState.Failure;
            return;
        }

        _skillPlayer.OnSkillEnd += OnSkillFinished;
        _skillPlayer.PlaySkill(skillAsset);
    }

    public override BTNodeState OnTick()
    {
        if (_skillPlayer == null) return _state = BTNodeState.Failure;
        _skillPlayer.ManualUpdate();
        return _skillFinished ? _state = BTNodeState.Success : (_state = BTNodeState.Running);
    }

    public override void OnExit()
    {
        if (_skillPlayer != null)
        {
            _skillPlayer.OnSkillEnd -= OnSkillFinished;
            if (_skillPlayer.IsPlaying)
                _skillPlayer.StopAndCleanup();
        }
        base.OnExit();
    }

    private void OnSkillFinished() => _skillFinished = true;
}
