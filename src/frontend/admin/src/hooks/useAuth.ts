import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { jwtDecode } from 'jwt-decode'
import {
  authApi,
  LoginRequest,
  LoginResponse,
  VerifyTwoFactorRequest,
  ResendTwoFactorRequest,
} from '@/api/auth.api'
import { useAuthStore } from '@/stores/auth.store'

interface AccessTokenClaims {
  tenant_id?: string
}

function decodeTenantId(accessToken: string): string {
  try {
    return jwtDecode<AccessTokenClaims>(accessToken).tenant_id ?? ''
  } catch {
    return ''
  }
}

/**
 * Shared completion step for every login path (password, 2FA-verified, and every
 * SSO provider): stores tokens + user (with tenantId decoded once from the JWT
 * `tenant_id` claim) and navigates to the dashboard. `data.requiresTwoFactor`
 * must be checked by the caller BEFORE calling this — this helper assumes tokens
 * and `user` are populated.
 */
export function useCompleteLogin() {
  const navigate = useNavigate()
  const { setUser, setTokens } = useAuthStore()

  return (data: LoginResponse) => {
    if (!data.accessToken || !data.refreshToken || !data.user) return
    setTokens(data.accessToken, data.refreshToken)
    setUser({
      id: data.user.id,
      email: data.user.email,
      fullName: data.user.fullName,
      tenantId: decodeTenantId(data.accessToken),
      roles: data.user.roles,
      permissions: data.user.permissions,
    })
    navigate('/dashboard')
  }
}

export function useLogin() {
  const navigate = useNavigate()
  const completeLogin = useCompleteLogin()

  return useMutation({
    mutationFn: (body: LoginRequest) => authApi.login(body),
    onSuccess: (data) => {
      if (data.requiresTwoFactor) {
        navigate(`/login/2fa?challengeId=${data.challengeId}&method=${data.twoFactorMethod}`)
        return
      }
      completeLogin(data)
    },
  })
}

export function useVerifyTwoFactor() {
  const navigate = useNavigate()
  const completeLogin = useCompleteLogin()

  return useMutation({
    mutationFn: (body: VerifyTwoFactorRequest) => authApi.verifyTwoFactor(body),
    onSuccess: (data) => {
      if (data.requiresTwoFactor) {
        navigate(`/login/2fa?challengeId=${data.challengeId}&method=${data.twoFactorMethod}`)
        return
      }
      completeLogin(data)
    },
  })
}

export function useResendTwoFactor() {
  return useMutation({
    mutationFn: (body: ResendTwoFactorRequest) => authApi.resendTwoFactor(body),
  })
}

export function useLogout() {
  const navigate = useNavigate()
  const { logout, refreshToken } = useAuthStore()

  return () => {
    if (refreshToken) authApi.logout(refreshToken).catch(() => undefined)
    logout()
    navigate('/login')
  }
}
