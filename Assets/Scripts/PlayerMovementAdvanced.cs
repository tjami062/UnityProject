using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerMovementAdvanced : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 12f;          // Base walking speed (unused, but kept)
    public float walkSpeed = 12f;
    public float sprintSpeed = 18f;
    public float crouchSpeed = 6f;

    [Header("Gravity and Jumping")]
    public float gravity = -9.8f * 2;
    public float jumpHeight = 3f;
    public float jumpCooldown = 0.25f;
    public float airMultiplier = 0.4f;
    bool readyToJump = true;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    public float playerHeight = 2f;
    bool isGrounded;

    [Header("Crouching")]
    public float crouchYScale = 0.5f;   // body scale when crouched
    private float startYScale;          // original body scale Y

    [Header("Crouch Visual Objects")]
    public Transform body;              // assign the "body" child here
    public Transform cameraHolder;      // assign the Main Camera here
    private float standCamLocalY;       // original camera local Y
    public float crouchCamYOffset = -0.5f; // how far to lower camera when crouched

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Slope Handling")]
    public float maxSlopeAngle = 45f;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("References")]
    public Transform orientation;       // Direction reference (usually camera orientation)

    private CharacterController controller;
    private Rigidbody rb;               // For slope detection only
    private Vector3 velocity;           // vertical velocity only
    private Vector3 moveDirection;
    private bool isMoving;
    private Vector3 lastPosition;

    public enum MovementState
    {
        walking,
        sprinting,
        crouching,
        air
    }

    public MovementState state;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.freezeRotation = true;

        // Fallbacks in case you forget to assign in Inspector
        if (body == null)
        {
            Transform t = transform.Find("body");
            if (t != null) body = t;
        }

        if (cameraHolder == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraHolder = cam.transform;
        }

        if (body != null)
            startYScale = body.localScale.y;
        if (cameraHolder != null)
            standCamLocalY = cameraHolder.localPosition.y;

        lastPosition = transform.position;
    }

    private void Update()
    {
        // Ground check
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // 🔒 If the match is over, ignore movement input but still apply gravity
        if (NetworkClient.Instance != null && NetworkClient.Instance.MatchOver)
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            isMoving = false;
            lastPosition = transform.position;
            return;
        }

        MyInput();
        StateHandler();

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Apply vertical movement
        controller.Move(velocity * Time.deltaTime);

        // Detect movement
        if (transform.position != lastPosition && isGrounded)
            isMoving = true;
        else
            isMoving = false;

        lastPosition = transform.position;
    }

    private void MyInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        // Determine movement direction
        moveDirection = orientation.forward * z + orientation.right * x;

        float currentSpeed;

        if (state == MovementState.sprinting)
            currentSpeed = sprintSpeed;
        else if (state == MovementState.crouching)
            currentSpeed = crouchSpeed;
        else
            currentSpeed = walkSpeed;

        // Horizontal movement
        controller.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);

        // Jumping
        if (Input.GetKey(jumpKey) && readyToJump && isGrounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // Crouch start
        if (Input.GetKeyDown(crouchKey))
        {
            StartCrouch();
        }

        // Crouch end
        if (Input.GetKeyUp(crouchKey))
        {
            StopCrouch();
        }
    }

    private void StartCrouch()
    {
        // scale ONLY the body mesh, not the whole player (so gun/camera don't flatten)
        if (body != null)
        {
            Vector3 scale = body.localScale;
            scale.y = crouchYScale;
            body.localScale = scale;
        }

        // move camera (and weapon) down so it feels like crouching
        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = standCamLocalY + crouchCamYOffset;
            cameraHolder.localPosition = camPos;
        }
    }

    private void StopCrouch()
    {
        // reset body scale
        if (body != null)
        {
            Vector3 scale = body.localScale;
            scale.y = startYScale;
            body.localScale = scale;
        }

        // reset camera position
        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = standCamLocalY;
            cameraHolder.localPosition = camPos;
        }
    }

    private void StateHandler()
    {
        // Crouching
        if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
        }
        // Sprinting
        else if (isGrounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
        }
        // Walking
        else if (isGrounded)
        {
            state = MovementState.walking;
        }
        // Air
        else
        {
            state = MovementState.air;
        }
    }

    private void Jump()
    {
        exitingSlope = true;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}
