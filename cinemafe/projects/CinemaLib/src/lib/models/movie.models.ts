export interface Movie {
  id: number;
  title: string;
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
  parentId?: number;
  createdAt: string;
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
