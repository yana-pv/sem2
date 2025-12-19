using Common.Enums;
using Common.Models;
using Server.Game.Models;
using Server.Networking.Protocol;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Server.Infrastructure
{
    public class GameSessionManager
    {
        private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
        private readonly Timer _cleanupTimer;
        private readonly List<Socket> _lobbySubscribers = new(); // Подписчики на обновления лобби

        public GameSessionManager()
        {
            // Очистка неактивных сессий каждые 5 минут
            _cleanupTimer = new Timer(CleanupInactiveSessions, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public GameSession? GetSession(Guid sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public bool CreateSession(GameSession session)
        {
            var result = _sessions.TryAdd(session.Id, session);

            if (result)
            {
                // Уведомляем всех подписчиков о новой игре
                BroadcastGamesListUpdate();
            }

            return result;
        }

        public bool RemoveSession(Guid sessionId)
        {
            var result = _sessions.TryRemove(sessionId, out _);

            if (result)
            {
                // Уведомляем всех подписчиков об удалении игры
                BroadcastGamesListUpdate();
            }

            return result;
        }

        public IEnumerable<GameSession> GetActiveSessions()
        {
            return _sessions.Values.Where(s =>
                s.State != GameState.GameOver &&
                s.Players.Count > 0 &&
                DateTime.UtcNow - s.CreatedAt < TimeSpan.FromHours(1));
        }

        // Получить список игр для лобби
        public List<GameSessionInfoDto> GetAvailableGames()
        {
            var games = new List<GameSessionInfoDto>();

            foreach (var session in _sessions.Values)
            {
                if (session.State == GameState.WaitingForPlayers &&
                    session.Players.Count > 0 &&
                    session.Players.Count < session.MaxPlayers)
                {
                    var creator = session.Players.FirstOrDefault();

                    games.Add(new GameSessionInfoDto
                    {
                        Id = session.Id,
                        Name = $"Игра {session.Id.ToString().Substring(0, 8)}...",
                        PlayersCount = session.Players.Count,
                        MaxPlayers = session.MaxPlayers,
                        Status = GetGameStatus(session),
                        CreatedAt = session.CreatedAt,
                        CreatorName = creator?.Name ?? "Неизвестно"
                    });
                }
            }

            return games.OrderByDescending(g => g.CreatedAt).ToList();
        }

        private string GetGameStatus(GameSession session)
        {
            return session.State switch
            {
                GameState.WaitingForPlayers => "Ожидание игроков",
                GameState.Initializing => "Начинается",
                GameState.PlayerTurn => "Идет игра",
                GameState.GameOver => "Завершена",
                _ => "Неизвестно"
            };
        }

        // Подписка на обновления списка игр
        public void SubscribeToGamesList(Socket clientSocket)
        {
            if (!_lobbySubscribers.Contains(clientSocket))
            {
                _lobbySubscribers.Add(clientSocket);
            }
        }

        // Отписка от обновлений
        public void UnsubscribeFromGamesList(Socket clientSocket)
        {
            _lobbySubscribers.Remove(clientSocket);
        }

        // Рассылка обновления списка игр всем подписчикам
        public async Task BroadcastGamesListUpdate()
        {
            var games = GetAvailableGames();
            var gamesJson = System.Text.Json.JsonSerializer.Serialize(games);

            var tasks = new List<Task>();

            foreach (var socket in _lobbySubscribers.ToList())
            {
                if (socket.Connected)
                {
                    try
                    {
                        var data = KittensPackageBuilder.GamesListResponse(gamesJson);
                        tasks.Add(socket.SendAsync(data, SocketFlags.None));
                    }
                    catch
                    {
                        // Удаляем отключившихся подписчиков
                        _lobbySubscribers.Remove(socket);
                    }
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        private void CleanupInactiveSessions(object? state)
        {
            var inactiveSessions = _sessions.Values
                .Where(s => s.State == GameState.GameOver ||
                           s.Players.Count == 0 ||
                           DateTime.UtcNow - s.CreatedAt > TimeSpan.FromHours(1))
                .ToList();

            foreach (var session in inactiveSessions)
            {
                if (_sessions.TryRemove(session.Id, out _))
                {
                    // Уведомляем об удалении игры
                    BroadcastGamesListUpdate();
                }
            }
        }
    }
}