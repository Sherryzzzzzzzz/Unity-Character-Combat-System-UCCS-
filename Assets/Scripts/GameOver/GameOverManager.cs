using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("死亡 UI")]
    [Tooltip("死后立即显示的大字")]
    public Text deathText;
    [Tooltip("延迟后显示的字")]
    public Text restartText;
    [Tooltip("寄字显示多久后出现开始")]
    public float delay = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (deathText == null) deathText = CreateText(canvas.transform, "DeathText", "寄", 140, Color.red, false);
            // 开始按钮的 Text 必须开启 raycastTarget，否则 Button 永远收不到点击
            if (restartText == null) restartText = CreateText(canvas.transform, "RestartText", "开始", 60, Color.white, true);
        }

        if (deathText != null) deathText.gameObject.SetActive(false);
        if (restartText != null) restartText.gameObject.SetActive(false);
    }

    Text CreateText(Transform parent, string name, string content, int size, Color color, bool raycastTarget)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = raycastTarget;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return t;
    }

    public void ShowGameOver()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0.0001f; // 几乎暂停但保留 UI 事件处理

        // 先显示"寄"
        if (deathText != null) deathText.gameObject.SetActive(true);

        // 等 delay 秒后切到"开始"
        StartCoroutine(ShowRestart());
    }

    System.Collections.IEnumerator ShowRestart()
    {
        yield return new WaitForSecondsRealtime(delay);

        if (deathText != null) deathText.gameObject.SetActive(false);
        if (restartText != null)
        {
            restartText.gameObject.SetActive(true);
            // 兜底：确保 Text 可被 EventSystem 射线命中，否则 Button 无法点击
            restartText.raycastTarget = true;
            // 点击"开始"重试
            var btn = restartText.GetComponent<Button>() ?? restartText.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(Retry);
        }
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
