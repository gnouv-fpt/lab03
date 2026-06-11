# Lab03 Chat App

Web chat app built with ASP.NET Core MVC on the client and ASP.NET Core SignalR on the server.

## Projects

- `ChatClient`: ASP.NET Core MVC web client for the chat UI, emoji picker, and attachment workflow.
- `ChatServer`: ASP.NET Core server for SignalR, uploads, static files, and chat history API.
- `ChatShared`: shared DTOs used by the server storage layer.

## Port And URLs

- SignalR server: `http://localhost:5050`
- SignalR hub endpoint: `http://localhost:5050/chatHub`
- Chat history API: `http://localhost:5050/api/history`
- Upload API: `http://localhost:5050/api/uploads`
- MVC client: `http://localhost:5066`

For LAN use, the browser client should connect to the host machine IP, for example `http://192.168.1.6:5050`.

## How To Run

Open the solution folder:

```powershell
cd E:\FPTU\2026SUMMER\PRN222\lab03
```

Start the SignalR server first:

```powershell
dotnet run --project .\ChatServer\ChatServer.csproj
```

Then start the MVC client:

```powershell
dotnet run --project .\ChatClient\ChatClient.csproj
```

Open the browser at:

```text
http://localhost:5066
```

## Quick Test Flow

1. Start `ChatServer`.
2. Start `ChatClient`.
3. Open the browser on one or more machines in the same network.
4. Enter the host server IP and a display name, then click `Ket noi`.
5. Send a text message with `Enter`.
6. Use `Shift+Enter` for a new line.
7. Test emoji insertion from the emoji picker.
8. Test file, image, and video uploads:
   - image: preview in chat, click to open larger, or download
   - video: preview in chat, open in new tab, or download
   - file: download directly

## Build

Build the whole solution:

```powershell
dotnet build .\ChatClient.sln
```

## Features

- Realtime chat with SignalR
- ASP.NET Core MVC web client
- Display name chosen before connecting
- Left/right chat bubble layout
- Send text with `Enter`
- New line with `Shift+Enter`
- Emoji picker
- Async file, image, and video uploads
- Image preview and download
- Video preview and download
- SQLite chat history loaded on connect

## Runtime Data

- SQLite history database: `ChatServer\App_Data\chat-history.db`
- Uploaded files: `ChatServer\wwwroot\uploads`

Both runtime folders are ignored by `.gitignore`.
