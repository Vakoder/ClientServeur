using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;


public class Server
{
    private int clientCounter = 0;
    private readonly ConcurrentDictionary<int, TcpClient> clients = new();
    private int id = 0;
    private int bytesLus = 0;
    private SqliteService? _db; 

    public void Start()
    {
        var dbPath = Environment.GetEnvironmentVariable("CHAT_DB") ?? "chat.db";
        _db = new SqliteService(dbPath);

        TcpListener serveur = new TcpListener(IPAddress.Any, 5001);
        serveur.Start();
        Console.WriteLine("Serveur en attente...");

        while (true)
        {
            Console.WriteLine("En attente de connexion...");
            TcpClient client = serveur.AcceptTcpClient();
            id = Interlocked.Increment(ref clientCounter);
            clients[id] = client;
            Console.WriteLine($"Client {id} connecté.");

            Task.Run(() => HandleClient(id, client));
        }
    }

    private async Task<string?> ReadLineAsync(NetworkStream stream)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, 0, 1);
            if (read == 0) return null;
            char c = (char)buffer[0];
            if (c == '\n') break;
            if (c != '\r') sb.Append(c);
        }
        return sb.ToString();
    }

    private async Task WriteAsync(NetworkStream stream, string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(data, 0, data.Length);
        await stream.FlushAsync();
    }

    private void HandleClient(int clientId, TcpClient client)
    {
        Task.Run(async () =>
        {
            int? userId = null;
            string? username = null;

            try
            {
                using (client)
                using (NetworkStream flux = client.GetStream())
                {
                    

                    var line = await ReadLineAsync(flux);
                    if (line == null)
                        return;

                    var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                    {
                        await WriteAsync(flux, "ERR Invalid command\n");
                        return;
                    }

                    var cmd = parts[0].ToUpperInvariant();
                    var user = parts[1];
                    var pass = parts[2];

                    if (_db == null)
                    {
                        await WriteAsync(flux, "ERR server db unavailable\n");
                        return;
                    }

                    if (cmd == "REGISTER")
                    {
                        var newId = await _db.RegisterUserAsync(user, pass);
                        if (newId == null)
                        {
                            await WriteAsync(flux, "ERR Username exists or invalid\n");
                            return;
                        }
                        userId = newId;
                        username = user;
                        await WriteAsync(flux, "OK Registered\n");
                    }
                    else if (cmd == "AUTH")
                    {
                        var authId = await _db.AuthenticateUserAsync(user, pass);
                        if (authId == null)
                        {
                            await WriteAsync(flux, "ERR Authentication failed\n");
                            return;
                        }
                        userId = authId;
                        username = user;
                        await WriteAsync(flux, "OK Authenticated\n");

                        var history = await _db.GetMessagesForUserAsync(userId.Value, 200);
                        foreach (var m in history)
                            await WriteAsync(flux, $"HIST {m.Timestamp} {m.Direction} {m.Content}\n");
                    }
                    else
                    {
                        await WriteAsync(flux, "ERR Unknown command\n");
                        return;
                    }

                    byte[] buffer = new byte[1024];
                    while (client.Connected)
                    {
                        bytesLus = await flux.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesLus == 0) break;

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesLus).TrimEnd('\r', '\n');
                        Console.WriteLine($"Client {clientId} ({username}): {message}");

                        var copy = message;
                        _ = Task.Run(async () =>
                        {
                            if (_db != null)
                                await _db.InsertMessageAsync(copy, $"FromUser{userId}", userId);
                        });

                        BroadcastMessage(clientId, username ?? $"User{clientId}", message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur avec le client {clientId}: {ex.Message}");
            }
            finally
            {
                clients.TryRemove(clientId, out _);
                Console.WriteLine($"Client {clientId} déconnecté.");
            }
        });
    }

    private void BroadcastMessage(int fromClientId, string fromUsername, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes($"{fromUsername}: {message}\n");

        foreach (var kv in clients.ToArray())
        {
            var idLocal = kv.Key;
            TcpClient tcp = kv.Value;

            if (idLocal == fromClientId)
                continue;

            if (!tcp.Connected)
            {
                clients.TryRemove(idLocal, out _);
                continue;
            }

            try
            {
                NetworkStream stream = tcp.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch
            {
                clients.TryRemove(idLocal, out _);
            }
        }
    }
}

