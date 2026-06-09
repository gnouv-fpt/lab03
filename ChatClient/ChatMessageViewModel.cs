using ChatShared;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ChatClient;

public sealed class ChatMessageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public long Id { get; init; }
    public string ClientMessageId { get; init; } = "";
    public string Sender { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTimeOffset SentAt { get; init; }
    public MessageKind Kind { get; init; }
    public AttachmentDto? Attachment { get; init; }
    public bool IsOwn { get; init; }
    public bool IsPending { get; init; }
    public string ServerBaseUrl { get; init; } = "";

    public string TimeText => SentAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
    public string SenderLine => IsOwn ? $"Bạn - {TimeText}" : $"{Sender} - {TimeText}";
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public bool HasAttachment => Attachment is not null;
    public bool IsImageAttachment => Attachment?.Kind is AttachmentKind.Image or AttachmentKind.Gif;
    public bool IsVideoAttachment => Attachment?.Kind == AttachmentKind.Video;
    public bool IsFileAttachment => Attachment is not null && !IsImageAttachment && !IsVideoAttachment;
    public string AttachmentName => Attachment?.FileName ?? "";
    public string AttachmentSize => Attachment is null ? "" : FormatSize(Attachment.Size);
    public string FullAttachmentUrl => Attachment is null ? "" : BuildUrl(ServerBaseUrl, Attachment.Url);
    public Uri? FullAttachmentUri => Uri.TryCreate(FullAttachmentUrl, UriKind.Absolute, out var uri) ? uri : null;

    public BitmapImage? PreviewImage
    {
        get
        {
            if (!IsImageAttachment || string.IsNullOrWhiteSpace(FullAttachmentUrl))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(FullAttachmentUrl);
                image.EndInit();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }

    public static ChatMessageViewModel FromDto(ChatMessageDto dto, string currentUser, string serverBaseUrl)
    {
        return new ChatMessageViewModel
        {
            Id = dto.Id,
            ClientMessageId = dto.ClientMessageId,
            Sender = dto.Sender,
            Text = dto.Text,
            SentAt = dto.SentAt,
            Kind = dto.Kind,
            Attachment = dto.Attachment,
            IsOwn = string.Equals(dto.Sender, currentUser, StringComparison.OrdinalIgnoreCase),
            ServerBaseUrl = serverBaseUrl
        };
    }

    public static ChatMessageViewModel PendingUpload(string fileName, string currentUser, string serverBaseUrl)
    {
        return new ChatMessageViewModel
        {
            ClientMessageId = Guid.NewGuid().ToString("N"),
            Sender = currentUser,
            Text = $"Đang gửi {fileName}...",
            SentAt = DateTimeOffset.Now,
            Kind = MessageKind.Text,
            IsOwn = true,
            IsPending = true,
            ServerBaseUrl = serverBaseUrl
        };
    }

    private static string BuildUrl(string baseUrl, string relativeOrAbsoluteUrl)
    {
        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return $"{baseUrl.TrimEnd('/')}/{relativeOrAbsoluteUrl.TrimStart('/')}";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
