using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MovieRecommendation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Data data = new Data();
        DispatcherTimer refreshTimer = new DispatcherTimer(DispatcherPriority.Render);

        int userID;
        bool useHash;

        #region Events
        public MainWindow()
        {
            InitializeComponent();

            refreshTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            data.MaxDegreeOfParallelism = 4;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            progressBar.Maximum = data.ProgressMaximum;
            progressBar.Value = data.ProgressPercent;
            progressLabel.Content = data.ProgressState;
        }

        private async void RateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(UserID.Text, out userID))
            {
                MessageBox.Show("Your userID must be an integer number.");
                return;
            }

            if (data.GetUser(userID).MoviesWatched().Count() == 0)
            {
                MessageBox.Show("There is no data about this user.");
                return;
            }

            IsEnabled = false;
            useHash = hashCheckbox.IsChecked.Value;
            DateTime t = DateTime.Now;

            IOrderedEnumerable<KeyValuePair<int, float>> orderedResult = null;
            await Task.Factory.StartNew(() =>
            {
                var result = data.RecommendMovieByRates(userID, useHash);
                orderedResult = result.OrderByDescending(order => order.Value);
            });

            MovieList.Items.Clear();
            for (int i = 0; i < 5; i++)
            {
                var movie = orderedResult.ElementAt(i);
                MovieList.Items.Add(string.Format("{0} - {1} - Genres: {2} - Predicted Rate: {3}",
                    movie.Key, data.GetMovie(movie.Key).name, data.GetMovieGenresString(movie.Key), (movie.Value + data.GetUser(userID).average).ToString("F2")));
            }
            timeLabel.Content = DateTime.Now - t;
            IsEnabled = true;
        }

        private async void MovieButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(UserID.Text, out userID))
            {
                MessageBox.Show("Your userID must be an integer number.");
                return;
            }

            if (data.GetUser(userID).MoviesWatched().Count() == 0)
            {
                MessageBox.Show("There is no data about this user.");
                return;
            }

            IsEnabled = false;
            useHash = hashCheckbox.IsChecked.Value;
            DateTime t = DateTime.Now;

            IOrderedEnumerable<KeyValuePair<int, uint>> orderedResult = null;
            await Task.Factory.StartNew(() =>
            {
                var result = data.RecommendMovieByGenre(userID);
                orderedResult = result.OrderBy(order => order.Value);
            });

            MovieList.Items.Clear();
            for (int i = 0; i < 5; i++)
            {
                var movie = orderedResult.ElementAt(i);
                MovieList.Items.Add(string.Format("{0} - {1} - Genres: {2} - Distance: {3}",
                    movie.Key, data.GetMovie(movie.Key).name, data.GetMovieGenresString(movie.Key), movie.Value));
            }
            timeLabel.Content = DateTime.Now - t;
            IsEnabled = true;
        }

        private async void MovieRateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(UserID.Text, out userID))
            {
                MessageBox.Show("Your userID must be an integer number.");
                return;
            }

            if (data.GetUser(userID).MoviesWatched().Count() == 0)
            {
                MessageBox.Show("There is no data about this user.");
                return;
            }

            IsEnabled = false;
            useHash = hashCheckbox.IsChecked.Value;
            DateTime t = DateTime.Now;

            IOrderedEnumerable<KeyValuePair<int, float>> orderedResult = null;
            await Task.Factory.StartNew(() =>
            {
                var result = data.RecommendMovieByRatesGenres(userID, useHash);
                orderedResult = result.OrderByDescending(order => order.Value);
            });

            MovieList.Items.Clear();
            for (int i = 0; i < 5; i++)
            {
                var movie = orderedResult.ElementAt(i);
                MovieList.Items.Add(string.Format("{0} - {1} - Genres: {2} - Predicted Rate: {3}",
                    movie.Key, data.GetMovie(movie.Key).name, data.GetMovieGenresString(movie.Key), (movie.Value + data.GetUser(userID).average).ToString("F2")));
            }
            timeLabel.Content = DateTime.Now - t;
            IsEnabled = true;
        }
        #endregion                                

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Factory.StartNew(() =>
            {
                data.ReadRatingFile("ratings.csv");
                data.ReadMovieFile("movies.csv");
                data.NormalizeRates();
                data.CalculateRateHashes();
                data.CalculatePredictionErrors(0.0001, true); // select possibility 1 out of 10,000
                //data.PreprocessSimularities();
            });
            IsEnabled = true;
        }
    }
}
