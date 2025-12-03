using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    public int playerId;
    public Team team;

    [Header("Smoothing")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 15f;

    [Header("Team Materials")]
    public Material redMaterial;
    public Material blueMaterial;
    public Renderer playerRenderer;   // drag the body mesh here

    // network state
    private Vector3 targetPosition;
    private float targetYaw;

    private void Awake()
    {
        targetPosition = transform.position;
        targetYaw = transform.eulerAngles.y;
    }

    private void Start()
    {
        ApplyTeamColor();
    }

    public void ApplyTeamColor()
    {
        if (playerRenderer == null) return;

        playerRenderer.material = (team == Team.Red)
            ? redMaterial
            : blueMaterial;
    }

    // Called by network to update remote player's state
    public void SetNetworkState(Vector3 pos, float yaw)
    {
        targetPosition = pos;
        targetYaw = yaw;
    }

    private void Update()
    {
        // position lerp
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * positionLerpSpeed
        );

        // rotation lerp
        Vector3 euler = transform.eulerAngles;
        euler.y = Mathf.LerpAngle(euler.y, targetYaw, Time.deltaTime * rotationLerpSpeed);
        transform.rotation = Quaternion.Euler(euler);
    }
}
