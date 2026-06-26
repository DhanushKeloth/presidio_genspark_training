// Matches backend: UserRegisterRequest
export interface UserRegisterRequest {
  fullName: string;
  email: string;
  phone?: string;
  password: string;
  confirmPassword: string;
}

// Matches backend: LoginRequest
export interface LoginRequest {
  email: string;
  password: string;
}

// Matches backend: LoginResponse
export interface LoginResponse {
  token: string;
  expiresAt: string;
  role: string;
  email: string;
  name: string;
}

// Matches backend: ErrorResponse
export interface ApiError {
  status: number;
  error: string;
  message: string;
  traceId: string;
  details?: { field: string; message: string }[];
}
