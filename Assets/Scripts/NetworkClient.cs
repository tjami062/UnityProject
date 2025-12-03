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

    // Prevents premature spawning
    private bool _receivedWelcome = false;

    public bool IsConnected => _client != null && _client.Connected;

    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[NC] Awake");
    }

    private void Start()
    {
        Debug.Log("[NC] Connecting...");
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
    // Connection Logic
    // ============================================================

    public void ConnectToServer()
    {
        StartCoroutine(ConnectRoutine());
    }

    private IEnumerator ConnectRoutine()
    {
        Debug.Log("[NC] ConnectRoutine Started");

        while (true)
        {
            try
            {
                Debug.Log($"[NC] Trying {serverHost}:{serverPort}");

                _client = new TcpClient();
                _client.NoDelay = true;
                _client.Connect(serverHost, serverPort);

                NetworkStream ns = _client.GetStream();
                _reader = new StreamReader(ns);
                _writer = new StreamWriter(ns) { AutoFlush = true };

                _running = true;

                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();

                Send($"JOIN {playerName}");

                yield break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NC] Connect failed: " + ex.Message);
            }

            // LEGAL — after catch
            yield return new WaitForSeconds(1f);
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
                    break;

                _incomingMessages.Enqueue(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] ReceiveLoop: " + ex.Message);
        }

        _running = false;
    }

    public void Send(string msg)
    {
        try
        {
            _writer?.WriteLine(msg);
            Debug.Log("[NC] → " + msg);
        }
        catch { }
    }

    public void Disconnect()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        _client = null;
    }

    // ============================================================
    // Outgoing Gameplay Messages
    // ============================================================

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

    public void SendPlayerHit(int targetId, int dmg, int shooter)
    {
        Send($"HIT {targetId} {dmg} {shooter}");
    }

    // ============================================================
    // Incoming Message Handling
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
        }
    }

    // ============================================================
    // WELCOME — Set Local ID + Team
    // ============================================================

    private void HandleWelcome(string[] p)
    {
        _receivedWelcome = true;

        LocalPlayerId = int.Parse(p[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), p[2]);

        Debug.Log($"[NC] WELCOME → ID={LocalPlayerId}, Team={LocalTeam}");

        var player = FindFirstObjectByType<PlayerTeam>();
        if (player != null)
        {
            player.team = LocalTeam;
            player.isLocalPlayer = true;
            GameManager.Instance.SpawnPlayer(player);
        }
    }

    // ============================================================
    // Player Spawn Logic
    // ============================================================

    private void HandlePlayerJoined(string[] p)
    {
        if (!_receivedWelcome) return; // <-- CRITICAL FIX

        int id = int.Parse(p[1]);
        Team t = (Team)Enum.Parse(typeof(Team), p[2]);

        if (id == LocalPlayerId)
            return;

        if (_remotePlayers.ContainsKey(id))
            return;

        Debug.Log($"[NC] Spawning remote {id} ({t})");

        GameObject obj = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity);
        RemotePlayer rp = obj.GetComponent<RemotePlayer>();
        rp.playerId = id;
        rp.team = t;

        _remotePlayers[id] = rp;
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

    // ============================================================
    // Flags / Score / Death
    // ============================================================

    private void HandleFlagState(string[] p)
    {
        Team ft = (Team)Enum.Parse(typeof(Team), p[1]);
        string state = p[2];
        int carrier = int.Parse(p[3]);

        float x = float.Parse(p[4]);
        float y = float.Parse(p[5]);
        float z = float.Parse(p[6]);

        Flag f = GameManager.Instance?.GetFlagForTeam(ft);
        if (f == null) return;

        switch (state)
        {
            case "AT_BASE": f.ApplyNetworkAtBase(); break;

            case "CARRIED":
                if (carrier == LocalPlayerId)
                {
                    f.ApplyNetworkCarriedByLocal(FindFirstObjectByType<PlayerTeam>());
                }
                else if (_remotePlayers.TryGetValue(carrier, out RemotePlayer rp))
                {
                    f.ApplyNetworkCarriedByRemote(rp.transform);
                }
                break;

            case "DROPPED":
                f.ApplyNetworkDropped(new Vector3(x, y, z));
                break;
        }
    }

    private void HandleScore(string[] p)
    {
        GameManager.Instance?.SetScoreFromServer(int.Parse(p[1]), int.Parse(p[2]));
    }

    private void HandleGameOver(string[] p)
    {
        MatchOver = true;
        GameUIManager.Instance?.ShowGameOver((Team)Enum.Parse(typeof(Team), p[1]));
    }

    private void HandleMatchReset(string[] p)
    {
        MatchOver = false;
        GameUIManager.Instance?.HideGameOver();

        var pt = FindFirstObjectByType<PlayerTeam>();
        if (pt != null)
            GameManager.Instance.SpawnPlayer(pt);
    }

    private void HandlePlayerHit(string[] p)
    {
        int target = int.Parse(p[1]);
        int dmg = int.Parse(p[2]);
        int shooter = int.Parse(p[3]);

        if (target == LocalPlayerId)
            FindFirstObjectByType<Health>()?.ApplyNetworkDamage(dmg, shooter);

        if (shooter == LocalPlayerId)
            HitMarkerUI.Instance?.ShowHitMarker();
    }

    private void HandlePlayerDead(string[] p)
    {
        Debug.Log($"[NC] Player {p[1]} died to {p[2]}");
    }
}
