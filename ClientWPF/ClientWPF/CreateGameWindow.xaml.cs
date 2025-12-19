using ClientWPF.Services;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClientWPF
{
    public partial class CreateGameWindow : Window
    {
        private readonly GameClientService _gameService;
        private readonly string _playerName;
        private Guid _gameId;
        private int _maxPlayers = 5;
        private int _currentPlayers = 1;
        private DispatcherTimer _updateTimer;
        private readonly StringBuilder _messages = new();

        public CreateGameWindow(GameClientService gameService, string playerName)
        {
            InitializeComponent();
            _gameService = gameService;
            _playerName = playerName;

            // Настройка таймера для обновления статуса
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;

            // Подписываемся на события
            _gameService.MessageReceived += OnMessageReceived;
            _gameService.GameCreated += OnGameCreated;
            _gameService.GameStateUpdated += OnGameStateUpdated;
            _gameService.PlayerJoined += OnPlayerJoined;
            _gameService.GamesListUpdated += OnGamesListUpdated;

            // Устанавливаем максимальное количество игроков по умолчанию
            PlayersComboBox.SelectedIndex = 3; // 5 игроков по умолчанию
            _maxPlayers = 5;
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Анимация кнопки
            AnimateButton(CreateButton);

            // Получаем настройки
            if (PlayersComboBox.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string maxPlayersStr)
            {
                _maxPlayers = int.Parse(maxPlayersStr);
            }

            string roomName = RoomNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(roomName))
            {
                roomName = $"Игра {_playerName}";
            }

            // Создаем игру на сервере
            try
            {
                CreateButton.IsEnabled = false;
                
                // Показываем панель ожидания и ID до создания
                GameIdPanel.Visibility = Visibility.Visible;
                GameIdText.Text = "⏳ Генерируется...";
                WaitingPanel.Visibility = Visibility.Visible;
                MessagesPanel.Visibility = Visibility.Visible;
                CreateButton.Visibility = Visibility.Collapsed;
                StartButton.Visibility = Visibility.Visible;
                StartButton.IsEnabled = false;

                AddMessage($"⏳ Создание игры: {roomName}...");
                AddMessage($"📊 Максимум игроков: {_maxPlayers}");

                // Отправляем имя игрока и максимальное количество игроков
                await _gameService.SendCommandAsync(Command.CreateGame, $"{_playerName}:{_maxPlayers}");

                // Ждем пока GameClientService установит GameId (максимум 5 секунд)
                var created = false;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(5))
                {
                    if (_gameService.GameId.HasValue)
                    {
                        _gameId = _gameService.GameId.Value;
                        GameIdText.Text = _gameId.ToString();
                        AddMessage($"✅ 🎮 ID ИГРЫ: {_gameId}");
                        AddMessage("📋 Нажмите кнопку копирования ID для отправки другим игрокам");
                        created = true;
                        break;
                    }
                    await Task.Delay(100);
                }
                sw.Stop();

                if (!created)
                {
                    AddMessage("⚠️ ID игры не получен, попробуйте позже");
                    GameIdText.Text = "❌ Ошибка";
                }
                else
                {
                    _updateTimer.Start();
                    AddMessage($"✅ Игра создана: {roomName}");
                    AddMessage($"👥 Текущая очередь: 1/{_maxPlayers}");
                    AddMessage("📢 Ожидаем подключение игроков...");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания игры: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                CreateButton.IsEnabled = true;
                GameIdText.Text = "❌ Ошибка создания";
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_gameService.GameId.HasValue)
            {
                MessageBox.Show("Игра не создана!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StartButton.IsEnabled = false;
                StartButton.Content = "ЗАПУСК...";

                await _gameService.StartGameAsync(_gameService.GameId.Value);

                AddMessage("🚀 Игра начинается...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска игры: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StartButton.Content = "НАЧАТЬ ИГРУ";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Возврат в лобби
            _updateTimer.Stop();
            var lobbyWindow = new LobbyWindow(_gameService, _playerName);
            lobbyWindow.Show();
            this.Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возврат в лобби
            _updateTimer.Stop();
            var lobbyWindow = new LobbyWindow(_gameService, _playerName);
            lobbyWindow.Show();
            this.Close();
        }

        private void CopyIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_gameService.GameId.HasValue)
            {
                Clipboard.SetText(_gameService.GameId.Value.ToString());
                AddMessage("✅ ID скопирован в буфер обмена");
            }
        }

        private void RoomNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RoomNameCounter.Text = $"{RoomNameTextBox.Text.Length}/30";
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Получаем актуальное состояние игры из сервиса
            // Информацию об игроках получаем из GamesListUpdated события
            if (_gameService.AvailableGames != null)
            {
                var currentGame = _gameService.AvailableGames.FirstOrDefault(g => g.Id == _gameId);
                if (currentGame != null)
                {
                    _currentPlayers = currentGame.PlayersCount;
                    PlayersCountText.Text = $"Игроков: {_currentPlayers}/{currentGame.MaxPlayers}";
                    WaitingStatusText.Text = $"Ожидание игроков... ({_currentPlayers}/{currentGame.MaxPlayers})";

                    // Проверяем, можно ли начать игру (2-5 игроков)
                    bool canStart = _currentPlayers >= 2 && _currentPlayers <= currentGame.MaxPlayers;
                    
                    if (canStart != StartButton.IsEnabled)
                    {
                        StartButton.IsEnabled = canStart;
                        if (canStart)
                        {
                            StartButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Ярко-синий
                            AddMessage($"✅ Достаточно игроков ({_currentPlayers}/{currentGame.MaxPlayers}) - можно начать!");
                        }
                        else
                        {
                            StartButton.Background = new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Серый
                            AddMessage($"⏳ Нужно еще игроков ({_currentPlayers}/{currentGame.MaxPlayers})...");
                        }
                    }
                }
            }
        }

        private void OnPlayerJoined(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage($"✨ Новый игрок присоединился к игре!");
                // Счетчик обновится через UpdateTimer_Tick и OnGamesListUpdated
            });
        }

        private void OnGamesListUpdated(object? sender, List<GameSessionInfoDto> games)
        {
            Dispatcher.Invoke(() =>
            {
                // Обновляем информацию о текущей игре
                if (_gameId != Guid.Empty)
                {
                    var gameInfo = games.FirstOrDefault(g => g.Id == _gameId);
                    if (gameInfo != null)
                    {
                        _currentPlayers = gameInfo.PlayersCount;
                        PlayersCountText.Text = $"Игроков: {_currentPlayers}/{gameInfo.MaxPlayers}";
                        WaitingStatusText.Text = $"Ожидание игроков... ({_currentPlayers}/{gameInfo.MaxPlayers})";

                        // Проверяем, можно ли начать игру (2-5 игроков)
                        bool canStart = _currentPlayers >= 2 && _currentPlayers <= gameInfo.MaxPlayers;
                        
                        if (canStart)
                        {
                            StartButton.IsEnabled = true;
                        }
                        else if (_currentPlayers < 2)
                        {
                            StartButton.IsEnabled = false;
                        }
                    }
                }
            });
        }

        private void OnMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage(message);
            });
        }

        private void OnGameCreated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_gameService.GameId.HasValue)
                {
                    _gameId = _gameService.GameId.Value;
                    GameIdText.Text = _gameId.ToString();
                    AddMessage($"🎮 ID игры: {_gameId}");

                    // Активируем кнопку начала игры
                    StartButton.IsEnabled = true;
                }
            });
        }

        private void OnGameStateUpdated(object? sender, ClientGameStateDto gameState)
        {
            Dispatcher.Invoke(() =>
            {
                // Если игра началась, переходим к игровому окну
                if (gameState.State == GameState.PlayerTurn ||
                    gameState.State == GameState.Initializing)
                {
                    _updateTimer.Stop();
                    var gameWindow = new GameWindow(_gameService, _playerName);
                    gameWindow.Show();
                    this.Close();
                }
            });
        }

        private void AddMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _messages.AppendLine($"[{timestamp}] {message}");
            MessagesText.Text = _messages.ToString();

            // Прокрутка вниз
            MessagesPanel.Visibility = Visibility.Visible;
        }

        private void AnimateButton(Button button)
        {
            var scaleAnimation = new DoubleAnimation
            {
                To = 0.95,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };
            button.RenderTransform = new ScaleTransform(1, 1);
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            button.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
    }
}