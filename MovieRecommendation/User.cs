using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieRecommendation
{
    class User
    {
        public readonly int id;
        public float average { get; private set; }
        private Dictionary<int, float> movieRating = new Dictionary<int, float>();   //movieID(int) , rate(float)

        private float[] hashes;
        private static bool[,] hashVectors;

        #region Data Encapsulation
        public User(int id)
        {
            this.id = id;
        }

        public void AddMovie(int movieID, float rate)
        {
            movieRating.Add(movieID, rate);
        }

        public void SetRate(int movieID, float rate)
        {
            movieRating[movieID] = rate;
        }

        public bool HasWatchedMovie(int movieID)
        {
            return movieRating.ContainsKey(movieID);
        }

        public float Rated(int movieID)
        {
            return movieRating[movieID];
        }

        public float EnsureRated(int movieID)
        {
            if (movieRating.ContainsKey(movieID))
                return movieRating[movieID];
            return 0;
        }

        public IEnumerable<int> MoviesWatched()
        {
            return movieRating.Keys;
        } 
        #endregion

        public void NormalizeRates()
        {
            if (movieRating.Count == 0)
                return;

            average = movieRating.Values.Average();
            foreach (int movieID in movieRating.Keys.ToList())
                movieRating[movieID] = movieRating[movieID] - average;
        }

        #region Hash Calculation
        /// <summary>
        /// تشکیل بردار ها
        /// </summary>
        /// <param name="count">تعداد بردارها</param>
        /// <param name="movieCount">تعداد درایه ها</param>
        public static void CalculateHashVectors(int count, int movieCount)
        {
            Random random = new Random();
            hashVectors = new bool[count, movieCount];
            for (int i = 0; i < count; i++)
                for (int j = 0; j < movieCount; j++)
                    hashVectors[i, j] = (random.Next(2) == 1);
        }

        /// <summary>
        /// محاسبه هش ها
        /// </summary>
        public void CalculateHashes()
        {
            hashes = new float[hashVectors.GetLength(0)];
            for (int i = 0; i < hashVectors.GetLength(0); i++)
                foreach (var rate in movieRating)
                    hashes[i] += hashVectors[i, rate.Key] ? rate.Value : -rate.Value;
        }

        /// <summary>
        /// مقایسه هش ها
        /// </summary>
        /// <param name="user_i"></param>
        /// <param name="user_j"></param>
        /// <returns></returns>
        public static bool CompareHashes(User user_i, User user_j)
        {
            if (user_i.hashes == null)
                return false;

            for (int k = 0; k < user_i.hashes.Length; k++)
                if (Math.Sign(user_i.hashes[k]) != Math.Sign(user_j.hashes[k]))
                    return false;
            return true;
        }
        #endregion

        #region Cosine Simularity Calculation
        /// <summary>
        /// یک قسمت از دو قسمت مخرج کسر
        /// </summary>
        public float GetRateSize()
        {
            float rateSize = 0;

            foreach (var rate in movieRating)
                rateSize += rate.Value * rate.Value;
            rateSize = (float)Math.Sqrt(rateSize);
            return rateSize;
        }

        /// <summary>
        /// صورت کسر
        /// </summary>
        private static float GetRateDot(User user_i, User user_j)
        {
            float dot = 0;
            foreach (var movieID in user_i.MoviesWatched())
                if (user_j.HasWatchedMovie(movieID))
                    dot += user_i.Rated(movieID) * user_j.Rated(movieID);
            return dot;
        }

        public static float CosineSimularityRate(User user_i, User user_j)
        {
            return GetRateDot(user_i, user_j)
                / (user_i.GetRateSize() * user_j.GetRateSize());
        } 
        #endregion
    }
}
