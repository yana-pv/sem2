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
            if (payload == null || payload.Length == 0)
            {
                await sender.SendError(CommandResponse.InvalidAction);
                return;
            }

            var data = Encoding.UTF8.GetString(payload);
            var parts = data.Split(':', 2);

            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var gameId))
            {
                await sender.SendError(CommandResponse.InvalidAction);
                return;
            }

            var session = sessionManager.GetSession(gameId);

            if (session == null)
            {
                await sender.SendError(CommandResponse.GameNotFound);
                return;
            }

            if (session.State != GameState.WaitingForPlayers)
            {
                await sender.SendError(CommandResponse.GameAlreadyStarted);
                return;
            }

            if (session.IsFull)
            {
                await sender.SendError(CommandResponse.GameFull);
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
                return;
            }

            // Отправляем GameCreated с ID игры и игрока (так же как при создании)
            await sender.SendAsync(KittensPackageBuilder.CreateGameResponse(session.Id, player.Id),
                SocketFlags.None);

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
        }
    }
}