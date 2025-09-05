using System.Net.Sockets;
using System.Text;

public class Client
{
    private string message = "";
    private string incoming = "";
    private int bytesLus = 0;

    public void SendMessage()
    {
        try
        {
            TcpClient client = new TcpClient("127.0.0.1", 5001);
            NetworkStream flux = client.GetStream();

            Console.Write("Voulez-vous REGISTER ou AUTH ? (R/A) : ");
            var choice = (Console.ReadLine() ?? "").Trim().ToUpperInvariant();
            string cmd = choice == "R" ? "REGISTER" : "AUTH";

            Console.Write("Username: ");
            var username = (Console.ReadLine() ?? "").Trim();
            Console.Write("Password: ");
            var password = (Console.ReadLine() ?? "").Trim();

            var authLine = $"{cmd} {username} {password}\n";
            var authData = Encoding.UTF8.GetBytes(authLine);
            flux.Write(authData, 0, authData.Length);
            flux.Flush();

            var resp = ReadLineAsync(flux).GetAwaiter().GetResult();
            if (resp == null)
            {
                Console.WriteLine("Connexion fermée par le serveur.");
                client.Close();
                return;
            }

            Console.WriteLine(resp.TrimEnd('\r', '\n'));
            if (!resp.StartsWith("OK"))
            {
                Console.WriteLine("Authentification/inscription refusée, fermeture.");
                client.Close();
                return;
            }

            Task.Run(() => ReadLoopAsync(flux, client));

            while (client.Connected)
            {
                Console.Write("> Entrez votre message : ");
                message = Console.ReadLine() ?? "";
                if (message.ToLower() == "exit")
                    break;

                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                    flux.Write(data, 0, data.Length);
                    flux.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur envoi: {ex.Message}");
                    break;
                }
            }

            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Impossible de se connecter: {ex.Message}");
        }
    }

    private async Task<string?> ReadLineAsync(NetworkStream flux)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        try
        {
            while (true)
            {
                int read = await flux.ReadAsync(buffer, 0, 1);
                if (read == 0) return null;
                char c = (char)buffer[0];
                if (c == '\n') break;
                if (c != '\r') sb.Append(c);
            }
            return sb.ToString();
        }
        catch
        {
            return null;
        }
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