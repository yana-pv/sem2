using ClientWPF.Services;
using Common.Enums;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClientWPF
{
    public partial class GameWindow : Window
    {
        private readonly GameClientService _gameService;
        private readonly string _playerName;
        private DispatcherTimer _gameTimer;
        private int _timeLeft = 30; // 30 секунд на ход
        private Card? _selectedCard = null;
        private Border? _selectedCardBorder = null;
        private ClientGameStateDto? _currentGameState;
        private List<Card> _myHand = new();
        private readonly Dictionary<Guid, PlayerInfoDto> _playerInfos = new();
        private readonly StringBuilder _gameLog = new();
        private bool _isMyTurn = false;

        // Поля для комбинаций
        private List<int> _selectedComboCards = new();
        private List<Border> _selectedComboBorders = new();
        private ComboType? _selectedComboType = null;

        private enum ComboType
        {
            TwoOfAKind = 2,      // 2 одинаковые карты
            ThreeOfAKind = 3,    // 3 одинаковые карты  
            FiveDifferent = 5    // 5 разных карт
        }

        public GameWindow(GameClientService gameService, string playerName)
        {
            InitializeComponent();
            _gameService = gameService;
            _playerName = playerName;

            // Подписываемся на события
            _gameService.MessageReceived += OnMessageReceived;
            _gameService.HandUpdated += OnHandUpdated;
            _gameService.GameStateUpdated += OnGameStateUpdated;
            _gameService.Disconnected += OnDisconnected;

            // Настраиваем таймер игры
            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _gameTimer.Tick += GameTimer_Tick;

            // Инициализируем кнопку комбо
            PlayComboButton.IsEnabled = false;
        }

        private void GameWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            this.BeginAnimation(OpacityProperty, fadeIn);

            // Добавляем первое сообщение в лог
            AddToLog($"🎮 Добро пожаловать в игру, {_playerName}!");
            AddToLog("⏳ Запрос состояния игры...");

            // Запрашиваем состояние игры и руку игрока
            _ = RequestGameStateAsync();
        }

        private async Task RequestGameStateAsync()
        {
            try
            {
                if (_gameService.GameId.HasValue)
                {
                    await _gameService.GetGameStateAsync(_gameService.GameId.Value);
                    await _gameService.GetPlayerHandAsync(_gameService.GameId.Value);
                }
            }
            catch (Exception ex)
            {
                AddToLog($"❌ Ошибка запроса состояния: {ex.Message}");
            }
        }

        private void SetupPlayersLayout(int playersCount)
        {
            // Показываем/скрываем панели игроков в зависимости от количества
            TopPlayerPanel.Visibility = playersCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
            LeftPlayerPanel.Visibility = playersCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
            RightPlayerPanel.Visibility = playersCount >= 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayersDisplay(ClientGameStateDto gameState)
        {
            if (gameState.Players == null) return;

            // Обновляем информацию об игроках
            _playerInfos.Clear();
            foreach (var player in gameState.Players)
            {
                _playerInfos[player.Id] = player;
            }

            // Обновляем счетчики
            PlayersCountLabel.Text = gameState.AlivePlayers.ToString();
            DeckCountLabel.Text = gameState.CardsInDeck.ToString();
            TurnNumberLabel.Text = $"#{gameState.TurnsPlayed + 1}";
            DeckCountText.Text = gameState.CardsInDeck.ToString();

            // Определяем, чей сейчас ход
            _isMyTurn = gameState.CurrentPlayerName == _playerName;
            CurrentPlayerLabel.Text = gameState.CurrentPlayerName ?? "Ожидание...";

            // Обновляем инструкции
            if (_isMyTurn)
            {
                TurnInstructions.Text = "Ваш ход! Выберите карту для игры или возьмите карту из колоды.";
                DrawCardButton.IsEnabled = true;
            }
            else
            {
                TurnInstructions.Text = $"Ходит {gameState.CurrentPlayerName}. Ожидайте своего хода.";
                DrawCardButton.IsEnabled = false;
                PlaySelectedCardButton.IsEnabled = false;
                PlayComboButton.IsEnabled = false;
            }

            // Обновляем информацию об игроках на панелях
            UpdatePlayerPanels(gameState);
        }

        private void UpdatePlayerPanels(ClientGameStateDto gameState)
        {
            if (gameState.Players == null) return;

            // Получаем других игроков (кроме текущего)
            var otherPlayers = gameState.Players
                .Where(p => p.Name != _playerName && p.IsAlive)
                .OrderBy(p => p.TurnOrder)
                .ToList();

            // Сбрасываем все панели
            TopPlayerPanel.Visibility = Visibility.Collapsed;
            LeftPlayerPanel.Visibility = Visibility.Collapsed;
            RightPlayerPanel.Visibility = Visibility.Collapsed;

            // Распределяем игроков по позициям
            for (int i = 0; i < Math.Min(otherPlayers.Count, 3); i++)
            {
                var player = otherPlayers[i];
                string emoji = player.IsCurrentPlayer ? "👑" : GetPlayerEmoji(player.Id);

                switch (i)
                {
                    case 0: // Верхний игрок
                        TopPlayerPanel.Visibility = Visibility.Visible;
                        TopPlayerName.Text = player.Name;
                        TopPlayerCards.Text = $"{player.CardCount} карт";
                        TopPlayerEmoji.Text = emoji;
                        UpdatePlayerCardsDisplay(TopPlayerCardsPanel, player.CardCount);
                        break;

                    case 1: // Левый игрок
                        LeftPlayerPanel.Visibility = Visibility.Visible;
                        LeftPlayerName.Text = player.Name;
                        LeftPlayerCards.Text = $"{player.CardCount} карт";
                        LeftPlayerEmoji.Text = emoji;
                        UpdatePlayerCardsDisplay(LeftPlayerCardsPanel, player.CardCount);
                        break;

                    case 2: // Правый игрок
                        RightPlayerPanel.Visibility = Visibility.Visible;
                        RightPlayerName.Text = player.Name;
                        RightPlayerCards.Text = $"{player.CardCount} карт";
                        RightPlayerEmoji.Text = emoji;
                        UpdatePlayerCardsDisplay(RightPlayerCardsPanel, player.CardCount);
                        break;
                }
            }
        }

        private string GetPlayerEmoji(Guid playerId)
        {
            // Простая функция для генерации эмодзи на основе ID
            string[] emojis = { "😺", "😸", "😹", "😻", "😼", "😽", "🙀", "😿", "😾", "🐱" };
            int index = Math.Abs(playerId.GetHashCode()) % emojis.Length;
            return emojis[index];
        }

        private void UpdatePlayerCardsDisplay(WrapPanel panel, int cardCount)
        {
            panel.Children.Clear();

            for (int i = 0; i < Math.Min(cardCount, 10); i++) // Показываем максимум 10 карт
            {
                var cardBorder = new Border
                {
                    Width = 35,
                    Height = 49,
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(74, 20, 140)),
                    BorderThickness = new Thickness(2)
                };

                // Загружаем изображение рубашки
                try
                {
                    var bitmap = new BitmapImage(new Uri("pack://application:,,,/Resources/Shirt.png"));
                    cardBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
                }
                catch
                {
                    cardBorder.Background = Brushes.DarkRed;
                }

                panel.Children.Add(cardBorder);
            }

            // Если карт больше 10, показываем счетчик
            if (cardCount > 10)
            {
                var countText = new TextBlock
                {
                    Text = $"+{cardCount - 10}",
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                panel.Children.Add(countText);
            }
        }

        private void UpdateMyHandDisplay()
        {
            MyCardsPanel.Children.Clear();
            MyCardsCount.Text = $"{_myHand.Count} шт.";

            for (int i = 0; i < _myHand.Count; i++)
            {
                var card = _myHand[i];
                var cardBorder = CreateCardBorder(card, i);
                MyCardsPanel.Children.Add(cardBorder);
            }

            // Прокручиваем к началу
            MyCardsScrollViewer.ScrollToLeftEnd();
        }

        private Border CreateCardBorder(Card card, int index)
        {
            var cardBorder = new Border
            {
                Width = 120,
                Height = 168,
                Margin = new Thickness(8),
                Cursor = Cursors.Hand,
                Tag = index,
                CornerRadius = new CornerRadius(8),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2)
            };

            // Загружаем изображение карты
            try
            {
                string imageName = GetCardImageName(card);
                var bitmap = new BitmapImage(new Uri($"pack://application:,,,/Resources/{imageName}.png"));
                cardBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            }
            catch
            {
                // Если изображение не найдено, создаем цветной фон
                cardBorder.Background = GetCardColor(card.Type);
                var textBlock = new TextBlock
                {
                    Text = card.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                cardBorder.Child = textBlock;
            }

            // Обработчики событий
            cardBorder.MouseEnter += (s, e) =>
            {
                if (!_selectedComboCards.Contains(index) && _selectedCardBorder != cardBorder)
                {
                    cardBorder.BorderBrush = Brushes.Gold;
                    cardBorder.RenderTransform = new ScaleTransform(1.05, 1.05);
                }
            };

            cardBorder.MouseLeave += (s, e) =>
            {
                if (!_selectedComboCards.Contains(index) && _selectedCardBorder != cardBorder)
                {
                    cardBorder.BorderBrush = Brushes.Transparent;
                    cardBorder.RenderTransform = null;
                }
            };

            cardBorder.MouseLeftButtonDown += (s, e) =>
            {
                SelectCard(card, cardBorder, index);
            };

            return cardBorder;
        }

        private string GetCardImageName(Card card)
        {
            return card.Type switch
            {
                CardType.Attack => "attack1",
                CardType.Skip => "skip1",
                CardType.Defuse => "defuse1",
                CardType.Nope => "no1",
                CardType.Shuffle => "shuffle1",
                CardType.SeeTheFuture => "future1",
                CardType.RainbowCat => "rainbowcat",
                CardType.BeardCat => "borodach",
                CardType.PotatoCat => "potatocat",
                CardType.WatermelonCat => "watermelon",
                CardType.TacoCat => "tacocat",
                CardType.ExplodingKitten => "exploding_kitten1",
                CardType.Favor => "borrow1",
                _ => "Shirt"
            };
        }

        private SolidColorBrush GetCardColor(CardType type)
        {
            return type switch
            {
                CardType.ExplodingKitten => new SolidColorBrush(Colors.DarkRed),
                CardType.Defuse => new SolidColorBrush(Colors.LightGreen),
                CardType.Nope => new SolidColorBrush(Colors.LightBlue),
                CardType.Attack => new SolidColorBrush(Colors.OrangeRed),
                CardType.Skip => new SolidColorBrush(Colors.LightYellow),
                CardType.Favor => new SolidColorBrush(Colors.Pink),
                CardType.Shuffle => new SolidColorBrush(Colors.Violet),
                CardType.SeeTheFuture => new SolidColorBrush(Colors.LightCyan),
                _ => new SolidColorBrush(Colors.LightGray)
            };
        }

        private void SelectCard(Card card, Border cardBorder, int index)
        {
            if (!_isMyTurn)
            {
                AddToLog("❌ Сейчас не ваш ход!");
                return;
            }

            // Если нажата Ctrl - выбираем для комбо
            bool isComboSelection = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (isComboSelection)
            {
                ToggleComboCardSelection(card, cardBorder, index);
            }
            else
            {
                SelectSingleCard(card, cardBorder, index);
            }
        }

        private void SelectSingleCard(Card card, Border cardBorder, int index)
        {
            // Сбрасываем выбор комбо если был
            if (_selectedComboCards.Count > 0)
            {
                ResetComboSelection();
            }

            // Снимаем выделение с предыдущей карты
            if (_selectedCardBorder != null)
            {
                _selectedCardBorder.BorderBrush = Brushes.Transparent;
                _selectedCardBorder.RenderTransform = null;
            }

            // Выделяем новую карту
            _selectedCard = card;
            _selectedCardBorder = cardBorder;
            cardBorder.BorderBrush = Brushes.Gold;
            cardBorder.BorderThickness = new Thickness(3);
            cardBorder.RenderTransform = new ScaleTransform(1.1, 1.1);

            // Обновляем кнопку
            PlaySelectedCardButton.IsEnabled = true;
            PlayCardText.Text = $"СЫГРАТЬ: {card.Name}";

            AddToLog($"✅ Выбрана карта: {card.Name}");
        }

        private void ToggleComboCardSelection(Card card, Border cardBorder, int index)
        {
            // Сбрасываем одиночный выбор если был
            if (_selectedCardBorder != null)
            {
                _selectedCardBorder.BorderBrush = Brushes.Transparent;
                _selectedCardBorder.RenderTransform = null;
                _selectedCard = null;
                _selectedCardBorder = null;
                PlaySelectedCardButton.IsEnabled = false;
            }

            if (_selectedComboCards.Contains(index))
            {
                // Убираем из выбора
                _selectedComboCards.Remove(index);
                _selectedComboBorders.Remove(cardBorder);

                cardBorder.BorderBrush = Brushes.Transparent;
                cardBorder.RenderTransform = null;

                AddToLog($"❌ Карта удалена из комбо: {card.Name}");
            }
            else
            {
                // Добавляем в выбор
                if (_selectedComboCards.Count >= 5)
                {
                    AddToLog("❌ Можно выбрать максимум 5 карт для комбо!");
                    return;
                }

                _selectedComboCards.Add(index);
                _selectedComboBorders.Add(cardBorder);

                cardBorder.BorderBrush = Brushes.Orange;
                cardBorder.BorderThickness = new Thickness(3);
                cardBorder.RenderTransform = new ScaleTransform(1.05, 1.05);

                AddToLog($"✅ Карта добавлена в комбо: {card.Name} ({_selectedComboCards.Count}/5)");
            }

            // Обновляем кнопку комбо
            UpdateComboButtonState();
        }

        private void UpdateComboButtonState()
        {
            if (_selectedComboCards.Count >= 2)
            {
                PlayComboButton.IsEnabled = true;

                // Определяем возможный тип комбо
                var comboType = DetermineComboType(_selectedComboCards);
                if (comboType.HasValue)
                {
                    PlayComboButton.Content = $"СЫГРАТЬ КОМБО ({_selectedComboCards.Count})";

                    // Показываем подсказку
                    string comboHint = comboType.Value switch
                    {
                        ComboType.TwoOfAKind => "2 одинаковые: кража случайной карты",
                        ComboType.ThreeOfAKind => "3 одинаковые: запрос конкретной карты",
                        ComboType.FiveDifferent => "5 разных: взять карту из сброса",
                        _ => "Неизвестная комбинация"
                    };

                    PlayComboButton.ToolTip = comboHint;
                }
                else
                {
                    PlayComboButton.Content = "НЕВЕРНАЯ КОМБИНАЦИЯ";
                    PlayComboButton.IsEnabled = false;
                }
            }
            else
            {
                PlayComboButton.IsEnabled = false;
                PlayComboButton.Content = "СЫГРАТЬ КОМБО";
                PlayComboButton.ToolTip = "Зажмите Ctrl и выберите от 2 до 5 карт для комбо";
            }
        }

        private ComboType? DetermineComboType(List<int> cardIndices)
        {
            if (cardIndices.Count < 2 || cardIndices.Count > 5)
                return null;

            var cards = cardIndices.Select(i => _myHand[i]).ToList();

            // Проверяем 2 одинаковые карты
            if (cardIndices.Count == 2)
            {
                if (cards[0].Type == cards[1].Type ||
                    cards[0].IconId == cards[1].IconId)
                {
                    return ComboType.TwoOfAKind;
                }
            }

            // Проверяем 3 одинаковые карты
            else if (cardIndices.Count == 3)
            {
                if ((cards[0].Type == cards[1].Type && cards[1].Type == cards[2].Type) ||
                    (cards[0].IconId == cards[1].IconId && cards[1].IconId == cards[2].IconId))
                {
                    return ComboType.ThreeOfAKind;
                }
            }

            // Проверяем 5 разных карт
            else if (cardIndices.Count == 5)
            {
                if (cards.Select(c => c.IconId).Distinct().Count() == 5)
                {
                    return ComboType.FiveDifferent;
                }
            }

            return null;
        }

        private async void PlayComboButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMyTurn || _selectedComboCards.Count == 0 || !_gameService.GameId.HasValue)
            {
                AddToLog("❌ Не выбраны карты для комбо!");
                return;
            }

            try
            {
                // Определяем тип комбо
                var comboType = DetermineComboType(_selectedComboCards);

                if (!comboType.HasValue)
                {
                    AddToLog("❌ Выбранные карты не образуют допустимую комбинацию!");
                    ResetComboSelection();
                    return;
                }

                _selectedComboType = comboType.Value;

                // Для разных типов комбо разная логика
                string cardIndicesStr = string.Join(",", _selectedComboCards.OrderBy(i => i));
                string payload = "";

                switch (comboType.Value)
                {
                    case ComboType.TwoOfAKind:
                        var target1 = await SelectTargetPlayer("Выберите игрока для кражи карты:");
                        if (target1 == null)
                        {
                            AddToLog("❌ Комбо отменено: не выбран игрок");
                            ResetComboSelection();
                            return;
                        }
                        payload = $"{_gameService.GameId}:{_gameService.PlayerId}:2:{cardIndicesStr}:{target1.Id}";
                        AddToLog($"✨ Комбо: кража случайной карты у {target1.Name}");
                        break;

                    case ComboType.ThreeOfAKind:
                        var target2 = await SelectTargetPlayer("Выберите игрока для запроса карты:");
                        if (target2 == null)
                        {
                            AddToLog("❌ Комбо отменено: не выбран игрок");
                            ResetComboSelection();
                            return;
                        }
                        var cardName = await SelectCardName(target2);
                        if (string.IsNullOrEmpty(cardName))
                        {
                            AddToLog("❌ Комбо отменено: не выбрана карта");
                            ResetComboSelection();
                            return;
                        }
                        payload = $"{_gameService.GameId}:{_gameService.PlayerId}:3:{cardIndicesStr}:{target2.Id}|{cardName}";
                        AddToLog($"✨ Комбо: запрос карты '{cardName}' у {target2.Name}");
                        break;

                    case ComboType.FiveDifferent:
                        payload = $"{_gameService.GameId}:{_gameService.PlayerId}:5:{cardIndicesStr}";
                        AddToLog("✨ Комбо: выбор карты из сброса");
                        break;
                }

                // Отправляем команду
                await _gameService.SendCommandAsync(Command.UseCombo, payload);
                ResetComboSelection();

            }
            catch (Exception ex)
            {
                AddToLog($"❌ Ошибка при игре комбо: {ex.Message}");
                ResetComboSelection();
            }
        }

        private async Task<PlayerInfoDto?> SelectTargetPlayer(string prompt)
        {
            if (_currentGameState?.Players == null)
                return null;

            var otherPlayers = _currentGameState.Players
                .Where(p => p.Name != _playerName && p.IsAlive)
                .ToList();

            if (otherPlayers.Count == 0)
                return null;

            // В реальной игре здесь должно быть модальное окно выбора
            // Пока берем первого доступного игрока
            return otherPlayers.FirstOrDefault();
        }

        private async Task<string> SelectCardName(PlayerInfoDto targetPlayer)
        {
            // В реальной игре здесь должно быть модальное окно с выбором карты
            // Пока возвращаем заглушку
            return "Любая карта";
        }

        private void ResetComboSelection()
        {
            foreach (var border in _selectedComboBorders)
            {
                border.BorderBrush = Brushes.Transparent;
                border.RenderTransform = null;
            }

            _selectedComboCards.Clear();
            _selectedComboBorders.Clear();
            _selectedComboType = null;

            PlayComboButton.IsEnabled = false;
            PlayComboButton.Content = "СЫГРАТЬ КОМБО";

            AddToLog("🔄 Выбор комбо сброшен");
        }

        private async void DrawCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMyTurn || !_gameService.GameId.HasValue)
            {
                AddToLog("❌ Сейчас не ваш ход!");
                return;
            }

            try
            {
                DrawCardButton.IsEnabled = false;
                AddToLog("🎴 Беру карту из колоды...");

                await _gameService.DrawCardAsync(_gameService.GameId.Value);
            }
            catch (Exception ex)
            {
                AddToLog($"❌ Ошибка: {ex.Message}");
                DrawCardButton.IsEnabled = true;
            }
        }

        private async void PlaySelectedCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMyTurn || _selectedCard == null || !_gameService.GameId.HasValue)
            {
                AddToLog("❌ Не выбрана карта или не ваш ход!");
                return;
            }

            try
            {
                // Находим индекс выбранной карты
                int cardIndex = _myHand.IndexOf(_selectedCard);
                if (cardIndex == -1)
                {
                    AddToLog("❌ Карта не найдена в руке!");
                    return;
                }

                AddToLog($"🎯 Играю карту: {_selectedCard.Name}");

                // В зависимости от типа карты可能需要 дополнительная информация
                string additionalData = "";
                if (_selectedCard.Type == CardType.Favor ||
                    _selectedCard.Type == CardType.Attack)
                {
                    // Для этих карт нужно выбрать цель
                    additionalData = GetDefaultTarget();
                }

                await _gameService.PlayCardAsync(_gameService.GameId.Value, cardIndex, additionalData);

                // Сбрасываем выделение
                if (_selectedCardBorder != null)
                {
                    _selectedCardBorder.BorderBrush = Brushes.Transparent;
                    _selectedCardBorder.RenderTransform = null;
                }
                _selectedCard = null;
                _selectedCardBorder = null;
                PlaySelectedCardButton.IsEnabled = false;

            }
            catch (Exception ex)
            {
                AddToLog($"❌ Ошибка: {ex.Message}");
            }
        }

        private string GetDefaultTarget()
        {
            // Упрощенная версия - выбираем первого другого игрока
            if (_currentGameState?.Players != null)
            {
                var otherPlayer = _currentGameState.Players
                    .FirstOrDefault(p => p.Name != _playerName && p.IsAlive);

                if (otherPlayer != null)
                {
                    return otherPlayer.Id.ToString();
                }
            }
            return "";
        }

        private void Deck_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isMyTurn)
            {
                DrawCardButton_Click(sender, e);
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };

            var exitItem = new MenuItem
            {
                Header = "Выйти в меню",
                FontSize = 14,
                Icon = new TextBlock { Text = "🚪", FontSize = 16 }
            };
            exitItem.Click += (s, args) => ExitToMenu();

            var rulesItem = new MenuItem
            {
                Header = "Правила игры",
                FontSize = 14,
                Icon = new TextBlock { Text = "📖", FontSize = 16 }
            };
            rulesItem.Click += (s, args) => ShowRules();

            var soundItem = new MenuItem
            {
                Header = "Звук: Вкл",
                FontSize = 14,
                Icon = new TextBlock { Text = "🔊", FontSize = 16 }
            };
            soundItem.Click += (s, args) => ToggleSound(soundItem);

            contextMenu.Items.Add(exitItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(rulesItem);
            contextMenu.Items.Add(soundItem);

            contextMenu.IsOpen = true;
        }

        private void ExitToMenu()
        {
            var result = MessageBox.Show("Вы уверены, что хотите выйти из игры?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _gameTimer.Stop();
                _gameService.Disconnect();

                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
        }

        private void ShowRules()
        {
            MessageBox.Show(
                "🎮 Взрывные Котята - Правила игры:\n\n" +
                "1. Цель игры - не вытянуть Взрывного Котенка\n" +
                "2. Используйте карты Обезвредить для защиты\n" +
                "3. Карты действий (Атака, Пропуск и др.) помогают в игре\n" +
                "4. Комбинации карт котов дают специальные эффекты:\n" +
                "   • 2 одинаковые: кража случайной карты\n" +
                "   • 3 одинаковые: запрос конкретной карты\n" +
                "   • 5 разных: взять карту из сброса\n" +
                "5. Последний выживший игрок побеждает!",
                "Правила игры",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ToggleSound(MenuItem soundItem)
        {
            if (soundItem.Header.ToString()?.Contains("Вкл") == true)
            {
                soundItem.Header = "Звук: Выкл";
                ((TextBlock)soundItem.Icon).Text = "🔇";
            }
            else
            {
                soundItem.Header = "Звук: Вкл";
                ((TextBlock)soundItem.Icon).Text = "🔊";
            }
        }

        private void ChatTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && ChatTextBox.Text.Trim().Length > 0)
            {
                AddToLog($"{_playerName}: {ChatTextBox.Text}");
                ChatTextBox.Clear();
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                MenuButton_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.C &&
                     (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                // Ctrl+C - сброс выбора комбо
                ResetComboSelection();
                AddToLog("🔄 Выбор комбо отменен (Ctrl+C)");
            }
            else if (e.Key == System.Windows.Input.Key.Space && _isMyTurn)
            {
                // Space - быстрое взятие карты
                if (DrawCardButton.IsEnabled)
                {
                    DrawCardButton_Click(sender, e);
                }
            }
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            if (_timeLeft > 0)
            {
                _timeLeft--;
                TimerLabel.Text = $"{_timeLeft:D2}";

                if (_timeLeft <= 10)
                {
                    TimerLabel.Foreground = Brushes.Red;

                    if (_timeLeft <= 5 && _timeLeft % 2 == 0)
                    {
                        TimerLabel.Opacity = 0.5;
                    }
                    else
                    {
                        TimerLabel.Opacity = 1;
                    }
                }
            }
            else
            {
                _gameTimer.Stop();
                AddToLog("⏰ Время на ход истекло!");

                if (_isMyTurn && DrawCardButton.IsEnabled)
                {
                    DrawCardButton_Click(null, null);
                }
            }
        }

        private void StartTurnTimer()
        {
            _timeLeft = 30;
            TimerLabel.Text = "30";
            TimerLabel.Foreground = Brushes.White;
            TimerLabel.Opacity = 1;
            _gameTimer.Start();
        }

        private void StopTurnTimer()
        {
            _gameTimer.Stop();
            TimerLabel.Text = "--:--";
        }

        private void AddToLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _gameLog.AppendLine($"[{timestamp}] {message}");
            GameLogText.Text = _gameLog.ToString();

            var scrollViewer = GetScrollViewer(GameLogText);
            scrollViewer?.ScrollToEnd();
        }

        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer) return (ScrollViewer)element;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }

        private void OnMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                AddToLog(message);

                if (message.Contains("Взрывной Котенок") || message.Contains("взрывной"))
                {
                    StartExplosionAnimation();
                }
            });
        }

        private void OnHandUpdated(object? sender, List<Card> hand)
        {
            Dispatcher.Invoke(() =>
            {
                _myHand = hand;
                UpdateMyHandDisplay();

                // Сбрасываем выбор карт при обновлении руки
                ResetCardSelection();
                ResetComboSelection();
            });
        }

        private void OnGameStateUpdated(object? sender, ClientGameStateDto gameState)
        {
            Dispatcher.Invoke(() =>
            {
                _currentGameState = gameState;
                UpdatePlayersDisplay(gameState);

                bool wasMyTurn = _isMyTurn;
                _isMyTurn = gameState.CurrentPlayerName == _playerName;

                if (_isMyTurn && !wasMyTurn)
                {
                    StartTurnTimer();
                    AddToLog($"🎮 Ваш ход! У вас {_timeLeft} секунд.");

                    DrawCardButton.IsEnabled = true;

                    ResetCardSelection();
                    ResetComboSelection();
                }
                else if (!_isMyTurn && wasMyTurn)
                {
                    StopTurnTimer();
                    AddToLog("✅ Ход завершен.");

                    DrawCardButton.IsEnabled = false;
                    PlaySelectedCardButton.IsEnabled = false;
                    PlayComboButton.IsEnabled = false;

                    ResetCardSelection();
                    ResetComboSelection();
                }

                UpdateDiscardPile(gameState);

                if (gameState.State == GameState.GameOver)
                {
                    HandleGameOver(gameState);
                }
            });
        }

        private void ResetCardSelection()
        {
            if (_selectedCardBorder != null)
            {
                _selectedCardBorder.BorderBrush = Brushes.Transparent;
                _selectedCardBorder.RenderTransform = null;
            }

            _selectedCard = null;
            _selectedCardBorder = null;
            PlaySelectedCardButton.IsEnabled = false;
        }

        private void UpdateDiscardPile(ClientGameStateDto gameState)
        {
            DiscardCountText.Text = "?";
        }

        private void HandleGameOver(ClientGameStateDto gameState)
        {
            StopTurnTimer();

            string message;
            if (gameState.WinnerName == _playerName)
            {
                message = "🎉 ПОБЕДА! Вы выиграли игру!";
                StartVictoryAnimation();
            }
            else if (!string.IsNullOrEmpty(gameState.WinnerName))
            {
                message = $"🏆 Игра окончена! Победитель: {gameState.WinnerName}";
            }
            else
            {
                message = "🏁 Игра окончена! Нет победителей.";
            }

            AddToLog(message);

            MessageBox.Show(message, "Игра окончена",
                MessageBoxButton.OK, MessageBoxImage.Information);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                ExitToMenu();
            };
            timer.Start();
        }

        private void OnDisconnected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Соединение с сервером потеряно", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ExitToMenu();
            });
        }

        private void StartExplosionAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };

            this.BeginAnimation(OpacityProperty, animation);
        }

        private void StartVictoryAnimation()
        {
            var rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever
            };

            var transform = new RotateTransform();
            CurrentPlayerLabel.RenderTransform = transform;
            transform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
        }
    }
}