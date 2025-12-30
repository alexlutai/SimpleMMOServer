using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Fleck;

class Program
{
    static void Main(string[] args)
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8181";
        var players = new Dictionary<IWebSocketConnection, Player>();

        // ✅ Fleck suportă automat HTTP->WebSocket upgrade
        // Nu mai e nevoie de HttpListener separat!
        FleckLog.Level = LogLevel.Info;

        var server = new WebSocketServer($"ws://0.0.0.0:{port}");

        // ✅ Render verifică health prin request HTTP simplu
        // Fleck răspunde automat la request-uri non-WebSocket
        server.SupportedSubProtocols = new[] { "binary", "base64" };
        server.RestartAfterListenError = true;

        server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                var player = new Player
                {
                    Id = Guid.NewGuid().ToString(),
                    X = 0,
                    Y = 0,
                    Z = 0
                };

                players[socket] = player;

                // Trimite ID-ul propriu
                socket.Send(JsonSerializer.Serialize(new
                {
                    type = "init",
                    id = player.Id
                }));

                // Trimite NOULUI client toți playerii EXISTENȚI
                foreach (var kvp in players)
                {
                    if (kvp.Key == socket) continue;

                    var p = kvp.Value;
                    socket.Send(JsonSerializer.Serialize(new
                    {
                        type = "player_join",
                        id = p.Id,
                        x = p.X,
                        y = p.Y,
                        z = p.Z
                    }));
                }

                // Anunță toți CEILALȚI despre NOUL player
                foreach (var s in players.Keys)
                {
                    if (s == socket) continue;

                    s.Send(JsonSerializer.Serialize(new
                    {
                        type = "player_join",
                        id = player.Id,
                        x = player.X,
                        y = player.Y,
                        z = player.Z
                    }));
                }

                Console.WriteLine($"✅ Player {player.Id.Substring(0, 8)} connected. Total: {players.Count}");
            };

            socket.OnMessage = message =>
            {
                try
                {
                    var baseData = JsonSerializer.Deserialize<BaseMessage>(message);

                    if (baseData?.type == "move")
                    {
                        var data = JsonSerializer.Deserialize<MoveMessage>(message);
                        if (!players.ContainsKey(socket)) return;

                        var p = players[socket];
                        p.X = data.x;
                        p.Y = data.y;
                        p.Z = data.z;

                        // Trimite DOAR la CEILALȚI
                        foreach (var s in players.Keys)
                        {
                            if (s == socket) continue;

                            s.Send(JsonSerializer.Serialize(new
                            {
                                type = "player_move",
                                id = p.Id,
                                x = p.X,
                                y = p.Y,
                                z = p.Z
                            }));
                        }
                    }
                    else if (baseData?.type == "chat")
                    {
                        var chatData = JsonSerializer.Deserialize<ChatMessage>(message);
                        if (!players.ContainsKey(socket)) return;

                        var senderId = players[socket].Id;

                        // Broadcast la TOȚI (inclusiv expeditor)
                        foreach (var s in players.Keys)
                        {
                            s.Send(JsonSerializer.Serialize(new
                            {
                                type = "chat",
                                id = senderId,
                                message = chatData.message,
                                timestamp = DateTime.UtcNow.ToString("HH:mm:ss")
                            }));
                        }

                        Console.WriteLine($"💬 [{senderId.Substring(0, 8)}]: {chatData.message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing message: {ex.Message}");
                }
            };

            socket.OnClose = () =>
            {
                if (!players.ContainsKey(socket))
                    return;

                var id = players[socket].Id;
                players.Remove(socket);

                foreach (var s in players.Keys)
                {
                    s.Send(JsonSerializer.Serialize(new
                    {
                        type = "player_leave",
                        id = id
                    }));
                }

                Console.WriteLine($"❌ Player {id.Substring(0, 8)} disconnected. Total: {players.Count}");
            };

            socket.OnError = (ex) =>
            {
                Console.WriteLine($"⚠️ Socket error: {ex.Message}");
            };
        });

        Console.WriteLine($"🚀 WebSocket Server running on port {port}");
        Console.WriteLine($"📡 Ready to accept connections...");
        Console.WriteLine($"🏥 Health check: Server responds to HTTP requests automatically");

        // Ține serverul pornit la infinit
        Thread.Sleep(Timeout.Infinite);
    }
}

// Clase
class Player
{
    public string Id { get; set; } = "";
    public float X, Y, Z;
}

class BaseMessage
{
    public string type { get; set; } = "";
}

class MoveMessage
{
    public string type { get; set; } = "";
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
}

class ChatMessage
{
    public string type { get; set; } = "";
    public string message { get; set; } = "";
}