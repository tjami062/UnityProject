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
    public string serverHost = "192.168.0.12";   // LAN host IP
    public int serverPort = 5000;
    public string playerName = "Player";

    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _receiveThread;
    private bool _running = false;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[NC] Awake()");
    }

    private void Start()
    {
        Debug.Log("[NC] Start() → Connecting to server...");
        ConnectToServer();
    }

    private void OnDestroy()
    {
        Debug.Log("[NC] OnDestroy() → Disconnecting");
        Disconnect();
    }

    private void Update()
    {
        while (_incomingMessages.TryDequeue(out string msg))
        {
            HandleServerMessage(msg);
        }
    }



    // ─────────────────────────────────────────────
    //  CONNECTION
    // ─────────────────────────────────────────────
    public void ConnectToServer()
    {
        Debug.Log("[NC] ConnectToServer() called");

        try
        {
            _client = new TcpClient();
            Debug.Log("[NC] TcpClient created. Connecting to " + serverHost + ":" + serverPort);

            _client.Connect(serverHost, serverPort);
            _client.NoDelay = true;  // IMPORTANT: Apply AFTER Connect()

            Debug.Log("[NC] CONNECTED to server!");

            NetworkStream ns = _client.GetStream();

            _reader = new StreamReader(ns);
            _writer = new StreamWriter(ns);
            _writer.AutoFlush = true;

            Debug.Log("[NC] StreamReader & StreamWriter created (AutoFlush = true)");

            _running = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true
            };
            _receiveThread.Start();

            Debug.Log("[NC] Receive thread started.");

            // SEND JOIN
            Debug.Log("[NC] ABOUT TO SEND JOIN");
            Send($"JOIN {playerName}");
            Debug.Log("[NC] JOIN SENT");

        }
        catch (Exception ex)
        {
            Debug.LogError("[NC] Failed to connect: " + ex.Message);
        }
    }

    private void ReceiveLoop()
    {
        Debug.Log("[NC] ReceiveLoop() STARTED");

        try
        {
            while (_running && _client != null && _client.Connected)
            {
                string line = _reader.ReadLine();
                if (line == null)
                {
                    Debug.Log("[NC] ReceiveLoop: server closed connection.");
                    break;
                }

                Debug.Log("[NC] Received: " + line);
                _incomingMessages.Enqueue(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] ReceiveLoop exception: " + ex.Message);
        }

        Debug.Log("[NC] ReceiveLoop() EXITED");
        _running = false;
    }

    public void Send(string msg)
    {
        try
        {
            Debug.Log("[NC] Trying to send: " + msg);

            if (_writer == null)
            {
                Debug.LogError("[NC] ERROR: Writer is NULL!");
                return;
            }

            _writer.WriteLine(msg);
            _writer.Flush();

            Debug.Log("[NC] Sent + Flushed: " + msg);
        }
        catch (Exception ex)
        {
            Debug.LogError("[NC] Send FAILED: " + ex.Message);
        }
    }

    public void Disconnect()
    {
        Debug.Log("[NC] Disconnect() called");

        _running = false;

        try
        {
            _client?.Close();
            Debug.Log("[NC] Client closed");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] Disconnect exception: " + ex.Message);
        }

        _client = null;
    }



    // ─────────────────────────────────────────────
    //  OUTGOING GAME MESSAGES
    // ─────────────────────────────────────────────
    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;
        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    public void SendFlagPickup(Team flagTeam)
    {
        Send($"FLAG_PICKUP {flagTeam}");
    }

    public void SendFlagDrop(Team flagTeam, Vector3 pos)
    {
        Send($"FLAG_DROP {flagTeam} {pos.x} {pos.y} {pos.z}");
    }

    public void SendFlagCapture(Team scoringTeam)
    {
        Send($"FLAG_CAPTURE {scoringTeam}");
    }

    public void SendPlayerHit(int targetId, int dmg)
    {
        Send($"HIT {targetId} {dmg}");
    }



    // ─────────────────────────────────────────────
    //  MESSAGE HANDLER
    // ─────────────────────────────────────────────
    private void HandleServerMessage(string msg)
    {
        Debug.Log("[NC] HandleServerMessage: " + msg);

        string[] parts = msg.Split(' ');
        if (parts.Length == 0) return;

        switch (parts[0])
        {
            case "WELCOME": HandleWelcome(parts); break;
            case "PLAYER_JOINED": HandlePlayerJoined(parts); break;
            case "PLAYER_LEFT": HandlePlayerLeft(parts); break;
            case "POS": HandlePos(parts); break;
            case "FLAG_STATE": HandleFlagState(parts); break;
            case "SCORE": HandleScore(parts); break;
            case "GAME_OVER": HandleGameOver(parts); break;
            case "MATCH_RESET": HandleMatchReset(parts); break;
            case "PLAYER_HIT": HandlePlayerHit(parts); break;
            case "PLAYER_DEAD": HandlePlayerDead(parts); break;
        }
    }



    // ─────────────────────────────────────────────
    //  WELCOME
    // ─────────────────────────────────────────────
    private void HandleWelcome(string[] parts)
    {
        Debug.Log("[NC] HandleWelcome");

        if (parts.Length < 3) return;

        LocalPlayerId = int.Parse(parts[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), parts[2]);

        Debug.Log($"[NC] Assigned LocalPlayerId={LocalPlayerId}, Team={LocalTeam}");

        var pt = FindObjectOfType<PlayerTeam>();
        if (pt != null)
        {
            pt.team = LocalTeam;
            pt.isLocalPlayer = true;

            GameManager.Instance?.SpawnPlayer(pt);
        }
        else Debug.LogError("[NC] No PlayerTeam in scene!");
    }


    // ─────────────────────────────────────────────
    //  PLAYER JOINED
    // ─────────────────────────────────────────────
    private void HandlePlayerJoined(string[] parts)
    {
        if (parts.Length < 4) return;

        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return;

        Team team = (Team)Enum.Parse(typeof(Team), parts[2]);
        string name = string.Join(" ", parts, 3, parts.Length - 3);

        Debug.Log($"[NC] Remote player joined: {id} {team} {name}");

        if (_remotePlayers.ContainsKey(id)) return;

        GameObject obj = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
        RemotePlayer rp = obj.GetComponent<RemotePlayer>();
        rp.playerId = id;
        rp.team = team;

        _remotePlayers[id] = rp;
    }


    // ─────────────────────────────────────────────
    //  PLAYER LEFT
    // ─────────────────────────────────────────────
    private void HandlePlayerLeft(string[] parts)
    {
        if (parts.Length < 2) return;

        int id = int.Parse(parts[1]);
        Debug.Log("[NC] Player left = " + id);

        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            Destroy(rp.gameObject);
            _remotePlayers.Remove(id);
        }
    }


    // ─────────────────────────────────────────────
    //  POSITION UPDATES
    // ─────────────────────────────────────────────
    private void HandlePos(string[] parts)
    {
        if (parts.Length < 7) return;

        int id = int.Parse(parts[1]);
        if (id == LocalPlayerId) return;

        if (!_remotePlayers.TryGetValue(id, out RemotePlayer rp)) return;

        float x = float.Parse(parts[2]);
        float y = float.Parse(parts[3]);
        float z = float.Parse(parts[4]);
        float ry = float.Parse(parts[5]);

        rp.SetNetworkState(new Vector3(x, y, z), ry);
    }


    // ─────────────────────────────────────────────
    //  FLAG / SCORE / GAME OVER / RESET / DAMAGE
    // ─────────────────────────────────────────────
    private void HandleFlagState(string[] parts)
    {
        Debug.Log("[NC] HandleFlagState");
    }

    private void HandleScore(string[] parts)
    {
        Debug.Log("[NC] HandleScore");
    }

    private void HandleGameOver(string[] parts)
    {
        Debug.Log("[NC] HandleGameOver");
    }

    private void HandleMatchReset(string[] parts)
    {
        Debug.Log("[NC] HandleMatchReset");
    }

    private void HandlePlayerHit(string[] parts)
    {
        Debug.Log("[NC] HandlePlayerHit");
    }

    private void HandlePlayerDead(string[] parts)
    {
        Debug.Log("[NC] HandlePlayerDead");
    }
}
