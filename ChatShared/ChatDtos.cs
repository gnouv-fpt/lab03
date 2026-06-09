namespace ChatShared;

public enum MessageKind
{
    Text,
    Attachment
}

public enum AttachmentKind
{
    File,
    Image,
    Video,
    Gif
}

public sealed class AttachmentDto
{
    public string FileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public string Url { get; set; } = "";
    public AttachmentKind Kind { get; set; } = AttachmentKind.File;
}

public sealed class ChatMessageDto
{
    public long Id { get; set; }
    public string ClientMessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public MessageKind Kind { get; set; } = MessageKind.Text;
    public AttachmentDto? Attachment { get; set; }
}

public sealed class UploadResponseDto
{
    public AttachmentDto Attachment { get; set; } = new();
}
