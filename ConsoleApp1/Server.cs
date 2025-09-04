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


    public void Start()
    {
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

    private void HandleClient(int clientId, TcpClient client)
    {
        try
        {
            using (client)
            using (NetworkStream flux = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                while (true)
                {
                    bytesLus = flux.Read(buffer, 0, buffer.Length);
                    if (bytesLus == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesLus).TrimEnd('\r', '\n');
                    Console.WriteLine($"Client {clientId} : {message}");

                    BroadcastMessage(clientId, message);
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
    }

    private void BroadcastMessage(int fromClientId, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes($"Client {fromClientId}: {message}\n");

        foreach (var kv in clients.ToArray())
        {
            id = kv.Key;
            TcpClient tcp = kv.Value;

            if (id == fromClientId)
                continue;

            if (!tcp.Connected)
            {
                clients.TryRemove(id, out _);
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
                clients.TryRemove(id, out _);
            }
        }
    }
}

