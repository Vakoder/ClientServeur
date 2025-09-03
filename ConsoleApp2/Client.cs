using System;
using System.Net.Sockets;
using System.Text;

public class Client
{
    public void SendPing()
    {
        using TcpClient client = new TcpClient("127.0.0.1", 5001);
        using NetworkStream flux = client.GetStream();

        Console.Write("Tape ton message : ");
        string message = Console.ReadLine() ?? "";

        byte[] data = Encoding.UTF8.GetBytes(message);
        flux.Write(data, 0, data.Length);

        Console.WriteLine("Message envoyé !");
    }
}