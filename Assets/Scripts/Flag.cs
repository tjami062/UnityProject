using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;                // Which team this flag belongs to
    public Transform homeOverride;   // The actual flag home position in Unity

    [Header("Carry Offset (if no hold point on remote)")]
    public Vector3 fallbackLocalOffset = new Vector3(0, 0, 0.5f);

    private PlayerTeam carrier;
    private Vector3 homePos;
    private Quaternion homeRot;

    public bool IsCarried => carrier != null;

    private void Awake()
    {
        // Use override if assigned
        if (homeOverride != null)
        {
            homePos = homeOverride.position;
            homeRot = homeOverride.rotation;
        }
        else
        {
            // Fallback = current position
            homePos = transform.position;
            homeRot = transform.rotation;
        }
    }

    private void Start()
    {
        ApplyNetworkAtBase();
    }

    // ============================================================
    // STATE SET FROM SERVER
    // ============================================================

    public void ApplyNetworkAtBase()
    {
        // Ensure no old parenting keeps wrong offset
        transform.SetParent(null);

        // Force home position ALWAYS
        transform.position = homePos;
        transform.rotation = homeRot;

        // Ensure no carrier reference stays
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        // FORCE Y FIX
        Vector3 p = transform.position;
        p.y = homePos.y;
        transform.position = p;
    }


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
        if (player == null || !player.isLocalPlayer) return;

        if (!NetworkClient.Instance.IsConnected) return;

        NetworkClient.Instance.SendFlagPickup(team);
    }
}
