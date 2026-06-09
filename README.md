# Lab03 Chat App

Desktop chat app built with WPF on the client and ASP.NET Core SignalR on the server.

## Projects

- `ChatClient`: WPF desktop app for chat UI, emoji, image/file/video messages.
- `ChatServer`: ASP.NET Core server for SignalR, uploads, static files, and chat history API.
- `ChatShared`: shared DTOs used by both client and server.

## Port And URLs

- SignalR server: `http://localhost:5050`
- SignalR hub endpoint: `http://localhost:5050/chatHub`
- Chat history API: `http://localhost:5050/api/history`
- Upload API: `http://localhost:5050/api/uploads`

The WPF client is currently hardcoded to connect to `http://localhost:5050`.

## How To Run

Open the solution folder:

```powershell
cd E:\FPTU\2026SUMMER\PRN222\lab03
```

Start the server first:

```powershell
dotnet run --project .\ChatServer\ChatServer.csproj
```

You should see the server listening on port `5050`.

Then start one or more chat clients in separate terminals:

```powershell
dotnet run --project .\ChatClient\ChatClient.csproj
```

## Quick Test Flow

1. Start `ChatServer`.
2. Start `ChatClient`.
3. Enter a display name, then click `Ket noi`.
4. Open a second `ChatClient` instance with another name.
5. Send a text message between the two windows.
6. Test emoji insertion in the message box.
7. Test attachments:

- image: click the image to preview full size, click `Tai ve` to download
- video: preview inside chat if supported, or click `Xem` to open externally
- file: click `Tai ve` to download

## Build

Build the whole solution:

```powershell
dotnet build .\Lab03ChatApp.sln
```

If build fails because `.exe` or `.dll` files are locked, close running `ChatClient` and `ChatServer` processes first, then build again.

## Features

- Realtime chat with SignalR
- Display name chosen before connecting
- Left/right chat bubble layout
- Send text, emoji, images, videos, and files
- Image full-screen preview inside the app
- Download attachments from chat
- Video preview inside the app when supported
- SQLite chat history loaded on connect

## Runtime Data

- SQLite history database: `ChatServer\App_Data\chat-history.db`
- Uploaded files: `ChatServer\wwwroot\uploads`

Both runtime folders are ignored by `.gitignore`.
