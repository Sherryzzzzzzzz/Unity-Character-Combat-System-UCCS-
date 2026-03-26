using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

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
        // Always play death animation if possible and disable local behaviour
        // Disable input/control
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

                // Try to trigger an Animator "Die" trigger or play a state named "Death" as a best-effort fallback
                if (_playerModel.animator != null)
                {
                    var animator = _playerModel.animator;
                    if (animator.HasState(0, Animator.StringToHash("Death")))
                    {
                        animator.Play("Death");
                    }
                    else
                    {
                        animator.SetTrigger("Die");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PlayerDeathHandler: playing death animation failed: {e}");
            }
        }

        // If this is the player, show Game Over via GameOverManager after a delay
        if (isPlayer)
        {
            StartCoroutine(ShowGameOverAfterDelay());
        }
    }

    private IEnumerator ShowGameOverAfterDelay()
    {
        yield return new WaitForSeconds(showGameOverDelay);
        var gm = GameOverManager.Instance;
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
