using System;
using System.Collections.Generic;
using System.Text.Json;
using Fleck;

var port = Environment.GetEnvironmentVariable("PORT") ?? "8181";
var players = new Dictionary<IWebSocketConnection, Player>();

var server = new WebSocketServer($"ws://0.0.0.0:{port}");

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

        // 1️⃣ trimite ID-ul propriu
        socket.Send(JsonSerializer.Serialize(new
        {
            type = "init",
            id = player.Id
        }));

        // 2️⃣ trimite NOULUI client toți playerii EXISTENȚI
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

        // 3️⃣ anunță toți CEILALȚI despre NOUL player
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

        Console.WriteLine($"Player {player.Id} connected");
    };

    socket.OnMessage = message =>
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
            
            Console.WriteLine($"Player {p.Id} moved to ({p.X}, {p.Y}, {p.Z})");
        }
        else if (baseData?.type == "chat")
        {
            // ✅ CHAT MESSAGE
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
                    timestamp = DateTime.Now.ToString("HH:mm:ss")
                }));
            }
            
            Console.WriteLine($"[CHAT] {senderId}: {chatData.message}");
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

        Console.WriteLine($"Player {id} disconnected");
    };
});

Console.WriteLine("Server running on port 8181");
Console.ReadLine();

// ✅ Clase
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