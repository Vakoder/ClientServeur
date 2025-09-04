using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{
    private string message = "";
    private int ClientId = 0;
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
            id = Interlocked.Increment(ref ClientId);
            Console.WriteLine($"Client {id} connecté.");


            Task.Run(() => HandleClient(id, client));
        }
    }

    private void HandleClient(int clientId, TcpClient client)
    {
        
        NetworkStream flux = client.GetStream();
        
        byte[] buffer = new byte[1024];
        try
        {
            while (true)
            {
                bytesLus = flux.Read(buffer, 0, buffer.Length);
                if (bytesLus == 0) 
                    break;

                message = Encoding.UTF8.GetString(buffer, 0, bytesLus);
                Console.WriteLine($"Client {clientId} : {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur avec le client {clientId}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"Client {clientId} déconnecté.");
        }
    }
}

