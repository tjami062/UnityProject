using UnityEngine;

[DisallowMultipleComponent]
public class Flag : MonoBehaviour
{
    [Header("Flag Settings")]
    public Team team;   // Which team OWNS this flag (Red flag, Blue flag)

    [Tooltip("Optional: leave empty to use the flag's starting position as home.")]
    public Transform homeOverride;

    [Header("Carry Offset (if no flagHoldPoint)")]
    public Vector3 fallbackLocalOffset = new Vector3(0f, 0f, 0.5f);

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
            homePos = transform.position;
            homeRot = transform.rotation;
        }
    }

    private void Start()
    {
        ApplyNetworkAtBase();
    }

    public void ApplyNetworkAtBase()
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(null);
        transform.position = homePos;
        transform.rotation = homeRot;
    }

    public void ApplyNetworkDropped(Vector3 pos)
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(null);

        // FIX: raise the drop height so it does NOT fall through ground
        transform.position = new Vector3(pos.x, pos.y + 1.5f, pos.z);
    }

    public void ApplyNetworkCarriedByLocal(PlayerTeam player)
    {
        carrier = player;
        carrier.AssignFlag(this);

        Transform attachPoint = carrier.flagHoldPoint != null
            ? carrier.flagHoldPoint
            : carrier.transform;

        transform.SetParent(attachPoint, worldPositionStays: false);

        if (carrier.flagHoldPoint != null)
            transform.localPosition = Vector3.zero;
        else
            transform.localPosition = fallbackLocalOffset;

        transform.localRotation = Quaternion.identity;
    }

    public void ApplyNetworkCarriedByRemote(Transform remoteHoldPoint)
    {
        if (carrier != null)
        {
            carrier.ClearFlag(this);
            carrier = null;
        }

        transform.SetParent(remoteHoldPoint, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public bool AtHome()
    {
        if (IsCarried) return false;
        return Vector3.Distance(transform.position, homePos) < 0.5f;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only local player should send pickup messages
        PlayerTeam player = other.GetComponentInParent<PlayerTeam>();
        if (player == null || !player.isLocalPlayer)
            return;

        if (NetworkClient.Instance == null || !NetworkClient.Instance.IsConnected)
        {
            return; // no networking; could optionally fall back to local mode
        }

        // Ask the server to handle pickup/return
        NetworkClient.Instance.Send($"FLAG_PICKUP {team}");
    }

    public void NetworkDropFromLocal(PlayerTeam player)
    {
        if (NetworkClient.Instance == null || !NetworkClient.Instance.IsConnected)
            return;

        Vector3 p = player.transform.position;
        NetworkClient.Instance.Send($"FLAG_DROP {team} {p.x} {p.y} {p.z}");
    }
}
