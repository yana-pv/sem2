using Common.Enums;
using Common.Models;
using Server.Infrastructure;
using Server.Networking.Protocol;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server.Networking.Commands.Handlers
{
    [Command(Command.GetGamesList)]
    public class GetGamesListHandler : ICommandHandler
    {
        public async Task Invoke(Socket sender, GameSessionManager sessionManager,
            byte[]? payload = null, CancellationToken ct = default)
        {
            try
            {
                // Подписываем клиента на обновления списка игр
                sessionManager.SubscribeToGamesList(sender);

                // Получаем текущий список игр
                var games = sessionManager.GetAvailableGames();
                var gamesJson = JsonSerializer.Serialize(games);

                // Отправляем список игр
                var response = KittensPackageBuilder.GamesListResponse(gamesJson);
                await sender.SendAsync(response, SocketFlags.None);

                await sender.SendMessage("✅ Подписан на обновления списка игр");
            }
            catch (Exception ex)
            {
                await sender.SendMessage($"❌ Ошибка получения списка игр: {ex.Message}");
            }
        }
    }
}