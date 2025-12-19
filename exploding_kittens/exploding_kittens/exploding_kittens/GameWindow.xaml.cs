using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace exploding_kittens
{
    public partial class GameWindow : Window
    {
        public GameWindow()
        {
            InitializeComponent();
            Loaded += GameWindow_Loaded;
        }

        private void GameWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            this.BeginAnimation(OpacityProperty, fadeIn);
            SetupPlayers();
        }

        private void SetupPlayers()
        {
            string mainPlayerName = GameSettings.PlayerName ?? "Игрок1";
            int playersCount = GameSettings.PlayersCount;
            CurrentPlayerLabel.Text = mainPlayerName;
            PlayersCountLabel.Text = playersCount.ToString();

            if (playersCount < 2)
            {
                TopPlayerPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                TopPlayerName.Text = $"Игрок{2}";
            }

            if (playersCount < 3)
            {
                LeftPlayerPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                LeftPlayerName.Text = $"Игрок{3}";
            }

            if (playersCount < 4)
            {
                RightPlayerPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                RightPlayerName.Text = $"Игрок{4}";
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            fadeOut.Completed += (s, args) =>
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            };

            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Escape)
            {
                ExitButton_Click(null, null);
            }
        }
    }
}