using System.Net.Sockets;
using System.Text;


public class Client
{
    private string message = "";
    private string incoming = "";
    private int bytesLus = 0;

    public void SendMessage()
    {
        TcpClient client = new TcpClient("127.0.0.1", 5001);
        NetworkStream flux = client.GetStream();


        Task.Run(() => ReadLoopAsync(flux, client));

        while (true)
        {
            Console.Write("> Entrez votre message : ");
            message = Console.ReadLine() ?? "";
            if (message.ToLower() == "exit")
                break;

            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            flux.Write(data, 0, data.Length);
            flux.Flush();
        }

        client.Close();
    }

    private async Task ReadLoopAsync(NetworkStream flux, TcpClient client)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (client.Connected)
            {
                bytesLus = await flux.ReadAsync(buffer, 0, buffer.Length);
                if (bytesLus == 0) 
                    break;

                incoming = Encoding.UTF8.GetString(buffer, 0, bytesLus).TrimEnd('\r', '\n');
                Console.WriteLine();
                Console.WriteLine(incoming);
                Console.Write("> Entrez votre message : ");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lecture: {ex.Message}");
        }
    }
}