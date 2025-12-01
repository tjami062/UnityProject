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
    public string serverHost = "192.168.0.12";   // LAN server IP
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


    // ---------------------------------------------------------------
    // UNITY LIFECYCLE
    // ---------------------------------------------------------------
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
        Debug.Log($"[NC] Attempting connection to {serverHost}:{serverPort}...");
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


    // ---------------------------------------------------------------
    // CONNECTION
    // ---------------------------------------------------------------
    public void ConnectToServer()
    {
        try
        {
            _client = new TcpClient();
            _client.NoDelay = true;  // IMPORTANT for fast shooters
            _client.Connect(serverHost, serverPort);

            NetworkStream ns = _client.GetStream();
            _reader = new StreamReader(ns);
            _writer = new StreamWriter(ns)
            {
                AutoFlush = false   // We manually flush to ensure reliability
            };

            _running = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true
            };
            _receiveThread.Start();

            Debug.Log("[NC] Connected to server.");

            // 🔥 CRITICAL FIX — JOIN must be flushed immediately
            Send($"JOIN {playerName}");

        }
        catch (Exception ex)
        {
            Debug.LogError("[NC] Failed to connect: " + ex.Message);
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
            Debug.LogWarning("[NC] ReceiveLoop error: " + ex.Message);
        }
        finally
        {
            _running = false;
        }
    }

    public void Disconnect()
    {
        _running = false;

        try
        {
            _client?.Close();
        }
        catch { }
    }

    // ---------------------------------------------------------------
    // SAFE SEND — ALWAYS FLUSH
    // ---------------------------------------------------------------
    public void Send(string msg)
    {
        try
        {
            if (_writer != null)
            {
                _writer.WriteLine(msg);
                _writer.Flush();   // 🔥 REQUIRED FIX
                Debug.Log("[NC] Sent: " + msg);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NC] Send error: " + ex.Message);
        }
    }


    // ---------------------------------------------------------------
    // OUTGOING GAME MESSAGES
    // ---------------------------------------------------------------
    public void SendPosition(Vector3 pos, Vector3 euler)
    {
        if (!IsConnected || MatchOver) return;

        Send($"POS {pos.x} {pos.y} {pos.z} {euler.y} {euler.x}");
    }

    public void SendFlagPickup(Team flagTeam)
    {
        Send($"FLAG_PICKUP {flagTeam}");
    }

    public void SendFlagDrop(Team flagTeam, Vector3 position)
    {
        Send($"FLAG_DROP {flagTeam} {position.x} {position.y} {position.z}");
    }

    public void SendFlagCapture(Team scoringTeam)
    {
        Send($"FLAG_CAPTURE {scoringTeam}");
    }

    public void SendPlayerHit(int targetId, int damage)
    {
        Send($"HIT {targetId} {damage} {LocalPlayerId}");
    }


    // ---------------------------------------------------------------
    // INCOMING SERVER MESSAGE ROUTING
    // ---------------------------------------------------------------
    private void HandleServerMessage(string msg)
    {
        Debug.Log("[NC] From server: " + msg);

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
            case "PLAYER_HIT": HandlePlayerHitReceived(p); break;
            case "PLAYER_DEAD": HandlePlayerDead(p); break;
        }
    }


    // ---------------------------------------------------------------
    // HANDLERS
    // ---------------------------------------------------------------
    private void HandleWelcome(string[] p)
    {
        LocalPlayerId = int.Parse(p[1]);
        LocalTeam = (Team)Enum.Parse(typeof(Team), p[2]);

        Debug.Log($"[NC] Assigned id={LocalPlayerId}, team={LocalTeam}");

        PlayerTeam team = FindObjectOfType<PlayerTeam>();
        if (team != null)
        {
            team.team = LocalTeam;
            team.isLocalPlayer = true;
            GameManager.Instance?.SpawnPlayer(team);
        }
    }

    private void HandlePlayerJoined(string[] p)
    {
        int id = int.Parse(p[1]);
        if (id == LocalPlayerId) return;

        Team t = (Team)Enum.Parse(typeof(Team), p[2]);
        string name = string.Join(" ", p, 3, p.Length - 3);

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

        if (!_remotePlayers.TryGetValue(id, out RemotePlayer rp)) return;

        float x = float.Parse(p[2]);
        float y = float.Parse(p[3]);
        float z = float.Parse(p[4]);
        float ry = float.Parse(p[5]);

        rp.SetNetworkState(new Vector3(x, y, z), ry);
    }

    private void HandleFlagState(string[] p)
    {
        Team flagTeam = (Team)Enum.Parse(typeof(Team), p[1]);
        string state = p[2];
        int carrier = int.Parse(p[3]);
        float x = float.Parse(p[4]);
        float y = float.Parse(p[5]);
        float z = float.Parse(p[6]);

        Flag flag = GameManager.Instance.GetFlagForTeam(flagTeam);
        if (flag == null) return;

        if (state == "AT_BASE")
            flag.ApplyNetworkAtBase();

        else if (state == "CARRIED")
        {
            if (carrier == LocalPlayerId)
            {
                PlayerTeam pt = FindObjectOfType<PlayerTeam>();
                flag.ApplyNetworkCarriedByLocal(pt);
            }
            else if (_remotePlayers.TryGetValue(carrier, out RemotePlayer rp))
            {
                flag.ApplyNetworkCarriedByRemote(rp.transform);
            }
        }

        else if (state == "DROPPED")
            flag.ApplyNetworkDropped(new Vector3(x, y, z));
    }

    private void HandleScore(string[] p)
    {
        GameManager.Instance.SetScoreFromServer(
            int.Parse(p[1]),
            int.Parse(p[2])
        );
    }

    private void HandleGameOver(string[] p)
    {
        MatchOver = true;
        GameUIManager.Instance.ShowGameOver(
            (Team)Enum.Parse(typeof(Team), p[1])
        );
    }

    private void HandleMatchReset(string[] p)
    {
        MatchOver = false;
        GameUIManager.Instance.HideGameOver();

        PlayerTeam team = FindObjectOfType<PlayerTeam>();
        GameManager.Instance.SpawnPlayer(team);
    }

    private void HandlePlayerHitReceived(string[] p)
    {
        int target = int.Parse(p[1]);
        int damage = int.Parse(p[2]);
        int shooter = int.Parse(p[3]);

        if (target == LocalPlayerId)
        {
            Health h = FindObjectOfType<Health>();
            h.ApplyNetworkDamage(damage, shooter);
        }

        if (shooter == LocalPlayerId)
            HitMarkerUI.Instance?.ShowHitMarker();
    }

    private void HandlePlayerDead(string[] p)
    {
        // Nothing special needed
    }
}
