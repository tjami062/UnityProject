using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;                // Which team this flag belongs to
    public Transform homeOverride;   // The actual flag home position in Unity

    [Header("Carry Offset (if no hold point on remote)")]
    public Vector3 fallbackLocalOffset = new Vector3(0, -0.5f, 1.5f);

    // The real carrier variable (not "currentCarrier")
    private PlayerTeam carrier;

    // Store home transform from editor
    private Vector3 homePos;
    private Quaternion homeRot;

    public bool IsCarried => carrier != null;

    private void Awake()
    {
        if (homeOverride != null)
        {
            homePos = homeOverride.position;
            homeRot = homeOverride.rotation;
        }
        else
        {
            // Fallback = current placement in scene
            homePos = transform.position;
            homeRot = transform.rotation;
            Debug.LogWarning($"[FLAG] {team} has NO homeOverride assigned — using scene starting position.");
        }
    }

    private void Start()
    {
        // Ensure flag always starts where it should
        ApplyNetworkAtBase();
    }

    // ============================================================
    // FLAG GOES HOME (server says AT_BASE)
    // ============================================================
    public void ApplyNetworkAtBase()
    {
        Debug.Log($"[FLAG] ApplyNetworkAtBase() for {team}");

        // Make sure we detach from any player
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(null);

        if (homeOverride != null)
        {
            transform.position = homeOverride.position;
            transform.rotation = homeOverride.rotation;

            Debug.Log($"[FLAG] {team} flag moved HOME → {homeOverride.position}");
        }
        else
        {
            // Only used for safety — you should always have a homeOverride!
            Debug.LogError($"[FLAG] No homeOverride assigned for {team}! Using fallback.");
            transform.position = homePos;
            transform.rotation = homeRot;
        }
    }

    // ============================================================
    // FLAG DROPPED (server sends DROPPED x y z)
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
        transform.localRotation = Quaternion.identity;

        Debug.Log($"[FLAG] Dropped at {pos}");
    }

    // ============================================================
    // CARRIED BY LOCAL PLAYER
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

        Debug.Log($"[FLAG] Local player now carrying {team} flag");
    }

    // ============================================================
    // CARRIED BY REMOTE PLAYER
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

        Debug.Log($"[FLAG] Remote player carrying {team} flag");
    }

    // ============================================================
    // LOCAL PICKUP — only triggers for LOCAL player
    // ============================================================
    private void OnTriggerEnter(Collider other)
    {
        PlayerTeam player = other.GetComponentInParent<PlayerTeam>();
        if (player == null || !player.isLocalPlayer) return;

        if (!NetworkClient.Instance.IsConnected) return;

        // Tell the server we want to pick it up
        NetworkClient.Instance.SendFlagPickup(team);
    }
}
