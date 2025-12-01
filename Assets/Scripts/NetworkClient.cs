using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance { get; private set; }

    [Header("Connection Settings")]
    public string serverHost = "192.168.0.12";
    public int serverPort = 5000;
    public string playerName = "Player";

    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _receiveThread;
    private bool _running = false;

    private readonly ConcurrentQueue<string> _incomingMessages = new ConcurrentQueue<string>();

    [Header("Local Player Info")]
    public int LocalPlayerId { get; private set; } = -1;
    public Team LocalTeam { get; private set; } = Team.Red;
    public bool MatchOver { get; private set; } = false;

    [Header("Local Player Reference")]
    // Assign your local Player (the one with PlayerTeam) here in the Inspector
    public PlayerTeam localPlayerTeam;

    [Header("Remote Players")]
    public GameObject remotePlayerPrefab;
    private readonly Dictionary<int, RemotePlayer> _remotePlayers = new Dictionary<int, RemotePlayer>();

    public bool IsConnected => _client != null && _client.Connected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ConnectToServer();
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void Update()
    {
        // Process all pending messages from server on main thread
        while (_incomingMessages.TryDequeue(out string msg))
        {
            HandleServerMessage(msg);
        }
    }

    public void ConnectToServer()
    {
        try
        {
            _client = new TcpClient();
            _client.NoDelay = true; // disable Nagle for snappier updates
            _client.Connect(serverHost, serverPort);

            NetworkStream ns = _client.GetStream();
            _reader = new StreamReader(ns);
            _writer = new StreamWriter(ns) { AutoFlush = true };

            _running = true;
            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();

            Debug.Log("Connected to server.");

            // First message: JOIN <name>
            Send($"JOIN {playerName}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to connect: " + ex.Message);
        }
    }

    private void ReceiveLoop()
    {
        try
        {
            while (_running && _client != null && _client.Connected)
            {
                string line = _reader.ReadLine();
                if (line == null)
                {
                    Debug.Log("Disconnected from server.");
                    break;
                }

                _incomingMessages.Enqueue(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ReceiveLoop error: " + ex.Message);
        }
        finally
        {
            _running = false;
        }
    }

    public void Send(string msg)
    {
        try
        {
            if (_writer != null)
            {
                _writer.WriteLine(msg);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Send error: " + ex.Message);
        }
    }

    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;
        // POS x y z yaw pitch
        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    private void HandleServerMessage(string msg)
    {
        Debug.Log("From server: " + msg);

        string[] parts = msg.Split(' ');
        if (parts.Length == 0) return;

        switch (parts[0])
        {
            case "WELCOME":
                HandleWelcome(parts);
                break;

            case "PLAYER_JOINED":
                HandlePlayerJoined(parts);
                break;

            case "PLAYER_LEFT":
                HandlePlayerLeft(parts);
                break;

            case "POS":
                HandlePos(parts);
                break;

            case "FLAG_STATE":
                HandleFlagState(parts);
                break;

            case "SCORE":
                HandleScore(parts);
                break;

            case "GAME_OVER":
                HandleGameOver(parts);
                break;

            case "MATCH_RESET":
                HandleMatchReset(parts);
                break;

            case "PLAYER_HIT":
                HandlePlayerHit(parts);
                break;

            case "PLAYER_DEAD":
                HandlePlayerDead(parts);
                break;
        }
    }

    private void HandleWelcome(string[] parts)
    {
        // WELCOME <playerId> <team>
        if (parts.Length < 3) return;

        LocalPlayerId = int.Parse(parts[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), parts[2]);

        Debug.Log($"Assigned id={LocalPlayerId}, team={LocalTeam}");

        if (localPlayerTeam == null)
        {
            Debug.LogError("NetworkClient.localPlayerTeam is not assigned in the Inspector!");
            return;
        }

        // Set up local player
        localPlayerTeam.team = LocalTeam;
        localPlayerTeam.isLocalPlayer = true;

        // Spawn at correct base
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SpawnPlayer(localPlayerTeam);
        }
    }

    private void HandlePlayerJoined(string[] parts)
    {
        // PLAYER_JOINED <playerId> <team> <name...>
        if (parts.Length < 4) return;

        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return;

        Team team = (Team)Enum.Parse(typeof(Team), parts[2]);
        string name = string.Join(" ", parts, 3, parts.Length - 3);

        if (_remotePlayers.ContainsKey(id)) return;

        if (remotePlayerPrefab != null)
        {
            GameObject obj = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
            RemotePlayer rp = obj.GetComponent<RemotePlayer>();
            rp.playerId = id;
            rp.team = team;
            _remotePlayers[id] = rp;
        }

        Debug.Log($"Remote player joined: {id} {team} {name}");
    }

    private void HandlePlayerLeft(string[] parts)
    {
        // PLAYER_LEFT <playerId>
        if (parts.Length < 2) return;
        int id = int.Parse(parts[1]);

        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            Destroy(rp.gameObject);
            _remotePlayers.Remove(id);
        }

        Debug.Log($"Remote player left: {id}");
    }

    private void HandlePos(string[] parts)
    {
        // POS <playerId> x y z ry pitch
        if (parts.Length < 7) return;

        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return; // ignore our own echo

        if (!_remotePlayers.TryGetValue(id, out RemotePlayer rp))
            return;

        float x = float.Parse(parts[2]);
        float y = float.Parse(parts[3]);
        float z = float.Parse(parts[4]);
        float ry = float.Parse(parts[5]);
        // float pitch = float.Parse(parts[6]); // ignored for now

        // Use smoothing on the RemotePlayer
        rp.SetNetworkTransform(new Vector3(x, y, z), ry);
    }

    private void HandleFlagState(string[] parts)
    {
        // FLAG_STATE <flagTeam> <state> <carrierId> x y z
        if (parts.Length < 7) return;

        Team flagTeam = (Team)Enum.Parse(typeof(Team), parts[1]);
        string state = parts[2];
        int carrierId = int.Parse(parts[3]);
        float x = float.Parse(parts[4]);
        float y = float.Parse(parts[5]);
        float z = float.Parse(parts[6]);

        if (GameManager.Instance == null) return;
        Flag flag = GameManager.Instance.GetFlagForTeam(flagTeam);
        if (flag == null) return;

        switch (state)
        {
            case "AT_BASE":
                flag.ApplyNetworkAtBase();
                break;

            case "CARRIED":
                if (carrierId == LocalPlayerId)
                {
                    if (localPlayerTeam != null)
                        flag.ApplyNetworkCarriedByLocal(localPlayerTeam);
                }
                else
                {
                    if (_remotePlayers.TryGetValue(carrierId, out RemotePlayer rp))
                    {
                        flag.ApplyNetworkCarriedByRemote(rp.transform);
                    }
                    else
                    {
                        flag.ApplyNetworkAtBase();
                    }
                }
                break;

            case "DROPPED":
                flag.ApplyNetworkDropped(new Vector3(x, y, z));
                break;
        }
    }

    private void HandleScore(string[] parts)
    {
        // SCORE <red> <blue>
        if (parts.Length < 3) return;

        int red = int.Parse(parts[1]);
        int blue = int.Parse(parts[2]);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetScoreFromServer(red, blue);
        }
    }

    private void HandleGameOver(string[] parts)
    {
        // GAME_OVER <team>
        if (parts.Length < 2) return;

        if (!Enum.TryParse(parts[1], out Team winningTeam))
            return;

        MatchOver = true;

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.ShowGameOver(winningTeam);
        }

        Debug.Log("Game over! Winning team: " + winningTeam);
    }

    private void HandleMatchReset(string[] parts)
    {
        // MATCH_RESET
        MatchOver = false;

        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.HideGameOver();
        }

        // Respawn local player at their base
        if (localPlayerTeam != null && GameManager.Instance != null)
        {
            GameManager.Instance.SpawnPlayer(localPlayerTeam);
        }

        Debug.Log("Match reset by server.");
    }

    private void HandlePlayerHit(string[] parts)
    {
        // PLAYER_HIT targetId damage shooterId
        if (parts.Length < 4) return;

        int targetId = int.Parse(parts[1]);
        int damage = int.Parse(parts[2]);
        int shooterId = int.Parse(parts[3]);

        if (targetId == LocalPlayerId)
        {
            Health health = FindObjectOfType<Health>();
            if (health != null)
            {
                health.ApplyNetworkDamage(damage, shooterId);
            }
        }
    }

    private void HandlePlayerDead(string[] parts)
    {
        // PLAYER_DEAD deadId killerId
        if (parts.Length < 3) return;

        int deadId = int.Parse(parts[1]);
        int killerId = int.Parse(parts[2]);

        Debug.Log($"Player {deadId} died to {killerId}");

        // For remote players, we just rely on their client respawning them;
        // their new position will come through via POS updates.
    }

    public void Disconnect()
    {
        _running = false;

        try
        {
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
        }
        catch
        {
            // ignore
        }
    }
}
