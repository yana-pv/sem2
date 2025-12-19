using Common.Enums;
using Server.Game.Models;
using Server.Infrastructure;
using Server.Networking.Protocol;
using System.Net.Sockets;
using System.Text;

namespace Server.Networking.Commands.Handlers
{
    [Command(Command.CreateGame)]
    public class CreateGameHandler : ICommandHandler
    {
        public async Task Invoke(Socket sender, GameSessionManager sessionManager,
            byte[]? payload = null, CancellationToken ct = default)
        {
            string playerName = "Player";
            int maxPlayers = 5; // Значение по умолчанию

            if (payload != null && payload.Length > 0)
            {
                var data = Encoding.UTF8.GetString(payload);
                var parts = data.Split(':', 2);
                
                playerName = parts[0];
                
                // Если передано количество игроков
                if (parts.Length == 2 && int.TryParse(parts[1], out int requestedMaxPlayers))
                {
                    // Ограничиваем значение от 2 до 5
                    maxPlayers = Math.Clamp(requestedMaxPlayers, 2, 5);
                }
            }

            var session = new GameSession
            {
                Id = Guid.NewGuid(),
                GameDeck = new Deck(),
                MaxPlayers = maxPlayers
            };

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

            sessionManager.CreateSession(session);

            await sender.SendAsync(KittensPackageBuilder.CreateGameResponse(session.Id, player.Id),
                SocketFlags.None);

            await sender.SendMessage($"🎮 Игра создана! ID: {session.Id}");
            await sender.SendMessage($"👤 Вы вошли как: {playerName}");
            await sender.SendMessage($"👥 Ожидание игроков ({session.MinPlayers}-{session.MaxPlayers})...");
            await sender.SendMessage($"📋 Отправьте ID другим игрокам для подключения");

            // Автоматически подписываем создателя на обновления списка игр
            sessionManager.SubscribeToGamesList(sender);

            // Уведомляем всех о новой игре
            await sessionManager.BroadcastGamesListUpdate();
        }
    }
}