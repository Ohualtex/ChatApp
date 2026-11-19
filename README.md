# <img src="docs/icon.png" width="24" height="24" alt=""> ChatApp

A desktop messaging application with a C# Windows Forms client and an Elixir TCP server. Join a shared room or start a private conversation with another connected user.

## Technologies

- C# and .NET 10 with Windows Forms and `TcpClient`
- Elixir 1.19 and Erlang/OTP 28 with `:gen_tcp` and `GenServer`
- UTF-8, newline-delimited JSON over TCP

## Run locally

The client requires Windows and the .NET 10 SDK. The server requires Elixir 1.19 with Erlang/OTP 28. Both use standard libraries.

In a PowerShell terminal at the repository root, start the server:

```powershell
Set-Location server
mix run --no-halt
```

In a separate terminal at the repository root, launch the client:

```powershell
dotnet run --project ChatApp.Client/ChatApp.Client.csproj
```

Run the client command again in another terminal. Enter a different display name in each client and connect to `127.0.0.1` on port `5050`.

## Using the chat

- Select **General** in the left **Chats** drawer to talk to everyone in the room.
- Select a user to open a private conversation. The server forwards private messages to the sender and recipient.
- Press Enter or select **Send** to send your message. Your messages appear on the right, with incoming messages on the left and a local timestamp on each bubble.
- Each conversation keeps its own messages and draft during the current connection session. The chat list shows the latest message and an unread count. Opening a conversation clears its unread count.
- Select the conversation title in the top bar to open or close the drawer. You can also close it with **×** or Esc.
- If a participant disconnects, their existing conversation stays visible. Sending becomes available again when they reconnect with the same display name.
- Select **Disconnect** to leave. Reconnecting starts a fresh local session. The server console lists users as they join and leave.

## Run on a local network

On the server computer, set `CHAT_BIND` to its local IPv4 address before starting Mix. Replace the example address with the server computer's address.

```powershell
Set-Location server
$env:CHAT_BIND = "192.168.1.20"
mix run --no-halt
```

On another computer on the same network, enter that address and port `5050` in the client. The server computer must allow incoming TCP connections to that port on the private network. You can set `CHAT_PORT` on the server to choose another port.

## Project structure

| Path | Responsibility |
| --- | --- |
| `ChatApp.Client/ChatForm.cs` | Connection controls, chat selection, and interface |
| `ChatApp.Client/ChatConnection.cs` | TCP connection and JSON messages |
| `ChatApp.Client/Conversation.cs` | Conversation history, drafts, and unread counts |
| `ChatApp.Client/MessageFeed.cs` | Message bubbles and scrolling |
| `ChatApp.Client/Assets/` | Windows application icon |
| `server/lib/chat_app.ex` | Elixir listener, connections, room membership, and message routing |
| `docs/` | README icon and in-app screenshots |

## Current limitations

The server listens on loopback by default. Display names identify users for a connection session and have no authentication. Messages are not stored on the server. Client histories and drafts reset on reconnection or application exit. The TCP connection is unencrypted.

## Screenshots

**Join screen**

![User name, server address, and port fields](docs/screenshots/join.png)

**Shared room**

![General room with the left chat list and current application icon](docs/screenshots/conversation.png)

**Private conversation**

![Private conversation with separate messages, draft, and latest message previews](docs/screenshots/private-chat.png)
