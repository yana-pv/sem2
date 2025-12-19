using Common.Enums;
using Server.Infrastructure;
using Server.Networking.Protocol;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server.Networking.Commands.Handlers
{
    [Command(Command.StartGame)]
    public class StartGameHandler : ICommandHandler
    {
        public async Task Invoke(Socket sender, GameSessionManager sessionManager,
            byte[]? payload = null, CancellationToken ct = default)
        {
            if (payload == null || payload.Length == 0)
            {
                await sender.SendError(CommandResponse.InvalidAction);
                return;
            }

            if (!Guid.TryParse(Encoding.UTF8.GetString(payload), out var gameId))
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

            var player = session.GetPlayerBySocket(sender);
            if (player == null)
            {
                await sender.SendError(CommandResponse.PlayerNotFound);
                return;
            }

            // Проверяем, является ли игрок создателем (первым игроком)
            var creator = session.Players.FirstOrDefault();
            if (creator == null || creator.Id != player.Id)
            {
                await sender.SendError(CommandResponse.InvalidAction);
                await sender.SendMessage("❌ Только создатель игры может начать игру!");
                return;
            }

            if (session.State != GameState.WaitingForPlayers)
            {
                await sender.SendError(CommandResponse.GameAlreadyStarted);
                return;
            }

            if (!session.CanStart)
            {
                await sender.SendError(CommandResponse.NotEnoughCards);
                await sender.SendMessage($"❌ Недостаточно игроков! Нужно от {session.MinPlayers} до {session.MaxPlayers}");
                return;
            }

            try
            {
                session.StartGame();

                // Уведомляем всех игроков
                await session.BroadcastMessage($"Игра началась! Первым ходит {session.CurrentPlayer!.Name}");

                // Отправляем GameStarted всем игрокам
                var gameStartedData = KittensPackageBuilder.MessageResponse("", Command.GameStarted);
                await session.BroadcastToAll(gameStartedData);

                await session.BroadcastGameState();

                // Рассылаем карты игрокам
                foreach (var p in session.Players)
                {
                    try
                    {
                        await p.Connection.SendPlayerHand(p);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка отправки руки игроку {p.Name}: {ex.Message}");
                        await sender.SendMessage($"Ошибка отправки карт игроку {p.Name}");
                    }
                }

                // Уведомляем всех в лобби об изменении статуса игры
                await sessionManager.BroadcastGamesListUpdate();

                Console.WriteLine($"Игра {session.Id} начата игроком {player.Name}");
            }
            catch (Exception ex)
            {
                await sender.SendMessage($"Ошибка при запуске игры: {ex.Message}");
                Console.WriteLine($"Ошибка в StartGameHandler: {ex}");
            }
        }
    }
}