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

        // 🔥 If carrying a flag, DROP IT (full fix)
        if (playerTeam != null && playerTeam.carriedFlag != null)
        {
            Flag carried = playerTeam.carriedFlag;

            Vector3 dropPos = playerTeam.transform.position;

            // Send correct drop message
            NetworkClient.Instance.Send(
                $"FLAG_DROP {carried.team} {dropPos.x} {dropPos.y} {dropPos.z}"
            );

            // Clear local reference
            playerTeam.ClearFlag(carried);
        }

        // inform server of death for kill feed
        if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
        {
            int myId = NetworkClient.Instance.LocalPlayerId;
            NetworkClient.Instance.Send($"PLAYER_DEAD {myId} {lastAttackerId}");
        }

        ShowDeathMessage();

        // Respawn after delay
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
