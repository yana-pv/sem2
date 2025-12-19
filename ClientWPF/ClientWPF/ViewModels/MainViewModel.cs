using ClientWPF.Services;
using Common.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ClientWPF.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly GameClientService _gameService;

        private string _serverIp = "127.0.0.1";
        public string ServerIp
        {
            get => _serverIp;
            set => SetField(ref _serverIp, value);
        }

        private int _serverPort = 5001;
        public int ServerPort
        {
            get => _serverPort;
            set => SetField(ref _serverPort, value);
        }

        private string _playerName = "Игрок";
        public string PlayerName
        {
            get => _playerName;
            set => SetField(ref _playerName, value);
        }

        private string _gameIdInput = string.Empty;
        public string GameIdInput
        {
            get => _gameIdInput;
            set => SetField(ref _gameIdInput, value);
        }

        private string _status = "Отключен";
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        private ObservableCollection<string> _messages = new();
        public ObservableCollection<string> Messages
        {
            get => _messages;
            set => SetField(ref _messages, value);
        }

        private ObservableCollection<Card> _hand = new();
        public ObservableCollection<Card> Hand
        {
            get => _hand;
            set => SetField(ref _hand, value);
        }

        private ClientGameStateDto? _gameState;
        public ClientGameStateDto? GameState
        {
            get => _gameState;
            set => SetField(ref _gameState, value);
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand CreateGameCommand { get; }
        public ICommand JoinGameCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand DrawCardCommand { get; }
        public ICommand PlayComboCommand { get; }

        public MainViewModel()
        {
            _gameService = new GameClientService();

            // Подписываемся на события сервиса
            _gameService.MessageReceived += OnMessageReceived;
            _gameService.HandUpdated += OnHandUpdated;
            _gameService.GameStateUpdated += OnGameStateUpdated;
            _gameService.Connected += OnConnected;
            _gameService.Disconnected += OnDisconnected;
            _gameService.GameCreated += OnGameCreated;

            // Команды
            ConnectCommand = new RelayCommand(async _ => await ConnectAsync());
            DisconnectCommand = new RelayCommand(_ => Disconnect());
            CreateGameCommand = new RelayCommand(async _ => await CreateGameAsync());
            JoinGameCommand = new RelayCommand(async _ => await JoinGameAsync());
            StartGameCommand = new RelayCommand(async _ => await StartGameAsync());
            DrawCardCommand = new RelayCommand(async _ => await DrawCardAsync());
            PlayComboCommand = new RelayCommand(async _ => await PlayComboAsync());
        }

        private async Task ConnectAsync()
        {
            try
            {
                Status = "Подключение...";
                ConnectCommandEnabled = false;
                await _gameService.ConnectAsync(ServerIp, ServerPort);
            }
            catch (Exception ex)
            {
                AddMessage($"Ошибка подключения: {ex.Message}");
                Status = "Ошибка подключения";
                ConnectCommandEnabled = true;
            }
        }

        private bool _connectCommandEnabled = true;
        public bool ConnectCommandEnabled
        {
            get => _connectCommandEnabled;
            set => SetField(ref _connectCommandEnabled, value);
        }

        private void Disconnect()
        {
            _gameService.Disconnect();
            Status = "Отключен";
            ConnectCommandEnabled = true;
        }

        private async Task CreateGameAsync()
        {
            if (string.IsNullOrWhiteSpace(PlayerName))
            {
                AddMessage("Введите имя игрока!");
                return;
            }

            await _gameService.CreateGameAsync(PlayerName);
        }

        private async Task JoinGameAsync()
        {
            if (!Guid.TryParse(GameIdInput, out var gameId))
            {
                AddMessage("Неверный ID игры!");
                return;
            }

            if (string.IsNullOrWhiteSpace(PlayerName))
            {
                AddMessage("Введите имя игрока!");
                return;
            }

            await _gameService.JoinGameAsync(gameId, PlayerName);
        }

        private async Task StartGameAsync()
        {
            if (!_gameService.GameId.HasValue)
            {
                AddMessage("Вы не в игре!");
                return;
            }

            await _gameService.StartGameAsync(_gameService.GameId.Value);
        }

        private async Task DrawCardAsync()
        {
            if (!_gameService.GameId.HasValue)
            {
                AddMessage("Вы не в игре!");
                return;
            }

            await _gameService.DrawCardAsync(_gameService.GameId.Value);
        }

        private async Task PlayComboAsync()
        {
            if (!_gameService.GameId.HasValue)
            {
                AddMessage("Вы не в игре!");
                return;
            }

            // Здесь должна быть логика выбора карт для комбо
            // Пока отправляем заглушку
            AddMessage("Функция комбо в разработке...");
        }

        private void OnMessageReceived(object? sender, string message)
        {
            AddMessage(message);
        }

        private void OnHandUpdated(object? sender, System.Collections.Generic.List<Card> hand)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Hand.Clear();
                foreach (var card in hand)
                {
                    Hand.Add(card);
                }
            });
        }

        private void OnGameStateUpdated(object? sender, ClientGameStateDto gameState)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                GameState = gameState;
                Status = $"В игре: {gameState.State}";

                // Обновляем информацию об игроках
                if (gameState.Players != null)
                {
                    var currentPlayer = gameState.Players.FirstOrDefault(p => p.IsCurrentPlayer);
                    if (currentPlayer != null)
                    {
                        Status += $" | Ходит: {currentPlayer.Name}";
                    }
                }
            });
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Status = "Подключен";
                AddMessage("Подключено к серверу!");
                ConnectCommandEnabled = true;
            });
        }

        private void OnDisconnected(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Status = "Отключен";
                AddMessage("Отключено от сервера");
                ConnectCommandEnabled = true;
            });
        }

        private void OnGameCreated(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_gameService.GameId.HasValue)
                {
                    AddMessage($"Игра создана! ID: {_gameService.GameId.Value}");
                    GameIdInput = _gameService.GameId.Value.ToString();
                }
            });
        }

        private void AddMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Messages.Insert(0, $"[{timestamp}] {message}");

                if (Messages.Count > 100)
                    Messages.RemoveAt(Messages.Count - 1);
            });
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}