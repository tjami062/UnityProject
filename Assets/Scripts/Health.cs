using UnityEngine;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

    // UI reads these
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    [Header("Death Message UI (optional)")]
    public TextMeshProUGUI deathMessageText;
    public float deathMessageDuration = 2f;

    private PlayerTeam playerTeam;
    private bool isDead = false;
    private int lastAttackerId = -1;
    private Coroutine deathMessageRoutine;

    private void Awake()
    {
        currentHealth = maxHealth;
        playerTeam = GetComponent<PlayerTeam>();
    }

    private void Start()
    {
        if (deathMessageText != null)
            deathMessageText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when server tells us we were hit.
    /// </summary>
    public void ApplyNetworkDamage(int damage, int attackerId)
    {
        if (isDead) return;
        if (damage <= 0) return;

        lastAttackerId = attackerId;
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDeath();
        }
    }

    private void OnDeath()
    {
        if (isDead) return;
        isDead = true;

        // 🔥 IMPORTANT: Drop flag *before* notifying server of death
        if (playerTeam != null && playerTeam.carriedFlag != null)
        {
            Debug.Log("[HEALTH] Dropping flag before sending death message...");
            playerTeam.carriedFlag.NetworkDropFromLocal(playerTeam);
        }

        // Inform server that we died (for kill feed etc.)
        if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
        {
            int myId = NetworkClient.Instance.LocalPlayerId;
            NetworkClient.Instance.Send($"PLAYER_DEAD {myId} {lastAttackerId}");
        }

        ShowDeathMessage();
        StartCoroutine(RespawnAfterDelay(3f));
    }

    private void ShowDeathMessage()
    {
        if (deathMessageText == null) return;

        if (deathMessageRoutine != null)
            StopCoroutine(deathMessageRoutine);

        deathMessageRoutine = StartCoroutine(DeathMessageRoutine());
    }

    private IEnumerator DeathMessageRoutine()
    {
        deathMessageText.gameObject.SetActive(true);
        deathMessageText.text = "You Died";
        yield return new WaitForSeconds(deathMessageDuration);
        deathMessageText.gameObject.SetActive(false);
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        currentHealth = maxHealth;
        isDead = false;

        // Respawn player at team spawn
        if (GameManager.Instance != null && playerTeam != null)
        {
            GameManager.Instance.SpawnPlayer(playerTeam);
        }
    }
}
