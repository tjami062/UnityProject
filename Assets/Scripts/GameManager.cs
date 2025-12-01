using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Spawn Points")]
    public Transform redSpawnPoint;
    public Transform blueSpawnPoint;

    [Header("Flags")]
    public Flag redFlag;
    public Flag blueFlag;

    [Header("Score")]
    public int redScore = 0;
    public int blueScore = 0;
    public int scoreToWin = 3;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Debug where the spawns actually are in WORLD space
        Debug.Log($"[GM] RedSpawnPoint = {(redSpawnPoint ? redSpawnPoint.position.ToString() : "null")}");
        Debug.Log($"[GM] BlueSpawnPoint = {(blueSpawnPoint ? blueSpawnPoint.position.ToString() : "null")}");
    }

    /// <summary>
    /// Teleports the given player to their team’s spawn point.
    /// </summary>
    public void SpawnPlayer(PlayerTeam player)
    {
        if (player == null)
        {
            Debug.LogError("GameManager.SpawnPlayer called with null player.");
            return;
        }

        Debug.Log($"[GM] SpawnPlayer: player={player.name}, team={player.team}");

        Transform spawn = null;

        switch (player.team)
        {
            case Team.Red:
                spawn = redSpawnPoint;
                break;
            case Team.Blue:
                spawn = blueSpawnPoint;
                break;
        }

        if (spawn == null)
        {
            Debug.LogError($"[GM] Spawn point not assigned for team {player.team}.");
            return;
        }

        Debug.Log($"[GM] Spawning {player.team} at {spawn.position}");
        player.TeleportTo(spawn);
    }

    /// <summary>
    /// Returns the flag object that belongs to the given team.
    /// </summary>
    public Flag GetFlagForTeam(Team team)
    {
        return team == Team.Red ? redFlag : blueFlag;
    }

    /// <summary>
    /// Called from NetworkClient when the server sends SCORE r b.
    /// </summary>
    public void SetScoreFromServer(int red, int blue)
    {
        redScore = red;
        blueScore = blue;

        Debug.Log($"[GM] Score update from server: Red={redScore}  Blue={blueScore}");

        // If you later add a UI manager with UpdateScore, you can hook it up here.
        // if (GameUIManager.Instance != null)
        // {
        //     GameUIManager.Instance.UpdateScore(redScore, blueScore);
        // }
    }
}
