using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MovieRecommendation
{
    class Data
    {
        private Dictionary<int, Movie> movies = new Dictionary<int, Movie>();
        private Dictionary<int, User> users = new Dictionary<int, User>();
        private List<string> genreTitles = new List<string>();
        private ParallelOptions parallelOptions = new ParallelOptions();

        public long ProgressPercent { get; private set; }
        public long ProgressMaximum { get; private set; }
        public string ProgressState { get; private set; }
        public int MaxDegreeOfParallelism { get { return parallelOptions.MaxDegreeOfParallelism; } set { parallelOptions.MaxDegreeOfParallelism = value; } }

        #region Get/Set Data    

        public string GetMovieGenresString(int movieID)
        {
            var movie = GetMovie(movieID);
            string result = null;
            foreach (var genreID in movie.GetGenres())
            {
                if (result != null)
                    result += ",";
                result += genreTitles[genreID];
            }
            return result;
        }

        public string GetUserGenresString(int userID)
        {
            string result = null;
            foreach (var genreID in MostWatchedGenres(GetUser(userID)))
            {
                if (result != null)
                    result += ",";
                result += genreTitles[genreID];
            }
            return result;
        }

        public User GetUser(int userID)
        {
            User user;
            if (!users.ContainsKey(userID))
            {
                user = new User(userID);
                users.Add(userID, user);
            }
            else
                user = users[userID];
            return user;
        }

        public Movie GetMovie(int movieID)
        {
            Movie movie;
            if (!movies.ContainsKey(movieID))
            {
                movie = new Movie(movieID);
                movies.Add(movieID, movie);
            }
            else
                movie = movies[movieID];
            return movie;
        }

        public void ReadRatingFile(string ratingsFile)
        {
            ProgressState = "Reading rating file...";
            ProgressPercent = 0;

            using (var streamReader = new StreamReader(ratingsFile))
            {
                ProgressMaximum = streamReader.BaseStream.Length;

                streamReader.ReadLine();
                while (!streamReader.EndOfStream)
                {
                    string line = streamReader.ReadLine();
                    string[] items = line.Split(',');
                    int userID = Convert.ToInt32(items[0]);
                    int movieID = Convert.ToInt32(items[1]);
                    float rate = (float)Convert.ToDouble(items[2]);

                    GetUser(userID).AddMovie(movieID, rate);

                    ProgressPercent = streamReader.BaseStream.Position;
                }
            }
            ProgressPercent = ProgressMaximum;
            ProgressState = "Done";
        }

        public void ReadMovieFile(string moviesFile)
        {
            ProgressState = "Reading movies file...";
            ProgressPercent = 0;
            using (var streamReader = new StreamReader(moviesFile))
            {
                ProgressMaximum = streamReader.BaseStream.Length;

                streamReader.ReadLine();
                while (!streamReader.EndOfStream)
                {
                    string line = streamReader.ReadLine();
                    var items = Split(line, ',');
                    int movieID = Convert.ToInt32(items[0]);
                    string name = items[1];

                    var movie = GetMovie(movieID);
                    movie.SetName(name);

                    var genres = Split(items[2], '|');
                    foreach (string genreName in genres)
                    {
                        var genreID = genreTitles.IndexOf(genreName);
                        if (genreID == -1)
                        {
                            genreID = genreTitles.Count;
                            genreTitles.Add(genreName);
                        }
                        movie.AddGenre((byte)genreID);
                    }

                    ProgressPercent = streamReader.BaseStream.Position;
                }
            }
            ProgressPercent = ProgressMaximum;
            ProgressState = "Done";
        }
        #endregion

        #region Rate Functions
        public void NormalizeRates()
        {
            ProgressMaximum = users.Count();
            ProgressPercent = 0;
            ProgressState = "Normalizing ratings...";

            //foreach (var user in data.users.Values)
            Parallel.ForEach(users.Values, parallelOptions, user =>
            {
                user.NormalizeRates();
                ProgressPercent++;
            });

            ProgressPercent = ProgressMaximum;
            ProgressState = "Done";
        }

        public void CalculateRateHashes()
        {
            ProgressMaximum = users.Count();
            ProgressPercent = 0;
            ProgressState = "Calculating rating hashes...";

            User.CalculateHashVectors(10, movies.Values.Max(p => p.id) + 1);

            //foreach (var user in data.users.Values)
            Parallel.ForEach(users.Values, parallelOptions, user =>
            {
                user.CalculateHashes();
                ProgressPercent++;
            });

            ProgressPercent = ProgressMaximum;
            ProgressState = "Done";
        }

        private float PredictRate(KeyValuePair<User, float> userA, KeyValuePair<User, float> userC, User userPrediction, int movieID_Prediction)
        {
            if (userA.Value + userC.Value == 0)
                return 0;
            float rateA = userA.Key.EnsureRated(movieID_Prediction);
            float rateC = userC.Key.EnsureRated(movieID_Prediction);
            //(simA * rateA + simC * rateC) / (simA + simC)
            return (userA.Value * rateA + userC.Value * rateC)
                / (userA.Value + userC.Value);
        }
        #endregion

        #region Movie
        /// <summary>
        /// Example: 100,"Leon, The Professional",Action,
        /// </summary>
        List<string> Split(string str, char seperator)
        {
            List<string> result = new List<string>();
            bool inQutation = false;
            int from = 0;
            for (int i = 0; i < str.Length; i++)
                if (str[i] == seperator && !inQutation)
                {
                    result.Add(str.Substring(from, i - from).Replace("\"", ""));
                    from = i + 1;
                }
                else if (str[i] == '\"')
                {
                    inQutation = !inQutation;
                }
            result.Add(str.Substring(from));
            return result;
        }

        private bool ContainsAny(byte[] genres_i, byte[] genres_j)
        {
            foreach (var genre_i in genres_i)
                foreach (var genre_j in genres_j)
                    if (genre_i == genre_j)
                        return true;
            return false;
        }

        /// <summary>
        /// فیلم هایی که ژانر های خاصی دارند و کاربر دیده است.
        /// </summary>
        private List<Movie> GetMoviesIncludingGenre(List<byte> genreIDs, User user)
        {
            var list = new List<Movie>();
            foreach (var movieID in user.MoviesWatched())
            {
                var movie = GetMovie(movieID);
                var genres = movie.GetGenres();
                if (ContainsAny(genres, genreIDs.ToArray()))
                    list.Add(movie);
            }
            return list;
        }

        private IEnumerable<byte> MostWatchedGenres(User user)
        {
            var list = new Dictionary<byte, uint>();    //genreID , count
            foreach (var movieID in user.MoviesWatched())
            {
                var movie = GetMovie(movieID);
                var genres = movie.GetGenres();
                foreach (var genreID in genres)
                    if (list.ContainsKey(genreID))
                        list[genreID]++;
                    else
                        list.Add(genreID, 1);
            }
            var ordered = list.OrderByDescending(p => p.Value);
            return ordered.Take(5)
                          .Select(p => p.Key);
        }

        private IEnumerable<KeyValuePair<Movie, uint>> MovieDistance(Movie movie)
        {
            var result = new Dictionary<Movie, uint>();
            foreach (var movie_j in movies)
                if (movie.id != movie_j.Key)
                {
                    uint sim = Movie.EuclideanDistanceMovie(movie, movie_j.Value);
                    result.Add(movie_j.Value, sim);
                }
            var ordered = result.OrderBy(p => p.Value).Take(5);
            return ordered;
        }
        #endregion

        #region Recommend Methods
        /// <summary>
        /// روش اول: بر اساس امتیازات
        /// </summary>
        /// <param name="userID">من چه کسی هستم</param>
        /// <param name="useHash">آیا از هش استفاده بشود یا خیر</param>
        /// <returns></returns>
        public Dictionary<int, float> RecommendMovieByRates(int userID, bool useHash)
        {
            ProgressPercent = 0;
            ProgressState = "Finding Simmilarities...";

            var user = GetUser(userID);
            ProgressMaximum = movies.Count() + users.Count();

            var result = new Dictionary<int, float>();
            var simularities = new Dictionary<User, float>();
            //foreach (var user_j in users)
            Parallel.ForEach(users, parallelOptions, user_j =>
            {
                if (user.id != user_j.Key)
                {
                    //if (user_j.Value.HasWatchedMovie(movieID))
                    if (!useHash || User.CompareHashes(user, user_j.Value))
                    {
                        float sim = User.CosineSimularityRate(user, user_j.Value);
                        lock (simularities)
                            simularities.Add(user_j.Value, sim);
                    }
                }
                ProgressPercent++;
            });

            if (simularities.Count < 2)
                return result;
            var orderedSimilarities = simularities.OrderByDescending(order => order.Value);
            var sim1 = orderedSimilarities.ElementAt(0);
            var sim2 = orderedSimilarities.ElementAt(1);

            //foreach (var movie in movies.Values)
            Parallel.ForEach(movies.Values, parallelOptions, movie =>
            {
                if (!user.HasWatchedMovie(movie.id))
                {
                    float predict = PredictRate(sim1, sim2, user, movie.id);
                    lock (result)
                        result.Add(movie.id, predict);
                }
                ProgressPercent++;
            });

            ProgressMaximum = 100;
            ProgressPercent = 100;
            ProgressState = "Done - He loves " + GetUserGenresString(userID);

            return result;
        }

        public Dictionary<int, float> RecommendMovieByRatesGenres(int userID, bool useHash)
        {
            ProgressPercent = 0;
            ProgressState = "Finding Simmilarities...";

            var user = GetUser(userID);
            var genreIDs = MostWatchedGenres(user).ToArray();
            ProgressMaximum = users.Count() + movies.Count();

            var result = new Dictionary<int, float>();
            var simularities = new Dictionary<User, float>();
            //foreach (var user_j in users)
            Parallel.ForEach(users, parallelOptions, user_j =>
            {
                if (user.id != user_j.Key)
                {
                    //if (user_j.Value.HasWatchedMovie(movieID))
                    if (!useHash || User.CompareHashes(user, user_j.Value))
                    {
                        float sim = User.CosineSimularityRate(user, user_j.Value);
                        lock (simularities)
                            simularities.Add(user_j.Value, sim);
                    }
                }
                ProgressPercent++;
            });

            if (simularities.Count < 2)
                return result;
            var orderedSimilarities = simularities.OrderByDescending(order => order.Value);
            var sim1 = orderedSimilarities.ElementAt(0);
            var sim2 = orderedSimilarities.ElementAt(1);

            //foreach (var movie in movies.Values)
            Parallel.ForEach(movies.Values, parallelOptions, movie =>
            {
                if (ContainsAny(movie.GetGenres(), genreIDs) && !user.HasWatchedMovie(movie.id))
                {
                    float predict = PredictRate(sim1, sim2, user, movie.id);
                    lock (result)
                        result.Add(movie.id, predict);
                }
                ProgressPercent++;
            });

            ProgressMaximum = 100;
            ProgressPercent = 100;
            ProgressState = "Done - He loves " + GetUserGenresString(userID);

            return result;
        }

        /// <summary>
        /// روش دوم: بر اساس ژانر های دیده شده توسط کاربر و شباهت فیلم ها بر اساس ژانر
        /// </summary>
        public IEnumerable<KeyValuePair<int, uint>> RecommendMovieByGenre(int userID)
        {
            ProgressPercent = 0;
            ProgressState = "Finding Simmilarities...";

            var user = GetUser(userID);
            var genreIDs = MostWatchedGenres(user).ToList();
            var userMovies = GetMoviesIncludingGenre(genreIDs, user);
            ProgressMaximum = userMovies.Count();

            var result = new Dictionary<int, uint>();
            ////foreach (var movie in movies.Values)
            Parallel.ForEach(userMovies, parallelOptions, movie =>
            {
                var distances = MovieDistance(movie);
                lock (result)
                    foreach (var m in distances)
                        if (result.ContainsKey(m.Key.id))
                        {
                            if (result[m.Key.id] > m.Value)
                                result[m.Key.id] = m.Value;
                        }
                        else
                            result.Add(m.Key.id, m.Value);
                ProgressPercent++;
            });

            ProgressMaximum = 100;
            ProgressPercent = 100;
            ProgressState = "Done - He loves " + GetUserGenresString(userID);

            return result;
        }

        public void CalculatePredictionErrors(double selectPossibility, bool useHash)
        {
            ProgressPercent = 0;
            ProgressState = "Getting Random Users...";

            Random random = new Random();
            double sumError = 0;            
            int itemsCount = 0;            

            var randomUsers = new User[(int)(users.Count * selectPossibility)];
            ProgressMaximum = randomUsers.Length;
            var usersTemp = users.Values.ToList();
            for (int i = 0; i < randomUsers.Length; i++)
            {
                var randomID = random.Next(usersTemp.Count);
                var user = usersTemp[randomID];
                randomUsers[i] = user;
                usersTemp.Remove(user);
                ProgressPercent++;
            }

            ProgressState = "Calculating Errors of our Prediction Algorithm...";
            ProgressPercent = 0;            

            //foreach (User user in users.Values)
            Parallel.ForEach(randomUsers, parallelOptions, user =>
            {
                var simularities = new Dictionary<User, float>();
                var movies = user.MoviesWatched().ToArray();
                foreach (var movieID in movies)
                {
                    var realRate = user.Rated(movieID);
                    try
                    {
                        user.SetRate(movieID, 0);
                        user.CalculateHashes();

                        simularities.Clear();
                        foreach (var user_j in users)
                            if (!useHash || User.CompareHashes(user, user_j.Value))
                            {
                                float sim = User.CosineSimularityRate(user, user_j.Value);
                                lock (simularities)
                                    simularities.Add(user_j.Value, sim);
                            }

                        if (simularities.Count < 2)
                            continue;
                        var orderedSimilarities = simularities.OrderByDescending(order => order.Value);
                        var sim1 = orderedSimilarities.ElementAt(0);
                        var sim2 = orderedSimilarities.ElementAt(1);


                        float predictRate = PredictRate(sim1, sim2, user, movieID);
                        if (float.IsNaN(predictRate))
                            continue;

                        double error = Math.Pow(realRate - predictRate, 2);
                        sumError += error;
                        itemsCount++;
                    }
                    finally
                    {
                        user.SetRate(movieID, realRate);
                        user.CalculateHashes();
                    }
                }

                ProgressPercent++;
            });

            var result = Math.Sqrt(sumError / itemsCount);            

            ProgressMaximum = 100;
            ProgressPercent = 100;
            ProgressState = "Done - Error: " + Math.Round((result / 5) * 100) + "%";
        }
        #endregion

        #region Preprocess Method
        private Dictionary<User, float> RateSimilarity(User user, bool useHash)
        {
            var result = new Dictionary<User, float>();
            foreach (var user_j in users)
                if (user.id != user_j.Key)
                {
                    //if (user_j.Value.HasWatchedMovie(movieID))
                    if (useHash || User.CompareHashes(user, user_j.Value))
                    {
                        float sim = User.CosineSimularityRate(user, user_j.Value);
                        if (!float.IsNaN(sim))
                            result.Add(user_j.Value, sim);
                    }
                }
            return result;
        }
        public void PreprocessSimularities(bool useHash)
        {
            ProgressPercent = 0;
            ProgressState = "Calculating Simmilarities...";

            List<int> users = new List<int>();
            string connectionString = "Data Source=192.168.1.2;Database=MovieRecommendation;User Id=matin;Password=matinL";
            using (var con = new SqlConnection(connectionString))
            using (var com = con.CreateCommand())
            {
                con.Open();
                com.CommandText = "select userID from dbo.Users";
                using (var reader = com.ExecuteReader())
                    while (reader.Read())
                        users.Add((int)reader[0]);
            }
            ProgressMaximum = users.Count;

            //Parallel.ForEach(users, parallelOptions, userID =>
            foreach (var userID in users)
            {
                var user = GetUser(userID);

                var simularities = RateSimilarity(user, useHash);
                if (simularities.Count < 2)
                    continue;

                var orderedSimilarities = simularities.OrderByDescending(order => order.Value);

                var sim1 = orderedSimilarities.ElementAt(0);
                var sim2 = orderedSimilarities.ElementAt(1);

                Thread t = new Thread(() =>
                {
                    using (var con = new SqlConnection(connectionString))
                    using (var com = con.CreateCommand())
                    {
                        con.Open();
                        com.CommandText = "update dbo.Users set SimUser_1=@su1, SimUser_2=@su2,SimValue_1=@sv1,SimValue_2=@sv2 where userID=@u";
                        com.Parameters.AddWithValue("@u", userID);
                        com.Parameters.AddWithValue("@su1", sim1.Key.id);
                        com.Parameters.AddWithValue("@su2", sim2.Key.id);
                        com.Parameters.AddWithValue("@sv1", sim1.Value);
                        com.Parameters.AddWithValue("@sv2", sim2.Value);
                        com.ExecuteNonQuery();
                    }
                });
                t.Start();

                ProgressPercent++;
            }//);

            ProgressMaximum = 100;
            ProgressPercent = 100;
            ProgressState = "Done";
        }
        #endregion
    }
}
