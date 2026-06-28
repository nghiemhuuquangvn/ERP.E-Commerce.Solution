import { apiClient } from './client'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  user: {
    id: string
    email: string
    firstName: string
    lastName: string
    roles: string[]
    permissions: string[]
  }
}

export const authApi = {
  login: (body: LoginRequest) =>
    apiClient.post<LoginResponse>('/v1/auth/login', body).then((r) => r.data),

  logout: (refreshToken: string) =>
    apiClient.post('/v1/auth/logout', { refreshToken }),

  me: () =>
    apiClient.get<LoginResponse['user']>('/v1/auth/me').then((r) => r.data),
}
