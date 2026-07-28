using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Animancer;

/// <summary>
/// Subscribes to AttributeSet.OnDeath and handles player-specific death flow:
/// - Disables player control
/// - Triggers death animation on PlayerModel (best-effort)
/// - Notifies GameOverManager to show Game Over UI after optional delay
///
/// Attach to the player GameObject (requires PlayerModel and AttributeSet). Optionally set isPlayer flag
/// if the same component could be used on NPCs but should not trigger Game Over.
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    [Tooltip("If true, this GameObject is considered the player and will trigger Game Over when dead")]
    public bool isPlayer = true;

    [Tooltip("Delay (seconds) after death animation start before showing the Game Over UI")]
    public float showGameOverDelay = 1.0f;

    private AttributeSet _attributes;
    private PlayerModel _playerModel;
    private PlayerController _playerController;

    private void Awake()
    {
        _attributes = GetComponent<AttributeSet>();
        _playerModel = GetComponent<PlayerModel>();
        _playerController = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (_attributes != null)
            _attributes.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (_attributes != null)
            _attributes.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        // 禁用输入/控制
        if (_playerController != null)
        {
            try { _playerController.enabled = false; }
            catch { }
        }

        if (_playerModel != null)
        {
            try
            {
                _playerModel.InterruptAndDisableBehavior();

                // 使用 Animancer 播放死亡动画
                float deathDuration = 2f; // 兜底时间
                var deathClip = _playerModel.AnimationSet?.death;
                if (deathClip?.Clip != null)
                {
                    _playerModel.animancer.Play(deathClip, 0.1f, FadeMode.FromStart);
                    deathDuration = deathClip.Clip.length;
                }
                else
                {
                    // 兜底：尝试用旧版 Animator 触发 Death
                    if (_playerModel.animator != null)
                    {
                        if (_playerModel.animator.HasState(0, Animator.StringToHash("Death")))
                            _playerModel.animator.Play("Death");
                        else
                            _playerModel.animator.SetTrigger("Die");
                    }
                }

                // 等待动画播放完毕再显示 GameOver
                if (isPlayer)
                    StartCoroutine(ShowGameOverAfterDelay(deathDuration));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PlayerDeathHandler: playing death animation failed: {e}");
                // 兜底：立即显示 GameOver
                if (isPlayer)
                    StartCoroutine(ShowGameOverAfterDelay(0.5f));
            }
        }
        else if (isPlayer)
        {
            // 没有 PlayerModel 时的兜底
            StartCoroutine(ShowGameOverAfterDelay(0.5f));
        }
    }

    private IEnumerator ShowGameOverAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        var gm = GameOverManager.Instance;
        if (gm == null)
            gm = FindObjectOfType<GameOverManager>();
        if (gm != null)
        {
            gm.ShowGameOver();
        }
        else
        {
            Debug.LogWarning("PlayerDeathHandler: GameOverManager.Instance is null. Ensure a GameOverManager exists in the scene.");
        }
    }
}
