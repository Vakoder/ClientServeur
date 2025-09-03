using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{
    private string message = "";
    public void Start()
    {
        TcpListener serveur = new TcpListener(IPAddress.Any, 5001);
        serveur.Start();
        Console.WriteLine("Serveur en attente...");

        TcpClient client = serveur.AcceptTcpClient();
        NetworkStream flux = client.GetStream();

        byte[] buffer = new byte[1024];
        while (true)
        {
            int bytesLus = flux.Read(buffer, 0, buffer.Length);
            if (bytesLus == 0)
            {
                break;
            }
            message = Encoding.UTF8.GetString(buffer, 0, bytesLus);
            Console.WriteLine("Reçu : " + message);
        }

        serveur.Stop();
        Console.WriteLine("Serveur arrêté.");
    }
}