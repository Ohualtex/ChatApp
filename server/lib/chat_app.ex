defmodule ChatApp do
  use Application
  require Logger

  @impl true
  def start(_type, _args) do
    port = System.get_env("CHAT_PORT", "5050") |> String.to_integer()

    {:ok, ip} =
      System.get_env("CHAT_BIND", "127.0.0.1")
      |> String.to_charlist()
      |> :inet.parse_ipv4_address()

    Supervisor.start_link(
      [ChatApp.Room, {ChatApp.Listener, {ip, port}}],
      strategy: :one_for_all,
      name: ChatApp.Supervisor
    )
  end
end

defmodule ChatApp.Listener do
  use GenServer
  require Logger

  def start_link(address), do: GenServer.start_link(__MODULE__, address)

  @impl true
  def init({ip, port}) do
    options = [
      :binary,
      ip: ip,
      active: false,
      packet: :line,
      packet_size: 8192,
      reuseaddr: true,
      send_timeout: 1000,
      send_timeout_close: true
    ]

    case :gen_tcp.listen(port, options) do
      {:ok, listener} ->
        {:ok, {_address, bound_port}} = :inet.sockname(listener)
        Logger.info("Chat server listening on #{:inet.ntoa(ip)}:#{bound_port}")
        {:ok, listener, {:continue, :accept}}

      {:error, reason} ->
        {:stop, reason}
    end
  end

  @impl true
  def handle_continue(:accept, listener) do
    case :gen_tcp.accept(listener) do
      {:ok, socket} ->
        handler =
          spawn(fn ->
            receive do
              {:socket, ^socket} -> ChatApp.Connection.run(socket)
              :close -> :ok
            end
          end)

        case :gen_tcp.controlling_process(socket, handler) do
          :ok ->
            send(handler, {:socket, socket})

          {:error, _reason} ->
            send(handler, :close)
            :gen_tcp.close(socket)
        end

        {:noreply, listener, {:continue, :accept}}

      {:error, reason} ->
        {:stop, reason, listener}
    end
  end
end

defmodule ChatApp.Connection do
  def run(socket) do
    try do
      case read(socket, 5000) do
        {:ok, %{"type" => "join", "name" => name}} when is_binary(name) ->
          name = String.trim(name)

          if String.valid?(name) and String.length(name) in 1..24 and
               not String.match?(name, ~r/[\x00-\x1f\x7f]/u) do
            case ChatApp.Room.join(socket, name) do
              :ok -> receive_messages(socket)
              {:error, reason} -> send_packet(socket, %{type: "error", message: reason})
            end
          else
            send_packet(socket, %{
              type: "error",
              message: "Enter a name between 1 and 24 characters."
            })
          end

        {:ok, _packet} ->
          send_packet(socket, %{type: "error", message: "Join before sending messages."})

        {:error, _reason} ->
          :ok
      end
    after
      :gen_tcp.close(socket)
    end
  end

  def send_packet(socket, packet), do: :gen_tcp.send(socket, [:json.encode(packet), "\n"])

  defp receive_messages(socket) do
    case read(socket, :infinity) do
      {:ok, %{"type" => "chat", "message" => message} = packet} when is_binary(message) ->
        recipient =
          case Map.get(packet, "recipient") do
            :null -> nil
            value -> value
          end

        message = String.trim(message)

        if String.valid?(message) and String.length(message) in 1..1000 and
             (is_nil(recipient) or (is_binary(recipient) and byte_size(recipient) <= 96)) do
          ChatApp.Room.publish(message, recipient)
        else
          send_packet(socket, %{
            type: "error",
            message: "Messages must contain 1 to 1000 characters."
          })
        end

        receive_messages(socket)

      {:ok, _packet} ->
        send_packet(socket, %{type: "error", message: "Unsupported message."})
        receive_messages(socket)

      {:error, _reason} ->
        :ok
    end
  end

  defp read(socket, timeout) do
    with {:ok, line} <- :gen_tcp.recv(socket, 0, timeout) do
      {:ok, :json.decode(line)}
    end
  rescue
    ArgumentError -> {:error, :invalid_json}
    ErlangError -> {:error, :invalid_json}
  end
end

defmodule ChatApp.Room do
  use GenServer
  require Logger

  def start_link(_options), do: GenServer.start_link(__MODULE__, %{}, name: __MODULE__)
  def join(socket, name), do: GenServer.call(__MODULE__, {:join, socket, name})
  def publish(message, recipient), do: GenServer.call(__MODULE__, {:chat, message, recipient})

  @impl true
  def init(members), do: {:ok, members}

  @impl true
  def handle_call({:join, socket, name}, {pid, _tag}, members) do
    if Enum.any?(members, fn {_pid, member} ->
         String.downcase(member.name) == String.downcase(name)
       end) do
      {:reply, {:error, "That user name is already in use."}, members}
    else
      case ChatApp.Connection.send_packet(socket, %{type: "joined", name: name}) do
        :ok ->
          member = %{socket: socket, name: name, monitor: Process.monitor(pid)}
          updated = Map.put(members, pid, member)
          publish_members("Joined: #{name}", updated)
          {:reply, :ok, updated}

        {:error, _reason} ->
          {:reply, {:error, "Unable to open the chat."}, members}
      end
    end
  end

  @impl true
  def handle_call({:chat, message, recipient}, {pid, _tag}, members) do
    case Map.fetch(members, pid) do
      {:ok, sender} ->
        target = Enum.find(members, fn {_pid, member} -> member.name == recipient end)

        cond do
          is_nil(recipient) ->
            packet = chat_packet(sender.name, message, nil)

            Enum.each(members, fn {_pid, member} ->
              ChatApp.Connection.send_packet(member.socket, packet)
            end)

          target != nil ->
            {target_pid, receiver} = target
            packet = chat_packet(sender.name, message, receiver.name)

            case ChatApp.Connection.send_packet(receiver.socket, packet) do
              :ok ->
                if target_pid != pid, do: ChatApp.Connection.send_packet(sender.socket, packet)

              {:error, _reason} ->
                ChatApp.Connection.send_packet(sender.socket, %{
                  type: "error",
                  message: "The recipient is no longer connected."
                })
            end

          true ->
            ChatApp.Connection.send_packet(sender.socket, %{
              type: "error",
              message: "The recipient is offline."
            })
        end

      :error ->
        :ok
    end

    {:reply, :ok, members}
  end

  defp chat_packet(sender, message, recipient) do
    %{
      type: "chat",
      name: sender,
      message: message,
      recipient: recipient || :null,
      timestamp: DateTime.utc_now() |> DateTime.to_iso8601()
    }
  end

  @impl true
  def handle_info({:DOWN, monitor, :process, pid, _reason}, members) do
    case Map.fetch(members, pid) do
      {:ok, %{monitor: ^monitor, name: name}} ->
        updated = Map.delete(members, pid)
        publish_members("Left: #{name}", updated)
        {:noreply, updated}

      _other ->
        {:noreply, members}
    end
  end

  defp publish_members(event, members) do
    names = members |> Map.values() |> Enum.map(& &1.name) |> Enum.sort()
    packet = %{type: "presence", users: names}

    Enum.each(members, fn {_pid, member} ->
      ChatApp.Connection.send_packet(member.socket, packet)
    end)

    Logger.info("#{event} | Connected (#{map_size(members)}): #{Enum.join(names, ", ")}")
  end
end
