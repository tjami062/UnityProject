using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    public int playerId;
    public Team team;

    [Header("Smoothing")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 15f;

    // Target state coming from the network
    private Vector3 targetPosition;
    private float targetYaw;

    private void Awake()
    {
        targetPosition = transform.position;
        targetYaw = transform.eulerAngles.y;
    }

    /// <summary>
    /// Called by NetworkClient whenever a POS message is received.
    /// </summary>
    public void SetNetworkState(Vector3 pos, float yaw)
    {
        targetPosition = pos;
        targetYaw = yaw;
    }

    private void Update()
    {
        // Smoothly move toward the networked position
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * positionLerpSpeed
        );

        // Smoothly rotate toward the networked yaw
        Vector3 euler = transform.eulerAngles;
        euler.y = Mathf.LerpAngle(euler.y, targetYaw, Time.deltaTime * rotationLerpSpeed);
        transform.rotation = Quaternion.Euler(euler);
    }
}
