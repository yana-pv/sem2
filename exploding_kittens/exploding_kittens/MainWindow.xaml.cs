using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace exploding_kittens
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
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

            var gameWindow = new GameWindow();
            gameWindow.Show();

            this.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}