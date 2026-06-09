using ChatServer;
using ChatShared;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200 * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatStore>();

var app = builder.Build();
var store = app.Services.GetRequiredService<ChatStore>();
await store.InitializeAsync();

var uploadRoot = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(uploadRoot);

app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "wwwroot")),
    RequestPath = ""
});

app.MapGet("/", () => "Lab03 Zalo-style SignalR chat server is running.");

app.MapGet("/api/history", async (ChatStore chatStore) =>
{
    var messages = await chatStore.GetRecentMessagesAsync();
    return Results.Ok(messages);
});

app.MapPost("/api/uploads", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Request must be multipart/form-data.");
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("Missing upload file.");
    }

    var safeName = Path.GetFileName(file.FileName);
    var storedName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
    var filePath = Path.Combine(uploadRoot, storedName);

    await using (var stream = File.Create(filePath))
    {
        await file.CopyToAsync(stream);
    }

    var attachment = new AttachmentDto
    {
        FileName = safeName,
        StoredFileName = storedName,
        ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
        Size = file.Length,
        Url = $"/uploads/{Uri.EscapeDataString(storedName)}",
        Kind = DetectKind(safeName, file.ContentType)
    };

    return Results.Ok(new UploadResponseDto { Attachment = attachment });
});

app.MapHub<ChatHub>("/chatHub");

app.Run();

static AttachmentKind DetectKind(string fileName, string contentType)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();

    if (extension == ".gif")
    {
        return AttachmentKind.Gif;
    }

    if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp")
    {
        return AttachmentKind.Image;
    }

    if (extension is ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm")
    {
        return AttachmentKind.Video;
    }

    if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        return AttachmentKind.Image;
    }

    if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
    {
        return AttachmentKind.Video;
    }

    return AttachmentKind.File;
}

namespace ChatServer
{
    public sealed class ChatHub(ChatStore store) : Hub
    {
        public async Task Join(string username)
        {
            username = NormalizeUsername(username);
            Context.Items["username"] = username;

            var history = await store.GetRecentMessagesAsync();
            await Clients.Caller.SendAsync("HistoryLoaded", history);

            var systemMessage = new ChatMessageDto
            {
                Sender = "System",
                Text = $"{username} joined the chat",
                SentAt = DateTimeOffset.UtcNow,
                Kind = MessageKind.Text
            };
            await Clients.Others.SendAsync("ReceiveMessage", systemMessage);
        }

        public async Task SendText(string clientMessageId, string text)
        {
            text = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var message = new ChatMessageDto
            {
                ClientMessageId = string.IsNullOrWhiteSpace(clientMessageId) ? Guid.NewGuid().ToString("N") : clientMessageId,
                Sender = GetUsername(),
                Text = text,
                SentAt = DateTimeOffset.UtcNow,
                Kind = MessageKind.Text
            };

            message = await store.SaveMessageAsync(message);
            await Clients.All.SendAsync("ReceiveMessage", message);
        }

        public async Task SendAttachment(string clientMessageId, string caption, AttachmentDto attachment)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.Url))
            {
                return;
            }

            var message = new ChatMessageDto
            {
                ClientMessageId = string.IsNullOrWhiteSpace(clientMessageId) ? Guid.NewGuid().ToString("N") : clientMessageId,
                Sender = GetUsername(),
                Text = caption?.Trim() ?? "",
                SentAt = DateTimeOffset.UtcNow,
                Kind = MessageKind.Attachment,
                Attachment = attachment
            };

            message = await store.SaveMessageAsync(message);
            await Clients.All.SendAsync("ReceiveMessage", message);
        }

        private string GetUsername()
        {
            return Context.Items.TryGetValue("username", out var username) && username is string name
                ? name
                : "Guest";
        }

        private static string NormalizeUsername(string username)
        {
            username = (username ?? "").Trim();
            return string.IsNullOrWhiteSpace(username) ? "Guest" : username;
        }
    }
}
