import { Movie, MovieDetail, PagedResult } from '../../models/movie.models';

export interface MoviesState {
  nowShowing: Movie[];
  comingSoon: Movie[];
  pagedMovies: PagedResult<Movie> | null;
  selectedMovie: MovieDetail | null;
  loading: boolean;
  error: string | null;
}

export const initialMoviesState: MoviesState = {
  nowShowing: [],
  comingSoon: [],
  pagedMovies: null,
  selectedMovie: null,
  loading: false,
  error: null,
};
