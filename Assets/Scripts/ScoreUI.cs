using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI scoreText;

    private void Update()
    {
        if (scoreText == null) return;
        if (GameManager.Instance == null) return;

        var gm = GameManager.Instance;

        // Basic scoreboard text
        scoreText.text = $"Red {gm.redScore} : {gm.blueScore} Blue";
    }
}