using UnityEngine;

public class PlayerTeam : MonoBehaviour
{
    public Team team;
    public bool isLocalPlayer;

    [Header("Movement Components")]
    public CharacterController characterController;
    public PlayerMovementAdvanced movement;

    [Header("Flag Carrying")]
    public Transform flagHoldPoint;   // where the flag should attach
    public Flag carriedFlag;          // current flag (if any)

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (movement == null)
            movement = GetComponent<PlayerMovementAdvanced>();
    }

    /// <summary>
    /// Teleport the player to a spawn Transform (position + rotation).
    /// Handles CharacterController safely.
    /// </summary>
    public void TeleportTo(Transform spawn)
    {
        if (spawn == null)
        {
            Debug.LogError("TeleportTo called with null spawn.");
            return;
        }

        Debug.Log($"[PlayerTeam] TeleportTo: team={team}, targetPos={spawn.position}");

        bool ccWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        // FULL position and rotation
        transform.position = spawn.position;
        transform.rotation = spawn.rotation;

        // If you ever add a vertical-velocity reset in movement, call it here.
        // if (movement != null) movement.ResetVertical();

        if (characterController != null && ccWasEnabled)
            characterController.enabled = true;
    }

    // -------------------------
    // CTF helpers – shaped to match Flag/CaptureZone usage
    // -------------------------

    // CaptureZone uses:  if (!player.HasFlag) ...
    public bool HasFlag => carriedFlag != null;

    // Flag / CaptureZone use:  player.AssignFlag(flag);
    public void AssignFlag(Flag flag)
    {
        carriedFlag = flag;
    }

    // Flag uses: player.ClearFlag();  AND player.ClearFlag(flag);
    public void ClearFlag()
    {
        carriedFlag = null;
    }

    public void ClearFlag(Flag flag)
    {
        // Only clear if this is the flag we were actually carrying
        if (carriedFlag == flag)
            carriedFlag = null;
    }
}
