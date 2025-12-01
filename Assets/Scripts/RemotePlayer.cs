using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [Header("Identity")]
    public int playerId;
    public Team team;

    [Header("Visual Offset")]
    [Tooltip("Optional offset to apply if the model's pivot is not at the same height as the networked position.")]
    public float yOffset = 0f;

    [Header("Smoothing")]
    public float positionLerpSpeed = 15f;
    public float rotationLerpSpeed = 20f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    /// <summary>
    /// Called by NetworkClient.HandlePos when a new network transform is received.
    /// </summary>
    public void SetNetworkTransform(Vector3 pos, float yaw)
    {
        targetPosition = pos + new Vector3(0f, yOffset, 0f);
        targetRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void Update()
    {
        // Smoothly move towards latest networked state
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * positionLerpSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationLerpSpeed
        );
    }
}