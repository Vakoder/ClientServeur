using System;
using System.Net.Sockets;
using System.Text;

public class Client
{
    private string message = "";
    
    public void SendMessage()
    {

        TcpClient client = new TcpClient("127.0.0.1", 5001);
        NetworkStream flux = client.GetStream();

        while (true)
        {
            Console.Write("> ");
            message = Console.ReadLine() ?? "";
            if (message.ToLower() == "exit")
                break;

            byte[] data = Encoding.UTF8.GetBytes(message);
            flux.Write(data, 0, data.Length);

            Console.WriteLine("Message envoyé !");
        }
    }
}