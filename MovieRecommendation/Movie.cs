using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieRecommendation
{
    class Movie
    {
        public readonly int id;
        public string name { get; private set; }
        private List<byte> genres = new List<byte>();

        #region Data Encapsulation
        public Movie(int id)
        {
            this.id = id;
        }

        public void AddGenre(byte genreID)
        {
            this.genres.Add(genreID);
        }

        public void SetName(string name)
        {
            this.name = name;
        }

        public bool IsInGenre(byte genreID)
        {
            return genres.Contains(genreID);
        }

        public byte[] GetGenres()
        {
            return genres.ToArray();
        }
        #endregion

        #region Euclidean Distance Calculation
        private static uint SubtractCount(Movie movie_i, Movie movie_j)
        {
            uint count = 0;
            foreach (var genreID in movie_i.GetGenres())
                if (!movie_j.IsInGenre(genreID))
                    count++;
            return count;
        }

        public static uint EuclideanDistanceMovie(Movie movie_i, Movie movie_j)
        {
            return SubtractCount(movie_i, movie_j) + SubtractCount(movie_j, movie_i);
        }
        #endregion
    }
}
