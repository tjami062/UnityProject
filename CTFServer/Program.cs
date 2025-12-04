using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace CTFServer
{
    public enum Team
    {
        Red,
        Blue
    }


    //For each player connected
    //Using TCP object to read and send ,essages 
    public class PlayerConnection
    {
        public int Id;

        public Team Team;

        public string Name;

        public TcpClient Client;

        public StreamReader Reader;

        public StreamWriter Writer;
    }

    //Stores the state of each teams flag 
    //This acts as the shared variable for our project
    public class FlagState
    {
        public Team FlagTeam; 
        
        public bool AtBase = true;

        public int CarrierId = -1; 
        public float X, Y, Z;    
    }

    class Program
    {
        //Binding to 5000
        //Since this is socket based, clients must connect to a specific port 
        private const int Port = 5000;

        private static readonly object _lock = new object();
        private static readonly Dictionary<int, PlayerConnection> _players = new Dictionary<int, PlayerConnection>();
        private static int _nextPlayerId = 1;

        // Flags: one per team (Red flag at Red base, Blue flag at Blue base)
        private static readonly Dictionary<Team, FlagState> _flags = new Dictionary<Team, FlagState>();

        // Score
        private static int _redScore = 0;
        private static int _blueScore = 0;
        private const int _scoreToWin = 3;

        static void Main(string[] args)
        {
            InitFlags();

            Console.WriteLine("CTF Server starting on port " + Port);
            //Raw socket API, binds to 5000
            //Server will accept connections from any network 
            TcpListener listener = new TcpListener(IPAddress.Any, Port);


            //Tell OS to begin listening for TCP 
            listener.Start();

            Console.WriteLine("Waiting for clients...");

            while (true)
            {
                //Blocks until remote client connects 
                //Creates TCP socket end for connection 
                //Manuall Accepeting connections 
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected from " + client.Client.RemoteEndPoint);





                //TS important 
                //We gotta create a thread per player to handle everything 
                //New thread each time client connects 
                //Server can handle multiple players hell yeah 
                Thread t = new Thread(HandleClient);
                t.IsBackground = true;
                t.Start(client);
            }
        }

        private static void InitFlags()
        {
            _flags[Team.Red] = new FlagState
            {
                FlagTeam = Team.Red,
                AtBase = true,
                CarrierId = -1,
                X = 0,
                Y = 0,
                Z = 0
            };
            _flags[Team.Blue] = new FlagState
            {
                FlagTeam = Team.Blue,
                AtBase = true,
                CarrierId = -1,
                X = 0,
                Y = 0,
                Z = 0
            };
        }

        private static void HandleClient(object obj)
        {



            TcpClient client = (TcpClient)obj;

            using (client)
                //Using get stream to obtain TCP
            using (NetworkStream ns = client.GetStream())
                //Turns incoming bytes into text
            using (StreamReader reader = new StreamReader(ns))
                //Weites to client 
            using (StreamWriter writer = new StreamWriter(ns) { AutoFlush = true })
            {
                PlayerConnection player = null;

                try
                {
                    // First message must be JOIN
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        Console.WriteLine("Client disconnected before JOIN");
                        return;
                    }

                    string[] parts = line.Split(' ');
                    if (parts.Length < 2 || parts[0] != "JOIN")
                    {
                        Console.WriteLine("Invalid first message: " + line);
                        return;
                    }

                    string name = string.Join(" ", parts, 1, parts.Length - 1);

                    lock (_lock)
                    {
                        int id = _nextPlayerId++;
                        Team team = (id % 2 == 0) ? Team.Blue : Team.Red; // alternate teams

                        player = new PlayerConnection
                        {
                            Id = id,
                            Team = team,
                            Name = name,
                            Client = client,
                            Reader = reader,
                            Writer = writer
                        };

                        // 1) Tell the NEW player about all existing players
                        //Check for debug when trying to joinnnnn
                        foreach (var kv in _players)
                        {
                            var existing = kv.Value;
                            writer.WriteLine($"PLAYER_JOINED {existing.Id} {existing.Team} {existing.Name}");
                        }

                        // 2) Add new player to list
                        _players.Add(id, player);

                        Console.WriteLine($"Player {id} ({name}) joined as {team}");

                        // 3) Send WELCOME to new player
                        writer.WriteLine($"WELCOME {player.Id} {player.Team}");

                        // 4) Send current score
                        writer.WriteLine($"SCORE {_redScore} {_blueScore}");

                        // 5) Send current flag states
                        foreach (var fk in _flags)
                        {
                            var f = fk.Value;
                            string state = f.AtBase
                                ? "AT_BASE"
                                : (f.CarrierId != -1 ? "CARRIED" : "DROPPED");

                            if (state == "AT_BASE")
                                writer.WriteLine($"FLAG_STATE {f.FlagTeam} AT_BASE -1");
                            else
                                writer.WriteLine($"FLAG_STATE {f.FlagTeam} {state} {f.CarrierId} {f.X} {f.Y} {f.Z}");
                            ;

                            writer.WriteLine(
                                $"FLAG_STATE {f.FlagTeam} {state} {f.CarrierId} {f.X} {f.Y} {f.Z}");
                        }

                        // 6) Tell everyone else that THIS player joined
                        BroadcastExcept(player.Id, $"PLAYER_JOINED {player.Id} {player.Team} {player.Name}");
                    }

                    // Main receive loop for this client
                    while (true)
                    {
                        string msg = reader.ReadLine();
                        if (msg == null)
                        {
                            Console.WriteLine($"Player {player.Id} disconnected.");
                            break;
                        }

                        HandleMessage(player, msg);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in client handler: " + ex.Message);
                }
                finally
                {
                    if (player != null)
                    {
                        lock (_lock)
                        {
                            if (_players.ContainsKey(player.Id))
                            {
                                _players.Remove(player.Id);
                                BroadcastExcept(player.Id, $"PLAYER_LEFT {player.Id}");
                            }

                            // If this player was carrying any flag, drop it
                            foreach (var kv in _flags)
                            {
                                var f = kv.Value;
                                if (f.CarrierId == player.Id)
                                {
                                    f.CarrierId = -1;
                                    f.AtBase = false; // dropped
                                    BroadcastAll($"FLAG_STATE {f.FlagTeam} DROPPED -1 {f.X} {f.Y} {f.Z}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void HandleMessage(PlayerConnection player, string msg)
        {
            Console.WriteLine($"From {player.Id}: {msg}");

            string[] parts = msg.Split(' ');
            if (parts.Length == 0) return;

            switch (parts[0])
            {

                //This bunch of garbage shows how server and client talk 
                //Messaging from scratch 
                //Will show in report :D
                case "POS":
                    // POS x y z ry pitch
                    BroadcastExcept(player.Id, $"POS {player.Id} " + string.Join(" ", parts, 1, parts.Length - 1));
                    break;

                case "FLAG_PICKUP":
                    HandleFlagPickup(player, parts);
                    break;

                case "FLAG_DROP":
                    HandleFlagDrop(player, parts);
                    break;

                case "FLAG_CAPTURE":
                    HandleFlagCapture(player, parts);
                    break;

                case "RESET_MATCH":
                    HandleResetMatch(player);
                    break;

                case "HIT":
                    HandleHit(player, parts);
                    break;

                case "PLAYER_DEAD":
                    HandlePlayerDead(player, parts);
                    break;

                default:
                    Console.WriteLine("Unknown command: " + parts[0]);
                    break;
            }
        }

        private static void HandleHit(PlayerConnection player, string[] parts)
        {
            // HIT targetId damage shooterId
            //plz ignore, wanted fun FPS 
            if (parts.Length < 4) return;

            if (!int.TryParse(parts[1], out int targetId)) return;
            if (!int.TryParse(parts[2], out int damage)) return;
            if (!int.TryParse(parts[3], out int shooterId)) return;

            if (shooterId != player.Id)
            {
                Console.WriteLine($"Warning: shooterId mismatch in HIT from player {player.Id}");
            }

            // Broadcast so target client can apply damage
            BroadcastAll($"PLAYER_HIT {targetId} {damage} {shooterId}");
        }

        private static void HandlePlayerDead(PlayerConnection player, string[] parts)
        {
            // PLAYER_DEAD deadId killerId
            if (parts.Length < 3) return;

            if (!int.TryParse(parts[1], out int deadId)) return;
            if (!int.TryParse(parts[2], out int killerId)) return;

            Console.WriteLine($"Player {deadId} died to {killerId}");

            lock (_lock)
            {
                // When a player dies, if they were carrying ANY flag, return it home (NO scoring)
                //Removed coords, default to override check flaghome game object 
                foreach (var kv in _flags)
                {
                    FlagState f = kv.Value;

                    if (f.CarrierId == deadId)
                    {
                        Console.WriteLine($"Flag {f.FlagTeam} returned to base due to death of {deadId}");

                        // Remove carrier
                        f.CarrierId = -1;

                        // Mark flag as at home
                        f.AtBase = true;

                        // IMPORTANT:
                        // DO NOT set f.X,f.Y,f.Z = 0 here!!!!!!!!!!!!!
                        // Death return must NOT overwrite real home coordinates.
                        //I am losing my mind from this 

                        // Tell ALL clients that the flag is now at its base.
                        // Clients will move the flag to the correct Unity homeOverride automatically.
                        BroadcastAll($"FLAG_STATE {f.FlagTeam} AT_BASE -1 0 0 0");
                    }
                }
            }

            // Broadcast the death event to all clients
            BroadcastAll($"PLAYER_DEAD {deadId} {killerId}");
        }



        private static void HandleFlagPickup(PlayerConnection player, string[] parts)
        {
            // FLAG_PICKUP <flagTeam>
            if (parts.Length < 2) return;
            string flagTeamStr = parts[1];

            if (!Enum.TryParse(flagTeamStr, out Team flagTeam))
                return;

            lock (_lock)
            {
                FlagState flag = _flags[flagTeam];

                // Enemy picking up enemy flag
                if (player.Team != flag.FlagTeam)
                {
                    if (flag.CarrierId == -1) // not already carried
                    {
                        flag.CarrierId = player.Id;
                        flag.AtBase = false;
                        Console.WriteLine($"Player {player.Id} picked up {flag.FlagTeam} flag");

                        BroadcastAll($"FLAG_STATE {flag.FlagTeam} CARRIED {player.Id} 0 0 0");
                    }
                }
                else
                {
                    // Friendly touching own flag: if dropped, return to base
                    //Fix instead of drop at dead player coord 
                    if (!flag.AtBase && flag.CarrierId == -1)
                    {
                        flag.AtBase = true;
                        flag.X = flag.Y = flag.Z = 0;
                        Console.WriteLine($"Player {player.Id} returned {flag.FlagTeam} flag to base");

                        BroadcastAll($"FLAG_STATE {flag.FlagTeam} AT_BASE -1");
                    }
                }
            }
        }

        private static void HandleFlagDrop(PlayerConnection player, string[] parts)
        {
            // FLAG_DROP <flagTeam> x y z
            if (parts.Length < 5) return;
            if (!Enum.TryParse(parts[1], out Team flagTeam)) return;

            if (!float.TryParse(parts[2], out float x)) return;
            if (!float.TryParse(parts[3], out float y)) return;
            if (!float.TryParse(parts[4], out float z)) return;

            lock (_lock)
            {
                FlagState flag = _flags[flagTeam];

                // Only drop if player actually has it
                if (flag.CarrierId == player.Id)
                {
                    flag.CarrierId = -1;

                    // Don';t think this works 100% of the time but seems fine 
                    flag.AtBase = false;

                    flag.X = x;
                    flag.Y = y;
                    flag.Z = z;

                    Console.WriteLine($"Player {player.Id} dropped {flag.FlagTeam} flag at {x},{y},{z}");

                    BroadcastAll($"FLAG_STATE {flag.FlagTeam} DROPPED -1 {x} {y} {z}");
                }
            }
        }


        private static void HandleFlagCapture(PlayerConnection player, string[] parts)
        {
            // FLAG_CAPTURE <flagTeam>

            //Tick player team score
            //Make sure flag goes back to base 
            if (parts.Length < 2) return;
            string flagTeamStr = parts[1];

            if (!Enum.TryParse(flagTeamStr, out Team flagTeam))
                return;

            lock (_lock)
            {
                FlagState flag = _flags[flagTeam];

                // Must be enemy flag & must be currently carried by this player
                if (player.Team == flag.FlagTeam) return;
                if (flag.CarrierId != player.Id) return;

                // Score for player's team
                if (player.Team == Team.Red)
                    _redScore++;
                else
                    _blueScore++;

                Console.WriteLine($"Player {player.Id} captured {flag.FlagTeam} flag. Score: Red={_redScore} Blue={_blueScore}");

                // Flag back to base haha
                flag.CarrierId = -1;
                flag.AtBase = true;
                //Optional overrrride grabs from game object POS? 
                flag.X = flag.Y = flag.Z = 0;

                // Broadcast new score andd flag state
                BroadcastAll($"SCORE {_redScore} {_blueScore}");
                BroadcastAll($"FLAG_STATE {flag.FlagTeam} AT_BASE -1");

                // Optional win check
                if (_redScore >= _scoreToWin)
                {
                    BroadcastAll("GAME_OVER Red");
                }
                else if (_blueScore >= _scoreToWin)
                {
                    BroadcastAll("GAME_OVER Blue");
                }
            }
        }

        private static void HandleResetMatch(PlayerConnection player)
        {
            //Used to reset game after team wins 
            lock (_lock)
            {
                Console.WriteLine($"Player {player.Id} requested match reset.");

                // Reset scores
                _redScore = 0;
                _blueScore = 0;

                // Reset flags to base
                foreach (var kv in _flags)
                {
                    var f = kv.Value;
                    f.CarrierId = -1;
                    f.AtBase = true;
                    f.X = f.Y = f.Z = 0;
                }

                // Broadcast new score + flags + MATCH_RESET
                BroadcastAll($"SCORE {_redScore} {_blueScore}");

                foreach (var kv in _flags)
                {
                    var f = kv.Value;
                    BroadcastAll($"FLAG_STATE {f.FlagTeam} AT_BASE -1"); // keep format
                    //Not sure why -1 0 0 0 not working? This seems chill 
                    f.X = 0;
                    f.Y = 0;
                    f.Z = 0;

                }

                BroadcastAll("MATCH_RESET");
            }
        }

        private static void BroadcastAll(string msg)
        {
            Console.WriteLine("Broadcast: " + msg);
            foreach (var kv in _players)
            {
                try
                {
                    kv.Value.Writer.WriteLine(msg);
                }
                catch
                {
                    // Why this throwing errors whatttttt
                }
            }
        }

        private static void BroadcastExcept(int exceptId, string msg)
        {
            Console.WriteLine("BroadcastExcept: " + msg);
            foreach (var kv in _players)
            {
                if (kv.Key == exceptId) continue;
                try
                {
                    kv.Value.Writer.WriteLine(msg);
                }
                catch
                {
                    // AHHHHHHHHHHHHHHHHH
                }
            }
        }
    }
}
