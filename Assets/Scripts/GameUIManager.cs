using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverTitleText;
    public TextMeshProUGUI gameOverSubtitleText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        // At start of match: lock and hide cursor for FPS controls
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Called by NetworkClient when server sends GAME_OVER.
    /// </summary>
    public void ShowGameOver(Team winningTeam)
    {
        if (gameOverPanel == null) return;

        string winnerText = winningTeam == Team.Red ? "RED WINS!" : "BLUE WINS!";
        if (gameOverTitleText != null)
            gameOverTitleText.text = winnerText;

        if (gameOverSubtitleText != null)
            gameOverSubtitleText.text = "Press Rematch to reset the match";

        gameOverPanel.SetActive(true);

        // 🔓 Unlock and show cursor so player can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Called when server tells everyone the match was reset.
    /// </summary>
    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // 🔒 Lock and hide cursor again for FPS control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Hook these to buttons in the Inspector:

    public void OnRematchButton()
    {
        if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
        {
            NetworkClient.Instance.Send("RESET_MATCH");
        }
    }

    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
