using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System.Collections;

public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance { get; private set; }

    [Header("Connection Settings")]
    public string serverHost = "192.168.0.12";   // LAN host IP (IP at Tosh's place)
    public int serverPort = 5000;
    public string playerName = "Player";

    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _receiveThread;
    private bool _running = false;

    // Thread-safe queue for server messages
    private readonly ConcurrentQueue<string> _incomingMessages =
        new ConcurrentQueue<string>();

    [Header("Local Player Info")]
    public int LocalPlayerId { get; private set; } = -1;
    public Team LocalTeam { get; private set; } = Team.Red;
    public bool MatchOver { get; private set; } = false;

    [Header("Remote Players")]
    public GameObject remotePlayerPrefab;
    private readonly Dictionary<int, RemotePlayer> _remotePlayers =
        new Dictionary<int, RemotePlayer>();

    public bool IsConnected => _client != null && _client.Connected;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        // ensures this behaves like a singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[NC] NetworkClient Awake()");
    }

    private void Start()
    {
        // attempts to establish a connection when the game starts
        Debug.Log("[NC] Starting connection...");
        ConnectToServer();
    }

    private void OnDestroy()
    {
        // disconnects when the object is destroyed
        Disconnect();
    }

    private void Update()
    {
        // processes incoming messages from the server
        while (_incomingMessages.TryDequeue(out string msg))
        {
            HandleServerMessage(msg);
        }
    }

    // ============================================================
    // Connection Logic (with retry)
    // ============================================================

    public void ConnectToServer()
    {
        StartCoroutine(ConnectRoutine());
    }

    // attempts to connect to the server and retries every second if it fails
    private IEnumerator ConnectRoutine()
    {
        Debug.Log("[NC] Starting ConnectRoutine...");

        while (true)
        {
            bool connectionFailed = false;

            try
            {
                Debug.Log($"[NC] Attempting connect to {serverHost}:{serverPort} ...");

                _client = new TcpClient();
                _client.NoDelay = true;
                _client.Connect(serverHost, serverPort);

                NetworkStream ns = _client.GetStream();
                _reader = new StreamReader(ns);
                _writer = new StreamWriter(ns) { AutoFlush = true };

                _running = true;

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Priority = System.Threading.ThreadPriority.Normal
                };
                _receiveThread.Start();

                Debug.Log("[NC] Connected! Sending JOIN...");
                Send($"JOIN {playerName}");

                yield break; // SUCCESS — exit coroutine
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NC] Connect failed: {ex.Message}");
                connectionFailed = true;
            }

            if (connectionFailed)
                yield return new WaitForSeconds(1f);
        }
    }

    // runs on a background thread to receive messages from the server
    private void ReceiveLoop()
    {
        Debug.Log("[NC] ReceiveLoop started");

        try
        {
            while (_running && _client != null && _client.Connected)
            {
                string line = _reader.ReadLine();

                if (line == null)
                {
                    Debug.Log("[NC] Server closed connection.");
                    break;
                }

                _incomingMessages.Enqueue(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] ReceiveLoop exception: " + ex.Message);
        }

        Debug.Log("[NC] ReceiveLoop ended.");
        _running = false;
    }

    // sends a text message to the server over TCP
    public void Send(string msg)
    {
        try
        {
            if (_writer != null)
            {
                _writer.WriteLine(msg);
                Debug.Log("[NC] Sent → " + msg);
            }
            else
            {
                Debug.LogWarning("[NC] Tried to send but writer is null.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] Send error: " + ex.Message);
        }
    }

    // disconnects from the server and stops the receive thread
    public void Disconnect()
    {
        Debug.Log("[NC] Disconnecting...");

        _running = false;

        try
        {
            _client?.Close();
        }
        catch { }

        _client = null;
    }

    // ============================================================
    // Outgoing gameplay messages
    // ============================================================

    // sends the player's position and rotation to the server
    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;
        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    // notifies the server that the player has picked up a flag
    public void SendFlagPickup(Team flagTeam)
    {
        if (!IsConnected) return;
        Send($"FLAG_PICKUP {flagTeam}");
    }

    // notifies the server that the player has dropped a flag at a specific position
    public void SendFlagDrop(Team flagTeam, Vector3 position)
    {
        if (!IsConnected) return;
        Send($"FLAG_DROP {flagTeam} {position.x} {position.y} {position.z}");
    }

    // notifies the server that a flag has been captured by a team
    public void SendFlagCapture(Team scoringTeam)
    {
        if (!IsConnected) return;
        Send($"FLAG_CAPTURE {scoringTeam}");
    }

    // notifies the server that the player has hit (shoots) another player
    public void SendPlayerHit(int targetId, int damage, int shooterId)
    {
        if (!IsConnected) return;
        Send($"HIT {targetId} {damage} {shooterId}");
    }

    // ============================================================
    // Incoming message handling
    // ============================================================

    // processes a message received from the server and routes it to the appropriate handler
    private void HandleServerMessage(string msg)
    {
        Debug.Log("[NC] ← From server: " + msg);

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

            default:
                Debug.LogWarning("[NC] Unknown server message: " + msg);
                break;
        }
    }

    // ============================================================
    // Message Handlers
    // ============================================================

    // server tells the client its player ID and team
    private void HandleWelcome(string[] parts)
    {
        // WELCOME <playerId> <team>
        LocalPlayerId = int.Parse(parts[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), parts[2]);

        Debug.Log($"[NC] Assigned LocalPlayerId={LocalPlayerId}, Team={LocalTeam}");

        var playerTeam = FindFirstObjectByType<PlayerTeam>();
        if (playerTeam != null)
        {
            playerTeam.team = LocalTeam;
            playerTeam.isLocalPlayer = true;

            GameManager.Instance?.SpawnPlayer(playerTeam);
        }
    }

    // server notifies that a new player has joined and creates a RemotePlayer for them
    private void HandlePlayerJoined(string[] parts)
    {
        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return;

        Team team = (Team)Enum.Parse(typeof(Team), parts[2]);

        string name = string.Join(" ", parts, 3, parts.Length - 3);

        Debug.Log($"[NC] Remote joined: {id} {team} {name}");

        if (!_remotePlayers.ContainsKey(id))
        {
            GameObject obj = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
            RemotePlayer rp = obj.GetComponent<RemotePlayer>();
            rp.playerId = id;
            rp.team = team;
            _remotePlayers[id] = rp;
        }
    }

    // server notifies that a player has left and removes their RemotePlayer
    private void HandlePlayerLeft(string[] parts)
    {
        int id = int.Parse(parts[1]);

        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            Destroy(rp.gameObject);
            _remotePlayers.Remove(id);
        }
    }


    // server updates the position and rotation of a remote player
    private void HandlePos(string[] parts)
    {
        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return;

        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            float x = float.Parse(parts[2]);
            float y = float.Parse(parts[3]);
            float z = float.Parse(parts[4]);
            float ry = float.Parse(parts[5]);

            rp.SetNetworkState(new Vector3(x, y, z), ry);
        }
    }

    // server updates the state of a flag (at base, carried, dropped)
    private void HandleFlagState(string[] parts)
    {
        Team flagTeam = (Team)Enum.Parse(typeof(Team), parts[1]);
        string state = parts[2];
        int carrierId = int.Parse(parts[3]);
        float x = float.Parse(parts[4]);
        float y = float.Parse(parts[5]);
        float z = float.Parse(parts[6]);

        Flag flag = GameManager.Instance?.GetFlagForTeam(flagTeam);
        if (flag == null) return;

        switch (state)
        {
            case "AT_BASE":
                flag.ApplyNetworkAtBase();
                break;

            case "CARRIED":
                if (carrierId == LocalPlayerId)
                {
                    flag.ApplyNetworkCarriedByLocal(FindFirstObjectByType<PlayerTeam>());
                }
                else if (_remotePlayers.TryGetValue(carrierId, out RemotePlayer rp))
                {
                    flag.ApplyNetworkCarriedByRemote(rp.transform);
                }
                break;

            case "DROPPED":
                flag.ApplyNetworkDropped(new Vector3(x, y, z));
                break;
        }
    }

    // server updates the score for both teams
    private void HandleScore(string[] parts)
    {
        int red = int.Parse(parts[1]);
        int blue = int.Parse(parts[2]);

        GameManager.Instance?.SetScoreFromServer(red, blue);
    }

    // server notifies that the game is over and which team won
    private void HandleGameOver(string[] parts)
    {
        if (!Enum.TryParse(parts[1], out Team winningTeam)) return;

        MatchOver = true;
        GameUIManager.Instance?.ShowGameOver(winningTeam);
    }

    // server notifies that the match has been reset and respawns the local player
    private void HandleMatchReset(string[] parts)
    {
        MatchOver = false;
        GameUIManager.Instance?.HideGameOver();

        PlayerTeam playerTeam = FindFirstObjectByType<PlayerTeam>();
        if (playerTeam != null)
            GameManager.Instance?.SpawnPlayer(playerTeam);
    }

    // server applies damage to a player and shows hit marker if local player was the shooter
    private void HandlePlayerHit(string[] parts)
    {
        int targetId = int.Parse(parts[1]);
        int damage = int.Parse(parts[2]);
        int shooterId = int.Parse(parts[3]);

        if (targetId == LocalPlayerId)
        {
            FindFirstObjectByType<Health>()?.ApplyNetworkDamage(damage, shooterId);
        }

        if (shooterId == LocalPlayerId)
        {
            HitMarkerUI.Instance?.ShowHitMarker();
        }
    }

    // server notifies that a player has died
    private void HandlePlayerDead(string[] parts)
    {
        int deadId = int.Parse(parts[1]);
        int killerId = int.Parse(parts[2]);

        Debug.Log($"[NC] Remote death: {deadId} killed by {killerId}");
    }
}
