using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple singleton manager that shows a Game Over UI (hook to a Canvas prefab or scene UI) and
/// provides Retry and Quit functions.
///
/// Usage: place a GameOverManager in the scene and assign gameOverPanel (a Canvas child or prefab instance).
/// Call GameOverManager.Instance.ShowGameOver() to display the panel.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Tooltip("Reference to the Game Over panel GameObject (assign a Canvas child or prefab instance)")]
    public GameObject gameOverPanel;

    [Tooltip("Scene name to load when quitting to main menu")]
    public string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optionally keep across scenes
        // DontDestroyOnLoad(gameObject);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        // Pause time optionally
        Time.timeScale = 0f;
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Retry()
    {
        // Resume time in case it was paused
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
