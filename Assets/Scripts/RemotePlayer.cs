using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [Header("Identity")]
    public int playerId;
    public Team team;

    [Header("Visual")]
    public MeshRenderer body;
    public Material redMaterial;
    public Material blueMaterial;

    [Header("Network Movement")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 15f;

    [Header("Flag Attachment")]
    public Transform flagHoldPoint;

    private Vector3 targetPos;
    private float targetYRot;

    private Flag carriedFlag;

    private void Start()
    {
        // set player color
        if (body != null)
            body.material = (team == Team.Red ? redMaterial : blueMaterial);
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerpSpeed);

        Quaternion targetRot = Quaternion.Euler(0, targetYRot, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
    }

    // ============================================================
    // Set network state
    // ============================================================
    public void SetNetworkState(Vector3 pos, float yRotation)
    {
        targetPos = pos;
        targetYRot = yRotation;
    }

    // ============================================================
    // FLAG ATTACH
    // ============================================================
    public void AttachCarriedFlag(Flag flag)
    {
        carriedFlag = flag;

        Debug.Log($"[REMOTE] Player {playerId} now carrying {flag.team}");

        Transform attach = flagHoldPoint != null ? flagHoldPoint : transform;

        flag.transform.SetParent(attach, false);
        flag.transform.localPosition = Vector3.zero;
        flag.transform.localRotation = Quaternion.identity;
    }

    public void ClearFlag(Flag flag)
    {
        if (carriedFlag == flag)
            carriedFlag = null;
    }
}
