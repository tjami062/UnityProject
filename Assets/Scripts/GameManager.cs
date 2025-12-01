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
        // DontDestroyOnLoad(gameObject); // optional
    }

    public void SpawnPlayer(PlayerTeam player)
    {
        if (player == null)
        {
            Debug.LogError("GameManager.SpawnPlayer called with null player.");
            return;
        }

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
            Debug.LogError($"Spawn point not assigned for team {player.team}.");
            return;
        }

        player.TeleportTo(spawn);
    }

    public Flag GetFlagForTeam(Team team)
    {
        return team == Team.Red ? redFlag : blueFlag;
    }

    public void SetScoreFromServer(int red, int blue)
    {
        redScore = red;
        blueScore = blue;
    }
}
