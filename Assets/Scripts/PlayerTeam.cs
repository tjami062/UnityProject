using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerTeam : MonoBehaviour
{
    [Header("Team Settings")]
    public Team team = Team.Red;   // will be overridden by NetworkClient on WELCOME

    [Header("Flag Carry")]
    public Transform flagHoldPoint;   // assign in Inspector
    [HideInInspector] public Flag carriedFlag;

    public bool HasFlag => carriedFlag != null;

    [Header("Local / Remote")]
    public bool isLocalPlayer = true; // true for the player you control

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Spawn handled after NetworkClient gets WELCOME and sets team
    }

    public void TeleportTo(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("PlayerTeam.TeleportTo called with null target.");
            return;
        }

        if (controller != null)
            controller.enabled = false;

        transform.SetPositionAndRotation(target.position, target.rotation);

        if (controller != null)
            controller.enabled = true;
    }

    public void AssignFlag(Flag flag)
    {
        carriedFlag = flag;
    }

    public void ClearFlag(Flag flag)
    {
        if (carriedFlag == flag)
            carriedFlag = null;
    }
}