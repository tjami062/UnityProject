using UnityEngine;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    private int currentHealth;

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

        // --- NEW: Drop flag on death ---
        var pt = GetComponent<PlayerTeam>();
        if (pt != null && pt.HasFlag && pt.carriedFlag != null)
        {
            Flag flag = pt.carriedFlag;
            Vector3 dropPos = transform.position;

            // remove reference first
            pt.ClearFlag();

            // tell server
            if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
                NetworkClient.Instance.Send(
                    $"FLAG_DROP {flag.team} {dropPos.x} {dropPos.y} {dropPos.z}"
                );

            // update locally
            flag.ApplyNetworkDropped(dropPos);
        }

        // Inform server that we died
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

        if (GameManager.Instance != null && playerTeam != null)
            GameManager.Instance.SpawnPlayer(playerTeam);
    }
}
