using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [Header("Identity")]
    public int playerId;
    public Team team;

    [Header("Visual")]
    public Renderer playerRenderer;
    public Material redMaterial;
    public Material blueMaterial;

    [Header("Network Movement")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 15f;

    private Vector3 targetPos;
    private float targetRotY;

    [Header("Flag Attachment")]
    public Transform flagHoldPoint; // ← Set this in the prefab

    private Flag carriedFlag;

    private void Start()
    {
        if (playerRenderer != null)
        {
            playerRenderer.material = (team == Team.Red) ? redMaterial : blueMaterial;
        }
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerpSpeed);

        Quaternion targetRot = Quaternion.Euler(0, targetRotY, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
    }

    public void SetNetworkState(Vector3 pos, float rotY)
    {
        targetPos = pos;
        targetRotY = rotY;
    }

    // Called by NetworkClient.HandleFlagState()
    public void AttachCarriedFlag(Flag flag)
    {
        carriedFlag = flag;

        Transform attachPoint = flagHoldPoint != null ? flagHoldPoint : transform;

        flag.transform.SetParent(attachPoint, false);
        flag.transform.localPosition = Vector3.zero;
        flag.transform.localRotation = Quaternion.identity;
    }

    public void DropFlag()
    {
        if (carriedFlag != null)
        {
            carriedFlag = null;
        }
    }
}
