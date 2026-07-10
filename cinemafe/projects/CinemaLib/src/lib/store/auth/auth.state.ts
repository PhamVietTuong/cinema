import { UserProfile } from '../../models/auth.models';

export interface AuthState {
  user: UserProfile | null;
  token: string | null;
  loading: boolean;
  error: string | null;
  awaitingTwoFactor: boolean;
}

export const initialAuthState: AuthState = {
  user: null,
  token: null,
  loading: false,
  error: null,
  awaitingTwoFactor: false,
};
