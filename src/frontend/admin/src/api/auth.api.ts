import { apiClient } from './client'

// Admin app's own numeric UserType value — must match
// ERP.Identity.Domain.Enums.UserType (Customer=0, Staff=1, Admin=2, SuperAdmin=3,
// System=999). Staff logging into this back-office app authenticate as Admin.
export const ADMIN_USER_TYPE = 2

export interface LoginRequest {
  email: string
  password: string
  userType: number
}

export interface LoginUser {
  id: string
  email: string
  fullName?: string | null
  roles: string[]
  permissions: string[]
}

export interface LoginResponse {
  accessToken: string | null
  refreshToken: string | null
  succeeded: boolean
  errors?: string[] | null
  requiresTwoFactor: boolean
  challengeId?: string | null
  twoFactorMethod?: string | null
  expiresInSeconds?: number | null
  user?: LoginUser | null
}

export interface ExternalLoginRequest {
  provider: string
  providerKey: string
  idToken: string
  userType: number
}

export interface VerifyTwoFactorRequest {
  challengeId: string
  code: string
}

export interface ResendTwoFactorRequest {
  challengeId: string
}

export interface TwitterAuthorizeUrlResponse {
  authorizeUrl: string
  state: string
}

export interface TwitterCallbackRequest {
  code: string
  state: string
}

export interface PublicAuthOptionsDto {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
}

export const authApi = {
  login: (body: LoginRequest) =>
    apiClient.post<LoginResponse>('/v1/auth/login', body).then((r) => r.data),

  logout: (refreshToken: string) =>
    apiClient.post('/v1/auth/logout', { refreshToken }),

  me: () =>
    apiClient.get<LoginUser>('/v1/auth/me').then((r) => r.data),

  externalLogin: (body: ExternalLoginRequest) =>
    apiClient.post<LoginResponse>('/v1/auth/external-login', body).then((r) => r.data),

  getTwitterAuthorizeUrl: () =>
    apiClient
      .get<TwitterAuthorizeUrlResponse>('/v1/auth/external-login/twitter/authorize-url')
      .then((r) => r.data),

  twitterCallback: (body: TwitterCallbackRequest) =>
    apiClient
      .post<LoginResponse>('/v1/auth/external-login/twitter/callback', body)
      .then((r) => r.data),

  verifyTwoFactor: (body: VerifyTwoFactorRequest) =>
    apiClient.post<LoginResponse>('/v1/auth/2fa/verify', body).then((r) => r.data),

  resendTwoFactor: (body: ResendTwoFactorRequest) =>
    apiClient.post<LoginResponse>('/v1/auth/2fa/resend', body).then((r) => r.data),

  getLoginOptions: () =>
    apiClient.get<PublicAuthOptionsDto>('/v1/auth/login-options').then((r) => r.data),
}
