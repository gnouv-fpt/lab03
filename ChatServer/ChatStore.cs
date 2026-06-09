using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer;

public sealed class ChatStore(IWebHostEnvironment environment)
{
    private readonly string _dbPath = Path.Combine(environment.ContentRootPath, "App_Data", "chat-history.db");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ClientMessageId TEXT NOT NULL,
                Sender TEXT NOT NULL,
                Text TEXT NOT NULL,
                SentAt TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                FileName TEXT NULL,
                StoredFileName TEXT NULL,
                ContentType TEXT NULL,
                Size INTEGER NULL,
                Url TEXT NULL,
                AttachmentKind INTEGER NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetRecentMessagesAsync(int take = 100)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT *
            FROM Messages
            ORDER BY Id DESC
            LIMIT $take;
            """;
        command.Parameters.AddWithValue("$take", take);

        var messages = new List<ChatMessageDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(ReadMessage(reader));
        }

        messages.Reverse();
        return messages;
    }

    public async Task<ChatMessageDto> SaveMessageAsync(ChatMessageDto message)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Messages (
                ClientMessageId,
                Sender,
                Text,
                SentAt,
                Kind,
                FileName,
                StoredFileName,
                ContentType,
                Size,
                Url,
                AttachmentKind
            )
            VALUES (
                $clientMessageId,
                $sender,
                $text,
                $sentAt,
                $kind,
                $fileName,
                $storedFileName,
                $contentType,
                $size,
                $url,
                $attachmentKind
            );
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$clientMessageId", message.ClientMessageId);
        command.Parameters.AddWithValue("$sender", message.Sender);
        command.Parameters.AddWithValue("$text", message.Text);
        command.Parameters.AddWithValue("$sentAt", message.SentAt.ToString("O"));
        command.Parameters.AddWithValue("$kind", (int)message.Kind);
        command.Parameters.AddWithValue("$fileName", (object?)message.Attachment?.FileName ?? DBNull.Value);
        command.Parameters.AddWithValue("$storedFileName", (object?)message.Attachment?.StoredFileName ?? DBNull.Value);
        command.Parameters.AddWithValue("$contentType", (object?)message.Attachment?.ContentType ?? DBNull.Value);
        command.Parameters.AddWithValue("$size", (object?)message.Attachment?.Size ?? DBNull.Value);
        command.Parameters.AddWithValue("$url", (object?)message.Attachment?.Url ?? DBNull.Value);
        command.Parameters.AddWithValue("$attachmentKind", message.Attachment is null ? DBNull.Value : (int)message.Attachment.Kind);

        message.Id = Convert.ToInt64(await command.ExecuteScalarAsync());
        return message;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }

    private static ChatMessageDto ReadMessage(SqliteDataReader reader)
    {
        var kind = (MessageKind)reader.GetInt32(reader.GetOrdinal("Kind"));
        AttachmentDto? attachment = null;

        if (kind == MessageKind.Attachment)
        {
            attachment = new AttachmentDto
            {
                FileName = reader.GetNullableString("FileName"),
                StoredFileName = reader.GetNullableString("StoredFileName"),
                ContentType = reader.GetNullableString("ContentType"),
                Size = reader.GetNullableInt64("Size"),
                Url = reader.GetNullableString("Url"),
                Kind = (AttachmentKind)reader.GetNullableInt32("AttachmentKind")
            };
        }

        return new ChatMessageDto
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            ClientMessageId = reader.GetString(reader.GetOrdinal("ClientMessageId")),
            Sender = reader.GetString(reader.GetOrdinal("Sender")),
            Text = reader.GetString(reader.GetOrdinal("Text")),
            SentAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("SentAt"))),
            Kind = kind,
            Attachment = attachment
        };
    }
}

internal static class SqliteReaderExtensions
{
    public static string GetNullableString(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
    }

    public static long GetNullableInt64(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0L : reader.GetInt64(ordinal);
    }

    public static int GetNullableInt32(this SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

}
