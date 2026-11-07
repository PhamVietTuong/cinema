/** Default page size for public movie lists (All / Now Showing / Coming Soon) before "load more" appears. */
export const MOVIE_PAGE_SIZE = 15;

export interface Movie {
  id: number;
  title: string;
  slug: string;
  description: string;
  duration: number;
  releaseDate: string;
  endDate?: string;
  posterUrl?: string;
  trailerUrl?: string;
  director?: string;
  cast?: string;
  language?: string;
  ageRestrictionCode: string;
  genres: string[];
  averageRating: number;
  ratingCount: number;
  isActive: boolean;
  isNowShowing: boolean;
  isComingSoon: boolean;
}

export interface MovieDetail extends Movie {
  ageRestrictionDescription: string;
  ageRestrictionMinAge: number;
  showTimes: ShowTimeSummary[];
  recentComments: Comment[];
  canReview: boolean;
  /** The signed-in user's own score (1-10) and private review text; absent until they rate. */
  myRating?: number | null;
  myReview?: string | null;
  /** Approved comments on the movie, replies included. */
  totalComments: number;
}

export interface ShowTimeSummary {
  id: number;
  startTime: string;
  endTime: string;
  projectionForm: number;
  theaterName: string;
  roomName: string;
  roomId: number;
  availableSeats: number;
}

export interface Comment {
  id: number;
  content: string;
  userName: string;
  userAvatar?: string;
  parentId?: string;
  creationTime: string;
  /** True when the signed-in user wrote it, so edit/delete are offered. */
  isMine: boolean;
  replies: Comment[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
