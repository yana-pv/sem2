using ClientWPF.Services;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClientWPF
{
    public partial class JoinGameWindow : Window
    {
        private readonly GameClientService _gameService;
        private readonly string _playerName;
        private DispatcherTimer _statusTimer;
        private readonly ObservableCollection<GameSessionInfoDto> _games = new();
        private readonly StringBuilder _messages = new();
        private bool _isConnecting = false;

        public JoinGameWindow(GameClientService gameService, string playerName)
        {
            try
            {
                InitializeComponent();
                _gameService = gameService;
                _playerName = playerName;

                // Настройка таймера для скрытия статуса
                _statusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _statusTimer.Tick += StatusTimer_Tick;

                // Инициализация списка - используем AvailableGames из GameClientService
                GamesListView.ItemsSource = _gameService.AvailableGames;

                // Подписываемся на события
                SubscribeToEvents();

                // Обработчик загрузки окна
                Loaded += OnWindowLoaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации окна: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Анимация появления
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                this.BeginAnimation(OpacityProperty, fadeIn);

                AddMessage($"🎮 Игрок: {_playerName}");
                AddMessage("💡 Для подключения введите ID игры или выберите из списка");

                // Запрашиваем список игр при загрузке окна
                _ = LoadGamesListAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка загрузки: {ex.Message}", true);
            }
        }

        private async Task LoadGamesListAsync()
        {
            try
            {
                ShowStatus("🔄 Загрузка списка игр...", false);
                await _gameService.GetGamesListAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Ошибка загрузки списка игр: {ex.Message}", true);
            }
        }

        private void SubscribeToEvents()
        {
            _gameService.MessageReceived += OnMessageReceived;
            _gameService.GameStateUpdated += OnGameStateUpdated;
            _gameService.GameCreated += OnGameCreated;
            _gameService.PlayerJoined += OnPlayerJoined;
            _gameService.GameStarted += OnGameStarted;
            _gameService.Connected += OnConnected;
            _gameService.Disconnected += OnDisconnected;
            _gameService.GamesListUpdated += OnGamesListUpdated; // Новое событие
        }

        private void UnsubscribeFromEvents()
        {
            _gameService.MessageReceived -= OnMessageReceived;
            _gameService.GameStateUpdated -= OnGameStateUpdated;
            _gameService.GameCreated -= OnGameCreated;
            _gameService.PlayerJoined -= OnPlayerJoined;
            _gameService.GameStarted -= OnGameStarted;
            _gameService.Connected -= OnConnected;
            _gameService.Disconnected -= OnDisconnected;
            _gameService.GamesListUpdated -= OnGamesListUpdated;
        }

        private async void JoinByIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnecting)
            {
                ShowStatus("❌ Уже идет подключение", true);
                return;
            }

            string gameIdText = GameIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(gameIdText))
            {
                ShowStatus("❌ Введите ID игры", true);
                return;
            }

            if (!Guid.TryParse(gameIdText, out Guid gameId))
            {
                ShowStatus("❌ Неверный формат ID игры", true);
                return;
            }

            AddMessage($"🔗 Попытка подключиться к игре {gameId}...");
            await ConnectToGameAsync(gameId);
        }

        private async void JoinGameItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnecting) 
            {
                ShowStatus("❌ Уже идет подключение", true);
                return;
            }

            if (sender is Button button && button.Tag is string tag && Guid.TryParse(tag, out Guid gameId))
            {
                AddMessage($"✅ Выбрана игра {gameId}, подключаемся...");
                await ConnectToGameAsync(gameId);
            }
        }

        private async Task ConnectToGameAsync(Guid gameId)
        {
            try
            {
                _isConnecting = true;

                // Показываем панель ожидания
                WaitingPanel.Visibility = Visibility.Visible;
                MessagesPanel.Visibility = Visibility.Visible;
                JoinByIdButton.IsEnabled = false;
                RefreshButton.IsEnabled = false;

                AddMessage($"🔗 Подключение к игре {gameId}...");

                // Присоединяемся к игре
                await _gameService.JoinGameAsync(gameId, _playerName);

                AddMessage($"✅ Запрос на подключение отправлен!");
                AddMessage("⏳ Ожидание подтверждения от сервера...");

                WaitingStatusText.Text = "Подключение к игре...";

                // Ждем пока установится GameId (максимум 10 секунд)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var checkTask = Task.Run(async () =>
                {
                    while (_gameService.GameId == null || _gameService.GameId != gameId)
                    {
                        await Task.Delay(100);
                        if (!_isConnecting) return;
                    }
                });

                await Task.WhenAny(checkTask, timeoutTask);

                if (_gameService.GameId == null || _gameService.GameId != gameId)
                {
                    ShowStatus("❌ Не удалось подключиться к игре", true);
                    ResetConnectionState();
                }
                else
                {
                    AddMessage($"✅ Успешно подключились к игре {_gameService.GameId}!");
                    AddMessage("⏳ Игра скоро начнется, подождите");
                    WaitingStatusText.Text = "Игра скоро начнется, подождите";
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Ошибка подключения: {ex.Message}", true);
                ResetConnectionState();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnecting)
            {
                AnimateButton(RefreshButton);
                ShowStatus("🔄 Обновление списка игр...", false);
                await LoadGamesListAsync();
            }
        }

        private void CancelJoinButton_Click(object sender, RoutedEventArgs e)
        {
            ResetConnectionState();
            ShowStatus("❌ Подключение отменено", true);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ReturnToLobby();
        }

        private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GamesListView.SelectedItem is GameSessionInfoDto selectedGame)
            {
                GameIdTextBox.Text = selectedGame.Id.ToString();
                AddMessage($"📌 Выбрана игра: {selectedGame.Name} (ID: {selectedGame.Id})");
            }
        }

        // Обработчики событий от GameClientService
        private void OnMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage(message);

                if (message.Contains("присоединился") || message.Contains("Присоединился"))
                {
                    ShowStatus(message, false);
                }
                else if (message.Contains("Ошибка") || message.Contains("ошибка"))
                {
                    ShowStatus(message, true);
                }
            });
        }

        private void OnGameStateUpdated(object? sender, ClientGameStateDto gameState)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage($"📊 Обновлено состояние игры: {gameState.State}");

                // Если игра началась, переходим к игровому окну
                if (gameState.State == GameState.PlayerTurn ||
                    gameState.State == GameState.Initializing)
                {
                    NavigateToGameWindow();
                }
                else if (gameState.State == GameState.WaitingForPlayers)
                {
                    // Если мы в состоянии ожидания, значит подключение успешно
                    _isConnecting = false;
                    AddMessage("✅ Успешно подключились к игре!");
                    AddMessage("⏳ Ожидаем начала игры...");
                    WaitingStatusText.Text = "Ожидание начала игры...";
                }
            });
        }

        private void OnGameCreated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage("🎮 Игра создана!");
            });
        }

        private void OnPlayerJoined(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage("👤 Игрок присоединился к игре");
            });
        }

        private void OnGameStarted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage("🚀 Игра началась!");
                NavigateToGameWindow();
            });
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddMessage("✅ Подключено к серверу");
            });
        }

        private void OnDisconnected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _isConnecting = false;
                ShowStatus("❌ Соединение с сервером потеряно", true);
                ResetConnectionState();
            });
        }

        private void OnGamesListUpdated(object? sender, System.Collections.Generic.List<GameSessionInfoDto> gamesList)
        {
            Dispatcher.Invoke(() =>
            {
                GamesCountText.Text = $" ({gamesList.Count})";

                if (gamesList.Count == 0)
                {
                    ShowStatus("ℹ️ Нет доступных игр. Создайте новую игру!", false);
                }
                else
                {
                    ShowStatus($"✅ Загружено {gamesList.Count} игр", false);
                }
            });
        }

        private void NavigateToGameWindow()
        {
            Dispatcher.Invoke(() =>
            {
                _isConnecting = false;
                UnsubscribeFromEvents();

                var gameWindow = new GameWindow(_gameService, _playerName);
                gameWindow.Show();
                this.Close();
            });
        }

        private void ReturnToLobby()
        {
            Dispatcher.Invoke(() =>
            {
                _isConnecting = false;
                UnsubscribeFromEvents();

                var lobbyWindow = new LobbyWindow(_gameService, _playerName);
                lobbyWindow.Show();
                this.Close();
            });
        }

        private void ResetConnectionState()
        {
            Dispatcher.Invoke(() =>
            {
                _isConnecting = false;
                WaitingPanel.Visibility = Visibility.Collapsed;
                JoinByIdButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
            });
        }

        private void ShowStatus(string message, bool isError)
        {
            Dispatcher.Invoke(() =>
            {
                StatusPanel.Visibility = Visibility.Visible;
                StatusText.Text = message;
                StatusText.Foreground = isError ? Brushes.Red : Brushes.Green;

                // Запускаем таймер для скрытия статуса
                _statusTimer.Start();
            });
        }

        private void StatusTimer_Tick(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusPanel.Visibility = Visibility.Collapsed;
                _statusTimer.Stop();
            });
        }

        private void AddMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _messages.AppendLine($"[{timestamp}] {message}");

                // Ограничиваем количество сообщений
                if (_messages.Length > 1000)
                {
                    _messages.Clear();
                    _messages.AppendLine("[система] История сообщений очищена");
                }

                MessagesText.Text = _messages.ToString();
                MessagesPanel.Visibility = Visibility.Visible;
            });
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            UnsubscribeFromEvents();
            _statusTimer?.Stop();
        }
    }
}