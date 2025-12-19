using Common.Enums;
using Server.Game.Models;
using Server.Infrastructure;
using Server.Networking.Protocol;
using System.Net.Sockets;
using System.Text;

namespace Server.Networking.Commands.Handlers
{
    [Command(Command.JoinGame)]
    public class JoinGameHandler : ICommandHandler
    {
        public async Task Invoke(Socket sender, GameSessionManager sessionManager,
            byte[]? payload = null, CancellationToken ct = default)
        {
            try
            {
                if (payload == null || payload.Length == 0)
                {
                    await sender.SendError(CommandResponse.InvalidAction);
                    return;
                }

                var data = Encoding.UTF8.GetString(payload);
                Console.WriteLine($"[JoinGameHandler] Received payload: {data}");
                var parts = data.Split(':', 2);

                if (parts.Length != 2 || !Guid.TryParse(parts[0], out var gameId))
                {
                    await sender.SendError(CommandResponse.InvalidAction);
                    Console.WriteLine($"[JoinGameHandler] Invalid format. Parts: {parts.Length}");
                    return;
                }

                var session = sessionManager.GetSession(gameId);

                if (session == null)
                {
                    await sender.SendError(CommandResponse.GameNotFound);
                    Console.WriteLine($"[JoinGameHandler] Game not found: {gameId}");
                    return;
                }

                if (session.State != GameState.WaitingForPlayers)
                {
                    await sender.SendError(CommandResponse.GameAlreadyStarted);
                    Console.WriteLine($"[JoinGameHandler] Game already started: {gameId}");
                    return;
                }

                if (session.IsFull)
                {
                    await sender.SendError(CommandResponse.GameFull);
                    Console.WriteLine($"[JoinGameHandler] Game is full: {gameId}");
                    return;
                }

                var playerName = parts[1];
                var player = new Player
                {
                    Id = Guid.NewGuid(),
                    Connection = sender,
                    Name = playerName
                };

                if (!session.AddPlayer(player))
                {
                    await sender.SendError(CommandResponse.InvalidAction);
                    Console.WriteLine($"[JoinGameHandler] Failed to add player: {playerName}");
                    return;
                }

                // Отправляем GameCreated с ID игры и игрока (так же как при создании)
                var response = KittensPackageBuilder.CreateGameResponse(session.Id, player.Id);
                Console.WriteLine($"[JoinGameHandler] Sending CreateGameResponse: {session.Id}:{player.Id}");
                await sender.SendAsync(response, SocketFlags.None);

                await sender.SendMessage($"Вы присоединились к игре как: {playerName}");

                // Отправляем PlayerJoined всем (включая самого игрока)
                var joinMessage = $"{session.Id}:{player.Id}";
                var joinData = KittensPackageBuilder.MessageResponse(joinMessage, Command.PlayerJoined);
                await session.BroadcastToAll(joinData);

                await session.BroadcastMessage($"{playerName} присоединился к игре!");

                // Уведомляем подписчиков лобби об обновлении
                await sessionManager.BroadcastGamesListUpdate();

                // Автоматически подписываем нового игрока на обновления
                sessionManager.SubscribeToGamesList(sender);

                await session.BroadcastGameState();

                if (session.CanStart && session.Players.Count == session.MaxPlayers)
                {
                    await session.BroadcastMessage($"Игра заполнена! Готовы начать?");
                }
                
                Console.WriteLine($"[JoinGameHandler] Player {playerName} joined game {gameId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JoinGameHandler] Exception: {ex.Message}");
                await sender.SendError(CommandResponse.InvalidAction);
            }
        }
    }
}