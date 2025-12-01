using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CaptureZone : MonoBehaviour
{
    [Header("Which team scores here?")]
    public Team zoneTeam;   // Red base zone, Blue base zone

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerTeam player = other.GetComponentInParent<PlayerTeam>();
        if (player == null || !player.isLocalPlayer)
            return;

        if (!player.HasFlag || player.carriedFlag == null)
            return;

        // Must be at THEIR OWN base to score
        if (player.team != zoneTeam)
            return;

        // Must be carrying the ENEMY flag
        Flag carriedFlag = player.carriedFlag;
        if (carriedFlag.team == player.team)
            return;

        if (NetworkClient.Instance == null || !NetworkClient.Instance.IsConnected)
            return;

        // Ask server to handle capture & scoring
        NetworkClient.Instance.Send($"FLAG_CAPTURE {carriedFlag.team}");
    }
}