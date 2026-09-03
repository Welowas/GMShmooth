using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GMSmooth
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Random Rand;
        private readonly DispatcherTimer _animationTimer;
        private readonly double _animationTick;
        private MediaPlayer _mediaPlayer;
        private List<Rectangle> _rectangles;
        private Color[] _rectangleColors;
        private byte _currentRectangleColor;
        private List<double> _rectanglesX;
        private List<double> _rectanglesY;
        private List<double> _rectanglesAngle;
        private uint _rectangleSpawnDelay;
        private uint _currentRectangleSpawnDelay;

        public MainWindow()
        {
            InitializeComponent();

            Rand = new Random();

            _animationTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _animationTick = 1000 / 60d;
            _animationTimer.Interval = TimeSpan.FromMilliseconds(_animationTick);
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Open(new Uri("rustybeacch.mp3", UriKind.Relative));
            _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            _mediaPlayer.Play();

            _rectangles = new List<Rectangle>();
            _rectangleColors = new Color[8];
            _rectangleColors[0] = Color.FromArgb(255, 184, 245, 232);
            _rectangleColors[1] = Color.FromArgb(255, 184, 237, 232);
            _rectangleColors[2] = Color.FromArgb(255, 184, 216, 232);
            _rectangleColors[3] = Color.FromArgb(255, 184, 195, 232);
            _rectangleColors[4] = Color.FromArgb(255, 184, 187, 232);
            _rectangleColors[5] = Color.FromArgb(255, 184, 195, 232);
            _rectangleColors[6] = Color.FromArgb(255, 184, 216, 232);
            _rectangleColors[7] = Color.FromArgb(255, 184, 237, 232);
            _currentRectangleColor = 0;
            _rectanglesX = new List<double>();
            _rectanglesY = new List<double>();
            _rectanglesAngle = new List<double>();
            _rectangleSpawnDelay = 60;
            _currentRectangleSpawnDelay = 0;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (_currentRectangleSpawnDelay == 0)
            {
                _rectangles.Add(new Rectangle
                {
                    Height = 0,
                    Fill = new SolidColorBrush(_rectangleColors[_currentRectangleColor]),
                    Width = 0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                _rectanglesX.Add(_bgCanvas.ActualWidth / 2);
                _rectanglesY.Add(_bgCanvas.ActualHeight / 2);
                _rectanglesAngle.Add(0);
                Canvas.SetLeft(_rectangles[_rectangles.Count - 1], _rectanglesX[_rectangles.Count - 1]);
                Canvas.SetTop(_rectangles[_rectangles.Count - 1], _rectanglesY[_rectangles.Count - 1]);
                Panel.SetZIndex(_rectangles[_rectangles.Count - 1], -1);
                _bgCanvas.Children.Add(_rectangles[_rectangles.Count - 1]);
                if (_currentRectangleColor < _rectangleColors.Length - 1)
                {
                    _currentRectangleColor++;
                }
                else
                {
                    _currentRectangleColor = 0;
                }

                _currentRectangleSpawnDelay = _rectangleSpawnDelay;
            }
            else
            {
                _currentRectangleSpawnDelay--;
            }

            for (int i = 0; i < _rectangles.Count; i++)
            {
                double xGrowth = 15;
                double yGrowth = 10;
                _rectanglesX[i] -= xGrowth;
                _rectangles[i].Width += xGrowth * 2;
                _rectanglesY[i] -= yGrowth;
                _rectangles[i].Height += yGrowth * 2;
                _rectanglesAngle[i] += 1;
                Canvas.SetLeft(_rectangles[i], _rectanglesX[i]);
                Canvas.SetTop(_rectangles[i], _rectanglesY[i]);

                _rectangles[i].RenderTransform = new RotateTransform(_rectanglesAngle[i], _rectangles[i].Width / 2, _rectangles[i].Height / 2);
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            _mediaPlayer.Position = TimeSpan.Zero;
            _mediaPlayer.Play();
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_muteLine.Visibility == Visibility.Visible)
            {
                _mediaPlayer.Play();
                _muteLine.Visibility = Visibility.Hidden;
            }
            else
            {
                _mediaPlayer.Stop();
                _muteLine.Visibility = Visibility.Visible;
            }
        }

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Both types (*.exe;*.win)|*.exe;*.win|GMS1/2 games (*.exe)|*.exe|data.win files (*.win)|*.win",
                InitialDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory.GetCurrentDirectory(), @"..\..\"))
            };
            if (openFileDialog.ShowDialog() == true)
            {
                string fileType = openFileDialog.FileName.Substring(openFileDialog.FileName.LastIndexOf('.'));
                if (fileType == ".exe")
                {
                    _chooseFileButton.Content = openFileDialog.FileName.Substring(0, openFileDialog.FileName.LastIndexOf(System.IO.Path.DirectorySeparatorChar));
                }
                else if (fileType == ".win")
                {

                }
                else
                {
                    throw new Exception("Invalid file type.");
                }
            }
        }
    }
}
