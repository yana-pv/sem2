using ClientWPF.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClientWPF
{
    public partial class LobbyWindow : Window
    {
        private readonly GameClientService _gameService;
        private readonly string _playerName;

        public LobbyWindow(GameClientService gameService, string playerName)
        {
            InitializeComponent();
            _gameService = gameService;
            _playerName = playerName;

            // Подписываемся на события
            _gameService.MessageReceived += OnMessageReceived;
            _gameService.GameCreated += OnGameCreated;
        }

        private void CreateGameButton_Click(object sender, RoutedEventArgs e)
        {
            // Анимация
            AnimateButton(CreateGameButton);

            // Переход к окну создания игры
            var createGameWindow = new CreateGameWindow(_gameService, _playerName);
            createGameWindow.Show();
            this.Close();
        }

        private void JoinGameButton_Click(object sender, RoutedEventArgs e)
        {
            // Анимация
            AnimateButton(JoinGameButton);

            // Переход к окну списка игр
            var joinGameWindow = new JoinGameWindow(_gameService, _playerName);
            joinGameWindow.Show();
            this.Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Возврат к подключению
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
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

        private void OnMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusPanel.Visibility = Visibility.Visible;
                StatusText.Text = message;
            });
        }

        private void OnGameCreated(object? sender, System.EventArgs e)
        {
            // Если игра создана, переходим к ожиданию игроков
            // Это будет обработано в CreateGameWindow
        }
    }
}