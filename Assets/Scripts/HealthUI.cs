using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    public Health playerHealth;
    public Slider healthSlider;

    private void Start()
    {
        if (playerHealth == null)
        {
            // Try to auto-find on the Player tagged "Player"
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                playerHealth = playerObj.GetComponent<Health>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning("HealthUI: No playerHealth assigned.");
            return;
        }

        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = playerHealth.MaxHealth;
            healthSlider.value = playerHealth.CurrentHealth;
        }
    }

    private void Update()
    {
        if (playerHealth == null || healthSlider == null) return;

        healthSlider.value = playerHealth.CurrentHealth;
    }
}