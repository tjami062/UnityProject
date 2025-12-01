using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    [Header("Mouse Settings")]
    public float mouseSensitivity = 500f;

    [Header("References")]
    public Transform playerBody; // drag your Player (root) object here

    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {

        if (NetworkClient.Instance != null && NetworkClient.Instance.MatchOver)
            return;

        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Vertical (pitch) rotation — camera only
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Apply pitch to camera
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal (yaw) rotation — player body only
        playerBody.Rotate(Vector3.up * mouseX);
    }
}