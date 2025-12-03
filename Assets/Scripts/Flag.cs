using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;
    public Transform homeOverride;

    [Header("Fallback Carry Offset")]
    public Vector3 fallbackLocalOffset = new Vector3(0, 0, 0.5f);

    private PlayerTeam carrier;
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
            Debug.LogError($"[FLAG] homeOverride is MISSING for {team} flag!");
            homePos = transform.position;
            homeRot = transform.rotation;
        }
    }

    private void Start()
    {
        ApplyNetworkAtBase();
    }

    // ============================================================
    // SERVER → CLIENT FLAG EVENTS
    // ============================================================

    public void ApplyNetworkAtBase()
    {
        Debug.Log($"[FLAG] ApplyNetworkAtBase for {team}");

        // Clear carrier
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        // Use HOME POSITION *only from homeOverride*
        if (homeOverride != null)
        {
            transform.SetParent(null);
            transform.position = homeOverride.position;
            transform.rotation = homeOverride.rotation;

            Debug.Log($"[FLAG] {team} flag moved to HOME at {homeOverride.position}");
        }
        else
        {
            Debug.LogError($"[FLAG] No homeOverride set! Flag forced to fallback.");
            transform.position = homePos;
        }
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
        transform.rotation = Quaternion.identity;

        Debug.Log($"[FLAG] {team} dropped at {pos}");
    }

    public void ApplyNetworkCarriedByLocal(PlayerTeam player)
    {
        carrier = player;
        player.AssignFlag(this);

        Transform attach = player.flagHoldPoint != null
            ? player.flagHoldPoint
            : player.transform;

        transform.SetParent(attach, false);

        transform.localPosition = player.flagHoldPoint != null
            ? Vector3.zero
            : fallbackLocalOffset;

        transform.localRotation = Quaternion.identity;

        Debug.Log($"[FLAG] {team} carried BY LOCAL");
    }

    public void ApplyNetworkCarriedByRemote(Transform remoteHoldPoint)
    {
        carrier = null;

        transform.SetParent(remoteHoldPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log($"[FLAG] {team} carried BY REMOTE");
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
