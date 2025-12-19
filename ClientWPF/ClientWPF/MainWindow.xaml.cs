using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClientWPF.Services;

namespace ClientWPF
{
    public partial class MainWindow : Window
    {
        private readonly GameClientService _gameService;

        public MainWindow()
        {
            InitializeComponent();
            _gameService = new GameClientService();
            _gameService.Connected += OnConnected;
            _gameService.MessageReceived += OnMessageReceived;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Анимация кнопки
            var scaleAnimation = new DoubleAnimation
            {
                To = 0.95,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };
            ConnectButton.RenderTransform = new ScaleTransform(1, 1);
            ConnectButton.RenderTransformOrigin = new Point(0.5, 0.5);
            ConnectButton.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            ConnectButton.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);

            // Получаем данные
            string ip = IpTextBox.Text;
            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                port = 5001;
            }

            string nickname = NicknameTextBox.Text;
            if (string.IsNullOrWhiteSpace(nickname))
            {
                MessageBox.Show("Введите никнейм!");
                return;
            }

            // Показываем статус
            StatusPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Подключение...";
            ConnectButton.IsEnabled = false;

            try
            {
                // Подключаемся к серверу
                await _gameService.ConnectAsync(ip, port);

                // Сохраняем никнейм
                _gameService.PlayerName = nickname;

                StatusText.Text = "Успешно подключено!";

                // Переходим в лобби
                var lobbyWindow = new LobbyWindow(_gameService, nickname);
                lobbyWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
                ConnectButton.IsEnabled = true;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Успешно подключено!";
                StatusText.Foreground = Brushes.Green;
            });
        }

        private void OnMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                // Здесь можно показывать сообщения от сервера
                if (message.Contains("Ошибка") || message.Contains("ошибка"))
                {
                    StatusText.Text = message;
                    StatusText.Foreground = Brushes.Red;
                }
            });
        }
    }
}