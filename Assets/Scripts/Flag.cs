using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;                // Red or Blue flag
    public Transform homeOverride;   // Set this in Inspector!

    [Header("Carry Offset (if no hold point on remote)")]
    public Vector3 fallbackLocalOffset = new Vector3(0, 0, 0.5f);

    private PlayerTeam carrier;      // Player carrying the flag

    private Vector3 homePos;
    private Quaternion homeRot;

    public bool IsCarried => carrier != null;

    // ============================================================
    // INITIALIZATION
    // ============================================================
    private void Awake()
    {
        if (homeOverride != null)
        {
            homePos = homeOverride.position;
            homeRot = homeOverride.rotation;
        }
        else
        {
            Debug.LogError($"[FLAG] homeOverride NOT ASSIGNED for {team}!");
            homePos = transform.position;
            homeRot = transform.rotation;
        }
    }

    private void Start()
    {
        ApplyNetworkAtBase();  // Always start at your base
    }

    // ============================================================
    // APPLY: FLAG AT BASE  (server tells us this)
    // ============================================================
    public void ApplyNetworkAtBase()
    {
        // Detach while preserving world position
        Vector3 world = transform.position;
        Quaternion worldRot = transform.rotation;

        transform.SetParent(null, true);  // true = keep world space

        // Clear carrier
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        // Place at home override
        if (homeOverride != null)
        {
            transform.position = homeOverride.position;
            transform.rotation = homeOverride.rotation;
        }
        else
        {
            Debug.LogError($"[FLAG] No homeOverride assigned for {team}. Using fallback.");
            transform.position = homePos; // fallback (startup pos)
            transform.rotation = homeRot;
        }

        Debug.Log($"[FLAG] {team} returned to HOME at {transform.position}");
    }

    // ============================================================
    // APPLY: FLAG DROPPED (server tells us this)
    // ============================================================
    public void ApplyNetworkDropped(Vector3 pos)
    {
        // Preserve world-space coordinates before unparenting
        transform.SetParent(null, true);

        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        // Server sends correct X,Y,Z — we trust it
        transform.position = pos;
        Debug.Log($"[FLAG] {team} dropped at {pos}");
    }

    // ============================================================
    // APPLY: LOCAL PLAYER PICKED UP FLAG
    // ============================================================
    public void ApplyNetworkCarriedByLocal(PlayerTeam player)
    {
        carrier = player;
        carrier.AssignFlag(this);

        Transform attach = carrier.flagHoldPoint != null
            ? carrier.flagHoldPoint
            : carrier.transform;

        // Attach and reset local offset
        transform.SetParent(attach, false);

        transform.localPosition = carrier.flagHoldPoint != null
            ? Vector3.zero
            : fallbackLocalOffset;

        transform.localRotation = Quaternion.identity;

        Debug.Log($"[FLAG] {team} carried by LOCAL");
    }

    // ============================================================
    // APPLY: REMOTE PLAYER PICKED UP FLAG
    // ============================================================
    public void ApplyNetworkCarriedByRemote(Transform holdPoint)
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        // Parent to the remote player's flag attach point
        transform.SetParent(holdPoint, false);

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log($"[FLAG] {team} carried by REMOTE");
    }

    // ============================================================
    // LOCAL PICKUP TRIGGER
    // ============================================================
    private void OnTriggerEnter(Collider other)
    {
        PlayerTeam p = other.GetComponentInParent<PlayerTeam>();

        if (p == null || !p.isLocalPlayer)
            return;

        if (!NetworkClient.Instance || !NetworkClient.Instance.IsConnected)
            return;

        NetworkClient.Instance.SendFlagPickup(team);

        Debug.Log($"[FLAG] Local player requested pickup for {team}");
    }
}
