using ChatShared;
using Emoji.Wpf;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChatClient;

public partial class MainWindow : Window
{
    private const string ServerBaseUrl = "http://localhost:5050";

    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri(ServerBaseUrl) };
    private HubConnection? _connection;
    private string _currentUser = "Ban";

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Messages.CollectionChanged += Messages_CollectionChanged;
        SetConnectionStatus("Chua ket noi", "Nhap ten roi bam Ket noi");
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        _currentUser = string.IsNullOrWhiteSpace(UserNameBox.Text) ? "Ban" : UserNameBox.Text.Trim();
        UserNameBox.IsEnabled = false;
        ConnectButton.IsEnabled = false;
        SetConnectionStatus("Dang ket noi...", $"Dang vao phong chat voi ten {_currentUser}");

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = new HubConnectionBuilder()
                .WithUrl($"{ServerBaseUrl}/chatHub")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<IReadOnlyList<ChatMessageDto>>("HistoryLoaded", history =>
            {
                Dispatcher.Invoke(() =>
                {
                    Messages.Clear();
                    foreach (var message in history)
                    {
                        Messages.Add(ChatMessageViewModel.FromDto(message, _currentUser, ServerBaseUrl));
                    }
                });
            });

            _connection.On<ChatMessageDto>("ReceiveMessage", message =>
            {
                Dispatcher.Invoke(() =>
                {
                    Messages.Add(ChatMessageViewModel.FromDto(message, _currentUser, ServerBaseUrl));
                });
            });

            _connection.Reconnecting += error =>
            {
                Dispatcher.Invoke(() => SetConnectionStatus("Mat ket noi, dang thu lai...", error?.Message ?? "Dang co gang ket noi lai toi server"));
                return Task.CompletedTask;
            };

            _connection.Reconnected += _ =>
            {
                Dispatcher.Invoke(() => SetConnectionStatus("Da ket noi lai", $"Dang chat voi ten {_currentUser}"));
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    SetConnectionStatus("Da ngat ket noi", error?.Message ?? "Ban co the doi ten hoac bam Ket noi de vao lai");
                    UserNameBox.IsEnabled = true;
                    ConnectButton.IsEnabled = true;
                });
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            await _connection.InvokeAsync("Join", _currentUser);
            SetConnectionStatus($"Da ket noi voi ten {_currentUser}", "San sang gui tin nhan");
        }
        catch (Exception ex)
        {
            SetConnectionStatus("Khong ket noi duoc server", ex.Message);
            UserNameBox.IsEnabled = true;
            ConnectButton.IsEnabled = true;
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendTextAsync();
    }

    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            await SendTextAsync();
        }
    }

    private async Task SendTextAsync()
    {
        var text = MessageInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!IsConnected())
        {
            SetConnectionStatus("Chua ket noi server", "Nhap ten roi bam Ket noi");
            return;
        }

        MessageInput.Text = string.Empty;
        await _connection!.InvokeAsync("SendText", Guid.NewGuid().ToString("N"), text);
    }

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chon file, anh hoac video",
            Filter = "Media and files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4;*.mov;*.avi;*.mkv;*.pdf;*.docx;*.xlsx;*.txt;*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            _ = UploadAndSendAttachmentAsync(fileName);
        }

        await Task.CompletedTask;
    }

    private async Task UploadAndSendAttachmentAsync(string path)
    {
        if (!IsConnected())
        {
            SetConnectionStatus("Chua ket noi server", "Nhap ten roi bam Ket noi");
            return;
        }

        var pending = ChatMessageViewModel.PendingUpload(Path.GetFileName(path), _currentUser, ServerBaseUrl);
        Messages.Add(pending);

        try
        {
            await using var stream = File.OpenRead(path);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(DetectContentType(path));
            content.Add(fileContent, "file", Path.GetFileName(path));

            Dispatcher.Invoke(() => ShowUploadProgress());
            var response = await _httpClient.PostAsync("/api/uploads", content);
            response.EnsureSuccessStatusCode();

            var upload = await response.Content.ReadFromJsonAsync<UploadResponseDto>();
            if (upload?.Attachment is null)
            {
                throw new InvalidOperationException("Server did not return attachment metadata.");
            }

            Dispatcher.Invoke(() => Messages.Remove(pending));
            await _connection!.InvokeAsync("SendAttachment", Guid.NewGuid().ToString("N"), MessageInput.Text.Trim(), upload.Attachment);
            Dispatcher.Invoke(() => MessageInput.Text = string.Empty);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                Messages.Remove(pending);
                Messages.Add(new ChatMessageViewModel
                {
                    ClientMessageId = Guid.NewGuid().ToString("N"),
                    Sender = _currentUser,
                    Text = $"Gui file that bai: {Path.GetFileName(path)} ({ex.GetBaseException().Message})",
                    SentAt = DateTimeOffset.Now,
                    Kind = MessageKind.Text,
                    IsOwn = true,
                    ServerBaseUrl = ServerBaseUrl
                });
            });
        }
        finally
        {
            Dispatcher.Invoke(HideUploadProgress);
        }
    }

    private void EmojiPicker_Picked(object sender, EmojiPickedEventArgs e)
    {
        MessageInput.Focus();
        ((System.Windows.Documents.TextRange)MessageInput.Selection).Text = e.Emoji;
        MessageInput.CaretPosition = MessageInput.Selection.End;
    }

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetMessage(sender, out var message))
        {
            OpenUrl(message.FullAttachmentUrl);
        }
    }

    private void PreviewAttachmentImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Image { Source: not null } image)
        {
            PreviewFullImage.Source = image.Source;
            ImagePreviewOverlay.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private void ImagePreviewOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ImagePreviewOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClosePreviewButton_Click(object sender, RoutedEventArgs e)
    {
        ImagePreviewOverlay.Visibility = Visibility.Collapsed;
    }

    private async void DownloadAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetMessage(sender, out var message) || message.Attachment is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = message.Attachment.FileName,
            Filter = "All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await using var remote = await _httpClient.GetStreamAsync(message.FullAttachmentUri!);
            await using var local = File.Create(dialog.FileName);
            await remote.CopyToAsync(local);
            SetConnectionStatus($"Da tai {Path.GetFileName(dialog.FileName)}", $"Dang chat voi ten {_currentUser}");
        }
        catch (Exception ex)
        {
            SetConnectionStatus("Tai file that bai", ex.Message);
        }
    }

    private static bool TryGetMessage(object sender, out ChatMessageViewModel message)
    {
        message = null!;

        if (sender is FrameworkElement { Tag: ChatMessageViewModel viewModel })
        {
            message = viewModel;
            return !string.IsNullOrWhiteSpace(viewModel.FullAttachmentUrl);
        }

        return false;
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private bool IsConnected()
    {
        return _connection?.State == HubConnectionState.Connected;
    }

    private void SetConnectionStatus(string primary, string? secondary = null)
    {
        StatusText.Text = primary;
        ConnectionStateText.Text = string.IsNullOrWhiteSpace(secondary) ? primary : secondary;
    }

    private void ShowUploadProgress()
    {
        UploadProgressBar.IsIndeterminate = true;
        UploadProgressBar.Value = 0;
        UploadProgressText.Text = "Dang gui";
        UploadProgressBar.Visibility = Visibility.Visible;
        UploadProgressText.Visibility = Visibility.Visible;
    }

    private void UpdateUploadProgress(long uploaded, long total)
    {
        if (total <= 0)
        {
            UploadProgressText.Text = "...";
            return;
        }

        var percent = Math.Clamp((double)uploaded / total * 100, 0, 100);
        UploadProgressBar.Value = percent;
        UploadProgressText.Text = $"{percent:0}%";
    }

    private void HideUploadProgress()
    {
        UploadProgressBar.IsIndeterminate = false;
        UploadProgressBar.Visibility = Visibility.Collapsed;
        UploadProgressText.Visibility = Visibility.Collapsed;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Messages.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            MessagesList.ScrollIntoView(Messages[^1]);
        });
    }

    private void VideoPreview_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is MediaElement mediaElement)
        {
            mediaElement.Play();
        }
    }

    private void VideoPreview_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        SetConnectionStatus("Khong phat duoc video trong app", e.ErrorException?.Message ?? "Hay thu nut Xem de mo bang trinh phat ngoai");
    }

    private static string DetectContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _httpClient.Dispose();
    }
}
