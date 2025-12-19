using Common.Enums;
using Common.Models;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ClientWPF.Services
{
    public class GameClientService
    {
        private Socket? _socket;
        private readonly CancellationTokenSource _cts = new();
        private readonly byte[] _buffer = new byte[4096];

        // События
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<List<Card>>? HandUpdated;
        public event EventHandler<ClientGameStateDto>? GameStateUpdated;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler? GameCreated;
        public event EventHandler? PlayerJoined;
        public event EventHandler? GameStarted;
        public event EventHandler<List<GameSessionInfoDto>>? GamesListUpdated; // Используем DTO

        // Свойства
        public bool IsConnected => _socket?.Connected == true;
        public Guid? PlayerId { get; private set; }
        public Guid? GameId { get; private set; }
        public string? PlayerName { get; set; }

        // Коллекция доступных игр
        public ObservableCollection<GameSessionInfoDto> AvailableGames { get; } = new();

        // Подключение к серверу
        public async Task ConnectAsync(string host, int port)
        {
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await _socket.ConnectAsync(host, port);

                Connected?.Invoke(this, EventArgs.Empty);

                // Начинаем получение сообщений
                _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token));

                // Запрашиваем список игр
                await GetGamesListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось подключиться: {ex.Message}");
            }
        }

        // Отправка команды
        public async Task SendCommandAsync(Command command, string? payload = null)
        {
            if (!IsConnected || _socket == null) return;

            try
            {
                var payloadBytes = payload != null
                    ? Encoding.UTF8.GetBytes(payload)
                    : Array.Empty<byte>();

                var package = BuildPackage(command, payloadBytes);
                await _socket.SendAsync(package, SocketFlags.None);
                // Небольшое диагностическое сообщение для UX
                if (command == Command.CreateGame)
                    MessageReceived?.Invoke(this, "Запрос на создание игры отправлен");
                if (command == Command.JoinGame)
                    MessageReceived?.Invoke(this, "Запрос на присоединение отправлен");
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke(this, $"Ошибка отправки команды: {ex.Message}");
            }
        }

        // Получение сообщений
        private async Task ReceiveMessagesAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _socket?.Connected == true)
                {
                    var bytesRead = await _socket.ReceiveAsync(_buffer, SocketFlags.None, ct);
                    if (bytesRead == 0) break;

                    var data = new byte[bytesRead];
                    Array.Copy(_buffer, 0, data, 0, bytesRead);

                    ProcessReceivedData(data);
                }
            }
            catch (OperationCanceledException)
            {
                // Отмена запрошена
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke(this, $"Ошибка получения данных: {ex.Message}");
            }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        // Обработка полученных данных
        private void ProcessReceivedData(byte[] data)
        {
            if (TryParsePackage(data, out var parsed))
            {
                var (command, payload) = parsed.Value;
                HandleCommand(command, payload);
            }
            else
            {
                MessageReceived?.Invoke(this, "Ошибка разбора пакета");
            }
        }

        // Обработка команд
        private void HandleCommand(Command command, byte[] payload)
        {
            try
            {
                switch (command)
                {
                    case Command.Message:
                        var message = Encoding.UTF8.GetString(payload);
                        MessageReceived?.Invoke(this, message);
                        break;

                    case Command.GameCreated:
                        var gameData = Encoding.UTF8.GetString(payload);
                        var parts = gameData.Split(':');
                        if (parts.Length == 2 &&
                            Guid.TryParse(parts[0], out var gameId) &&
                            Guid.TryParse(parts[1], out var playerId))
                        {
                            GameId = gameId;
                            PlayerId = playerId;
                            GameCreated?.Invoke(this, EventArgs.Empty);
                            MessageReceived?.Invoke(this, $"✅ GameCreated обработана: {gameId}");
                        }
                        else
                        {
                            MessageReceived?.Invoke(this, $"❌ Ошибка парсинга GameCreated. Получено: '{gameData}' (parts: {parts.Length})");
                        }
                        break;

                    case Command.PlayerJoined:
                        var joinData = Encoding.UTF8.GetString(payload);
                        var joinParts = joinData.Split(':');
                        if (joinParts.Length == 2 &&
                            Guid.TryParse(joinParts[0], out var joinedGameId) &&
                            Guid.TryParse(joinParts[1], out var joinedPlayerId))
                        {
                            // Устанавливаем GameId и PlayerId для присоединившегося игрока
                            GameId = joinedGameId;
                            PlayerId = joinedPlayerId;
                        }
                        else
                        {
                            MessageReceived?.Invoke(this, $"❌ Ошибка парсинга PlayerJoined. Получено: '{joinData}' (parts: {joinParts.Length})");
                        }
                        PlayerJoined?.Invoke(this, EventArgs.Empty);
                        MessageReceived?.Invoke(this, $"✅ PlayerJoined обработана: {GameId}");
                        break;

                    case Command.GameStarted:
                        GameStarted?.Invoke(this, EventArgs.Empty);
                        break;

                    case Command.GamesListUpdated:
                        var gamesJson = Encoding.UTF8.GetString(payload);
                        var gamesList = JsonSerializer.Deserialize<List<GameSessionInfoDto>>(gamesJson);
                        if (gamesList != null)
                        {
                            // Обновляем коллекцию в UI потоке
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                AvailableGames.Clear();
                                foreach (var game in gamesList)
                                {
                                    AvailableGames.Add(game);
                                }
                            });

                            GamesListUpdated?.Invoke(this, gamesList);
                        }
                        break;

                    case Command.PlayerHandUpdate:
                        try
                        {
                            var handJson = Encoding.UTF8.GetString(payload);
                            var hand = JsonSerializer.Deserialize<List<Card>>(handJson);
                            if (hand != null)
                                HandUpdated?.Invoke(this, hand);
                        }
                        catch (JsonException)
                        {
                            MessageReceived?.Invoke(this, "Ошибка разбора карт");
                        }
                        break;

                    case Command.GameStateUpdate:
                        try
                        {
                            var stateJson = Encoding.UTF8.GetString(payload);
                            var gameState = JsonSerializer.Deserialize<ClientGameStateDto>(stateJson);
                            if (gameState != null)
                                GameStateUpdated?.Invoke(this, gameState);
                        }
                        catch (JsonException)
                        {
                            MessageReceived?.Invoke(this, "Ошибка разбора состояния игры");
                        }
                        break;

                    case Command.Error:
                        if (payload.Length > 0)
                        {
                            var error = (CommandResponse)payload[0];
                            MessageReceived?.Invoke(this, $"Ошибка сервера: {error}");
                        }
                        break;

                    default:
                        MessageReceived?.Invoke(this, $"Неизвестная команда: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke(this, $"Ошибка обработки команды: {ex.Message}");
            }
        }

        // Метод для запроса списка игр
        public async Task GetGamesListAsync()
        {
            await SendCommandAsync(Command.GetGamesList);
        }

        // Методы для конкретных команд
        public async Task CreateGameAsync(string playerName, int maxPlayers = 5)
        {
            PlayerName = playerName;
            // Ограничиваем значение от 2 до 5
            maxPlayers = Math.Clamp(maxPlayers, 2, 5);
            await SendCommandAsync(Command.CreateGame, $"{playerName}:{maxPlayers}");
        }

        public async Task JoinGameAsync(Guid gameId, string playerName)
        {
            PlayerName = playerName;
            await SendCommandAsync(Command.JoinGame, $"{gameId}:{playerName}");
        }

        public async Task StartGameAsync(Guid gameId)
        {
            await SendCommandAsync(Command.StartGame, gameId.ToString());
        }

        public async Task GetGameStateAsync(Guid gameId)
        {
            await SendCommandAsync(Command.GetGameState, gameId.ToString());
        }

        public async Task GetPlayerHandAsync(Guid gameId)
        {
            await SendCommandAsync(Command.GetPlayerHand, $"{gameId}:{PlayerId}");
        }

        public async Task PlayCardAsync(Guid gameId, int cardIndex, string? additionalData = null)
        {
            var payload = additionalData != null
                ? $"{gameId}:{PlayerId}:{cardIndex}:{additionalData}"
                : $"{gameId}:{PlayerId}:{cardIndex}";

            await SendCommandAsync(Command.PlayCard, payload);
        }

        public async Task DrawCardAsync(Guid gameId)
        {
            await SendCommandAsync(Command.DrawCard, $"{gameId}:{PlayerId}");
        }

        public async Task EndTurnAsync(Guid gameId)
        {
            await SendCommandAsync(Command.EndTurn, $"{gameId}:{PlayerId}");
        }

        public async Task PlayComboAsync(Guid gameId, int comboType, List<int> cardIndices, string? targetData = null)
        {
            string cardIndicesStr = string.Join(",", cardIndices);
            string payload = targetData != null
                ? $"{gameId}:{PlayerId}:{comboType}:{cardIndicesStr}:{targetData}"
                : $"{gameId}:{PlayerId}:{comboType}:{cardIndicesStr}";

            await SendCommandAsync(Command.UseCombo, payload);
        }

        public void Disconnect()
        {
            try
            {
                _cts.Cancel();
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
            }
            catch
            {
                // Игнорируем ошибки при отключении
            }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        // Вспомогательные методы для работы с пакетами
        private static byte[] BuildPackage(Command command, byte[] payload)
        {
            if (payload.Length > 4096)
                throw new ArgumentException("Payload too large");

            var package = new byte[1 + 1 + 2 + payload.Length + 1];

            package[0] = 0x02; // Start byte
            package[1] = (byte)command;

            ushort length = (ushort)payload.Length;
            package[2] = (byte)(length & 0xFF);
            package[3] = (byte)((length >> 8) & 0xFF);

            if (payload.Length > 0)
            {
                Array.Copy(payload, 0, package, 4, payload.Length);
            }

            package[^1] = 0x03; // End byte
            return package;
        }

        private static bool TryParsePackage(ReadOnlySpan<byte> data, out (Command Command, byte[] Payload)? result)
        {
            result = null;

            if (data.Length < 5) return false;
            if (data[0] != 0x02 || data[^1] != 0x03) return false;

            var commandByte = data[1];
            if (!Enum.IsDefined(typeof(Command), commandByte)) return false;

            var command = (Command)commandByte;

            ushort length = (ushort)(data[2] | (data[3] << 8));

            if (length + 4 + 1 != data.Length) return false; // Проверяем полную длину

            var payload = length > 0
                ? data.Slice(4, length).ToArray()
                : Array.Empty<byte>();

            result = (command, payload);
            return true;
        }
    }
}