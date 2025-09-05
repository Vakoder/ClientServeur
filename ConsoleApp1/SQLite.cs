using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

public record MessageRecord(int Id, int? UserId, string Content, string Direction, string Timestamp);

public class SqliteService
{
    private readonly string _connectionString;

    public SqliteService(string dbPath = "chat.db")
    {
        _connectionString = $"Data Source={dbPath}";
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Salt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    Content TEXT NOT NULL,
                    Direction TEXT,
                    Timestamp TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Messages');";
            using var rdr = cmd.ExecuteReader();
            bool hasUserId = false;
            while (rdr.Read())
            {
                var colName = rdr.GetString(1); 
                if (string.Equals(colName, "UserId", StringComparison.OrdinalIgnoreCase))
                {
                    hasUserId = true;
                    break;
                }
            }

            if (!hasUserId)
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Messages ADD COLUMN UserId INTEGER;";
                alter.ExecuteNonQuery();
            }
        }
    }

    public async Task<int?> RegisterUserAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        
        await using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT Id FROM Users WHERE Username = @u;";
            check.Parameters.AddWithValue("@u", username);
            var res = await check.ExecuteScalarAsync();
            if (res != null) return null;
        }

        var salt = GenerateSalt();
        var hash = HashPassword(password, salt);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO Users (Username, PasswordHash, Salt, CreatedAt) VALUES (@u, @h, @s, @t); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@s", salt);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow.ToString("o"));
            var idObj = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(idObj);
        }
    }

    public async Task<int?> AuthenticateUserAsync(string username, string password)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, PasswordHash, Salt FROM Users WHERE Username = @u;";
            cmd.Parameters.AddWithValue("@u", username);
            await using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;
            var id = rdr.GetInt32(0);
            var storedHash = rdr.GetString(1);
            var salt = rdr.GetString(2);
            if (VerifyPassword(password, salt, storedHash))
                return id;
            return null;
        }
    }

    public async Task<string?> GetUsernameByIdAsync(int userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Username FROM Users WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", userId);
        var res = await cmd.ExecuteScalarAsync();
        return res?.ToString();
    }

    public async Task InsertMessageAsync(string content, string direction, int? userId = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Messages (UserId, Content, Direction, Timestamp) VALUES (@uid, @content, @direction, @ts);";
        cmd.Parameters.AddWithValue("@uid", userId.HasValue ? (object)userId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@content", content ?? "");
        cmd.Parameters.AddWithValue("@direction", direction ?? "");
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex)
        {
            Console.WriteLine($"SQLite insert error: {ex.Message}");
        }
    }

    public async Task<List<MessageRecord>> GetMessagesForUserAsync(int userId, int limit = 200)
    {
        var list = new List<MessageRecord>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, UserId, Content, Direction, Timestamp FROM Messages WHERE UserId = @uid ORDER BY Id DESC LIMIT @lim;";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@lim", limit);

        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new MessageRecord(
                rdr.GetInt32(0),
                rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                rdr.GetString(2),
                rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                rdr.GetString(4)
            ));
        }

        list.Reverse();
        return list;
    }

    private static string GenerateSalt(int size = 16)
    {
        var bytes = new byte[size];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var derive = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var hash = derive.GetBytes(32);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string salt, string expectedHash)
    {
        var hash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hash), Convert.FromBase64String(expectedHash));
    }
}