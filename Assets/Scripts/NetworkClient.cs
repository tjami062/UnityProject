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
    public string serverHost = "192.168.0.12"; //Ip address at Tosh's place (change as needed for testing)
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

    private readonly List<string[]> _pendingJoins = new List<string[]>();
    private bool _receivedWelcome = false;

    public bool IsConnected => _client != null && _client.Connected;


    // this is for loading the script before other scripts' Start methods run
    private void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[NC] Awake");
    }
    // this is for connecting to the server after all Awake methods have run
    private void Start()
    {
        Debug.Log("[NC] Connecting...");
        ConnectToServer();
    }

    // this is for processing incoming messages on the main thread
    private void Update()
    {
        while (_incomingMessages.TryDequeue(out string msg))
            HandleServerMessage(msg);
    }

    // this is for cleaning up the connection when the object is destroyed
    private void OnDestroy()
    {
        Disconnect();
    }

    // this is for connecting to the server
    public void ConnectToServer()
    {
        StartCoroutine(ConnectRoutine());
    }

    // this is the coroutine that attempts to connect to the server
    private IEnumerator ConnectRoutine()
    {
        while (true)
        {
            try
            {
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
            catch { }

            yield return new WaitForSeconds(1f);
        }
    }

    //this is the loop that receives messages from the server
    private void ReceiveLoop()
    {
        try
        {
            while (_running && _client.Connected)
            {
                string line = _reader.ReadLine();
                if (line == null)
                    break;
                _incomingMessages.Enqueue(line);
            }
        }
        catch { }

        _running = false;
    }

    // this is for sending messages to the server
    public void Send(string msg)
    {
        try
        {
            _writer?.WriteLine(msg);
            Debug.Log("[NC] → " + msg);
        }
        catch { }
    }

    // this is for disconnecting from the server
    public void Disconnect()
    {
        _running = false;

        try { _client?.Close(); } catch { }
        _client = null;
    }


    // this is for sending the player's position and rotation to the server
    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;
        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    // this notifies the server if a flag is picked up
    public void SendFlagPickup(Team t) =>
        Send($"FLAG_PICKUP {t}");

    // this notifies the server if a flag is dropped
    public void SendFlagDrop(Team t, Vector3 pos) =>
        Send($"FLAG_DROP {t} {pos.x} {pos.y} {pos.z}");

    // this notifies the server if a flag is captured (if the user scores)
    public void SendFlagCapture(Team t) =>
        Send($"FLAG_CAPTURE {t}");

    // this notifies the server if a player hits (shoots) another player
    public void SendPlayerHit(int target, int dmg, int shooter) =>
        Send($"HIT {target} {dmg} {shooter}");

    // this sends messages to the server every time something significant happens in the game (e.g., player joins, leaves, moves, scores, etc.)
    private void HandleServerMessage(string msg)
    {
        Debug.Log("[NC] ← " + msg);
        string[] p = msg.Split(' ');

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

    // this is for handling the welcome message from the server
    private void HandleWelcome(string[] p)
    {
        _receivedWelcome = true;

        LocalPlayerId = int.Parse(p[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), p[2]);

        var pt = FindFirstObjectByType<PlayerTeam>();
        pt.team = LocalTeam;
        pt.isLocalPlayer = true;
        GameManager.Instance.SpawnPlayer(pt);

        // Spawn any remote players received before welcome
        foreach (var join in _pendingJoins)
            SpawnRemote(join);

        _pendingJoins.Clear();
    }

    // this is for handling a player joining the game
    private void HandlePlayerJoined(string[] p)
    {
        if (!_receivedWelcome)
        {
            _pendingJoins.Add(p);
            return;
        }

        SpawnRemote(p);
    }

    // this is for spawning a remote player
    private void SpawnRemote(string[] p)
    {
        int id = int.Parse(p[1]);
        Team t = (Team)Enum.Parse(typeof(Team), p[2]);

        if (id == LocalPlayerId || _remotePlayers.ContainsKey(id))
            return;

        GameObject obj = Instantiate(remotePlayerPrefab);
        RemotePlayer rp = obj.GetComponent<RemotePlayer>();
        rp.playerId = id;
        rp.team = t;

        _remotePlayers[id] = rp;
    }

    // this is for handling a player leaving the game
    private void HandlePlayerLeft(string[] p)
    {
        int id = int.Parse(p[1]);
        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            Destroy(rp.gameObject);
            _remotePlayers.Remove(id);
        }
    }

    // this is for handling position updates from remote players
    private void HandlePos(string[] p)
    {
        int id = int.Parse(p[1]);
        if (_remotePlayers.TryGetValue(id, out RemotePlayer rp))
        {
            rp.SetNetworkState(
                new Vector3(float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4])),
                float.Parse(p[5])
            );
        }
    }

    // this is for handling flag state updates from the server
    private void HandleFlagState(string[] p)
    {
        Team ft = (Team)Enum.Parse(typeof(Team), p[1]);
        string state = p[2];
        int carrier = int.Parse(p[3]);

        Flag f = GameManager.Instance?.GetFlagForTeam(ft);
        if (f == null) return;

        if (state != "CARRIED")
        {
            f.ApplyNetworkAtBase();
            return;
        }

        
        if (carrier == LocalPlayerId)
        {
            f.ApplyNetworkCarriedByLocal(FindFirstObjectByType<PlayerTeam>());
        }
        else if (_remotePlayers.TryGetValue(carrier, out RemotePlayer rp))
        {
            rp.AttachCarriedFlag(f);
        }
    }


    // this is for handling score updates from the server
    private void HandleScore(string[] p)
    {
        GameManager.Instance.SetScoreFromServer(int.Parse(p[1]), int.Parse(p[2]));
    }

    // this is for handling game over messages from the server
    private void HandleGameOver(string[] p)
    {
        MatchOver = true;
        GameUIManager.Instance.ShowGameOver(
            (Team)Enum.Parse(typeof(Team), p[1])
        );
    }

    // this is for handling match reset messages from the server
    private void HandleMatchReset(string[] p)
    {
        MatchOver = false;
        GameUIManager.Instance.HideGameOver();

        var pt = FindFirstObjectByType<PlayerTeam>();
        GameManager.Instance.SpawnPlayer(pt);
    }

    // this is for handling player hit messages from the server
    private void HandlePlayerHit(string[] p)
    {
        int target = int.Parse(p[1]);
        int dmg = int.Parse(p[2]);
        int shooter = int.Parse(p[3]);

        if (target == LocalPlayerId)
            FindFirstObjectByType<Health>()?.ApplyNetworkDamage(dmg, shooter);

        if (shooter == LocalPlayerId)
            HitMarkerUI.Instance.ShowHitMarker();
    }

    // this is for handling player death messages from the server
    private void HandlePlayerDead(string[] p)
    {
        Debug.Log($"[NC] Player {p[1]} died to {p[2]}");
    }
}
