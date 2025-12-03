using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;                // Which team this flag belongs to
    public Transform homeOverride;   // Actual home position in Unity

    [Header("Carry Offset (if no hold point on remote)")]
    public Vector3 fallbackLocalOffset = new Vector3(0, 0, 0.5f);

    private PlayerTeam carrier;
    private Vector3 homePos;
    private Quaternion homeRot;

    public bool IsCarried => carrier != null;

    private void Awake()
    {
        // Use override if it exists
        if (homeOverride != null)
        {
            homePos = homeOverride.position;
            homeRot = homeOverride.rotation;
        }
        else
        {
            Debug.LogError($"[FLAG] No homeOverride assigned for {team}! Using starting position.");
            homePos = transform.position;
            homeRot = transform.rotation;
        }
    }

    private void Start()
    {
        ApplyNetworkAtBase();
    }

    // ============================================================
    // FLAG RETURNED TO BASE (SERVER SAYS AT_BASE)
    // ============================================================
    public void ApplyNetworkAtBase()
    {
        Debug.Log($"[FLAG] ApplyNetworkAtBase() for {team}");

        // Clear any carrier cleanly
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(null);

        // Always use Unity-defined homeOverride
        if (homeOverride != null)
        {
            transform.position = homeOverride.position;
            transform.rotation = homeOverride.rotation;

            Debug.Log($"[FLAG] {team} moved to HOME at {homeOverride.position}");
        }
        else
        {
            // fallback
            Debug.LogError($"[FLAG] No homeOverride set for {team}, returning to 0,0,0");
            transform.position = Vector3.zero;
        }
    }

    // ============================================================
    // DROPPED
    // ============================================================
    public void ApplyNetworkDropped(Vector3 pos)
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(null);
        transform.position = pos;
    }

    // ============================================================
    // CARRIED (LOCAL PLAYER)
    // ============================================================
    public void ApplyNetworkCarriedByLocal(PlayerTeam player)
    {
        carrier = player;
        carrier.AssignFlag(this);

        Transform attach = player.flagHoldPoint != null
            ? player.flagHoldPoint
            : player.transform;

        transform.SetParent(attach, false);

        transform.localPosition = player.flagHoldPoint != null
            ? Vector3.zero
            : fallbackLocalOffset;

        transform.localRotation = Quaternion.identity;
    }

    // ============================================================
    // CARRIED (REMOTE PLAYER)
    // ============================================================
    public void ApplyNetworkCarriedByRemote(Transform remoteHoldPoint)
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(remoteHoldPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // ============================================================
    // LOCAL PICKUP
    // ============================================================
    private void OnTriggerEnter(Collider other)
    {
        PlayerTeam player = other.GetComponentInParent<PlayerTeam>();
        if (player == null || !player.isLocalPlayer)
            return;

        if (!NetworkClient.Instance.IsConnected)
            return;

        NetworkClient.Instance.SendFlagPickup(team);
    }
}
