using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class Server
{
    public void Start()
    {
        TcpListener serveur = new TcpListener(IPAddress.Any, 5001);
        serveur.Start();
        Console.WriteLine("Serveur en attente...");

        using TcpClient client = serveur.AcceptTcpClient();
        using NetworkStream flux = client.GetStream();

        byte[] buffer = new byte[1024];
        int bytesLus = flux.Read(buffer, 0, buffer.Length);
        if (bytesLus > 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesLus);
            Console.WriteLine("Reçu : " + message);
        }

        Console.WriteLine("Appuie sur une touche pour arrêter le serveur...");

        Console.ReadKey(true);
        serveur.Stop();
        Console.WriteLine("Serveur arrêté.");
    }
}