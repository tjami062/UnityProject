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
    public string serverHost = "192.168.0.12";
    public int serverPort = 5000;
    public string playerName = "Player";

    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;
    private Thread _receiveThread;
    private bool _running = false;

    // Thread-safe incoming queue
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
        Debug.Log("[NC] Starting connection...");
        ConnectToServer();
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void Update()
    {
        while (_incomingMessages.TryDequeue(out string msg))
        {
            HandleServerMessage(msg);
        }
    }

    // ============================================================
    // CONNECTION + RETRY LOOP
    // ============================================================

    public void ConnectToServer()
    {
        StartCoroutine(ConnectRoutine());
    }

    private IEnumerator ConnectRoutine()
    {
        Debug.Log("[NC] ConnectRoutine started...");

        while (true)
        {
            bool failed = false;

            try
            {
                Debug.Log($"[NC] Connecting to {serverHost}:{serverPort} ...");

                _client = new TcpClient();
                _client.NoDelay = true;
                _client.Connect(serverHost, serverPort);

                NetworkStream ns = _client.GetStream();
                _reader = new StreamReader(ns);
                _writer = new StreamWriter(ns) { AutoFlush = true };

                _running = true;

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true
                };
                _receiveThread.Start();

                Debug.Log("[NC] Connected → Sending JOIN...");
                Send($"JOIN {playerName}");

                yield break; // SUCCESS
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NC] Connect failed: " + ex.Message);
                failed = true;
            }

            if (failed)
            {
                // IMPORTANT: yield goes OUTSIDE catch
                yield return new WaitForSeconds(1f);
            }
        }
    }

    private void ReceiveLoop()
    {
        Debug.Log("[NC] ReceiveLoop started.");

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

        _running = false;
        Debug.Log("[NC] ReceiveLoop ended.");
    }

    // ============================================================
    // SEND
    // ============================================================

    public void Send(string msg)
    {
        try
        {
            _writer?.WriteLine(msg);
            Debug.Log("[NC] Sent → " + msg);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] Send error: " + ex.Message);
        }
    }

    public void Disconnect()
    {
        Debug.Log("[NC] Disconnecting...");
        _running = false;

        try { _client?.Close(); } catch { }
        _client = null;
    }

    // ============================================================
    // OUTGOING GAMEPLAY MESSAGES
    // ============================================================

    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;
        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    public void SendFlagPickup(Team flagTeam)
    {
        if (!IsConnected) return;
        Send($"FLAG_PICKUP {flagTeam}");
    }

    public void SendFlagDrop(Team flagTeam, Vector3 position)
    {
        if (!IsConnected) return;
        Send($"FLAG_DROP {flagTeam} {position.x} {position.y} {position.z}");
    }

    public void SendFlagCapture(Team scoringTeam)
    {
        if (!IsConnected) return;
        Send($"FLAG_CAPTURE {scoringTeam}");
    }

    public void SendPlayerHit(int targetId, int damage, int shooterId)
    {
        if (!IsConnected) return;
        Send($"HIT {targetId} {damage} {shooterId}");
    }

    // ============================================================
    // INCOMING SERVER MESSAGES
    // ============================================================

    private void HandleServerMessage(string msg)
    {
        Debug.Log("[NC] ← " + msg);

        string[] p = msg.Split(' ');
        if (p.Length == 0) return;

        switch (p[0])
        {
            case "WELCOME": HandleWelcome(p); break;
            case "PLAYER_JOINED": HandlePlayerJoined(p); break;
            case "PLAYER_LEFT": HandlePlayerLeft(p); break;
            case "POS": HandlePos(p); break;
            case "FLAG_STATE": HandleFlagState(p); break;
            case "SCORE": HandleScore(p); break;
            case "GAME_OVER": HandleGameOver(p); break;
            case "MATCH_RESET": HandleMatchReset(p); break;
            case "PLAYER_HIT": HandlePlayerHit(p); break;
            case "PLAYER_DEAD": HandlePlayerDead(p); break;
            default:
                Debug.LogWarning("[NC] Unknown message: " + msg);
                break;
        }
    }

    // ============================================================
    // MESSAGE HANDLERS
    // ============================================================

    private void HandleWelcome(string[] p)
    {
        LocalPlayerId = int.Parse(p[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), p[2]);

        Debug.Log($"[NC] You are Player {LocalPlayerId} on {LocalTeam}");

        PlayerTeam pt = FindFirstObjectByType<PlayerTeam>();
        if (pt != null)
        {
            pt.team = LocalTeam;
            pt.isLocalPlayer = true;
            GameManager.Instance?.SpawnPlayer(pt);
        }
    }

    private void HandlePlayerJoined(string[] p)
    {
        int id = int.Parse(p[1]);
        if (id == LocalPlayerId) return;

        Team team = (Team)Enum.Parse(typeof(Team), p[2]);
        string name = string.Join(" ", p, 3, p.Length - 3);

        Debug.Log($"[NC] Remote joined → {id} Team={team}");

        if (!_remotePlayers.ContainsKey(id))
        {
            GameObject obj = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
            RemotePlayer rp = obj.GetComponent<RemotePlayer>();
            rp.playerId = id;
            rp.team = team;

            _remotePlayers[id] = rp;
        }
    }

    private void HandlePlayerLeft(string[] p)
    {
        int id = int.Parse(p[1]);
        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            Destroy(rp.gameObject);
            _remotePlayers.Remove(id);
        }
    }

    private void HandlePos(string[] p)
    {
        int id = int.Parse(p[1]);
        if (id == LocalPlayerId) return;

        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            float x = float.Parse(p[2]);
            float y = float.Parse(p[3]);
            float z = float.Parse(p[4]);
            float ry = float.Parse(p[5]);

            rp.SetNetworkState(new Vector3(x, y, z), ry);
        }
    }

    private void HandleFlagState(string[] p)
    {
        Team flagTeam = (Team)Enum.Parse(typeof(Team), p[1]);
        string state = p[2];
        int carrier = int.Parse(p[3]);
        float x = float.Parse(p[4]);
        float y = float.Parse(p[5]);
        float z = float.Parse(p[6]);

        Flag flag = GameManager.Instance?.GetFlagForTeam(flagTeam);
        if (flag == null) return;

        switch (state)
        {
            case "AT_BASE":
                flag.ApplyNetworkAtBase();
                break;

            case "CARRIED":
                if (carrier == LocalPlayerId)
                {
                    flag.ApplyNetworkCarriedByLocal(FindFirstObjectByType<PlayerTeam>());
                }
                else if (_remotePlayers.TryGetValue(carrier, out RemotePlayer rp))
                {
                    flag.ApplyNetworkCarriedByRemote(rp.transform);
                }
                break;

            case "DROPPED":
                flag.ApplyNetworkDropped(new Vector3(x, y, z));
                break;
        }
    }

    private void HandleScore(string[] p)
    {
        int r = int.Parse(p[1]);
        int b = int.Parse(p[2]);
        GameManager.Instance?.SetScoreFromServer(r, b);
    }

    private void HandleGameOver(string[] p)
    {
        if (!Enum.TryParse(p[1], out Team winner)) return;

        MatchOver = true;
        GameUIManager.Instance?.ShowGameOver(winner);
    }

    private void HandleMatchReset(string[] p)
    {
        MatchOver = false;
        GameUIManager.Instance?.HideGameOver();

        PlayerTeam pt = FindFirstObjectByType<PlayerTeam>();
        if (pt != null)
            GameManager.Instance?.SpawnPlayer(pt);
    }

    private void HandlePlayerHit(string[] p)
    {
        int target = int.Parse(p[1]);
        int damage = int.Parse(p[2]);
        int shooter = int.Parse(p[3]);

        if (target == LocalPlayerId)
            FindFirstObjectByType<Health>()?.ApplyNetworkDamage(damage, shooter);

        if (shooter == LocalPlayerId)
            HitMarkerUI.Instance?.ShowHitMarker();
    }

    private void HandlePlayerDead(string[] p)
    {
        int deadId = int.Parse(p[1]);
        int killerId = int.Parse(p[2]);
        Debug.Log($"[NC] Player {deadId} died by {killerId}");
    }
}
