import { useMutation } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { authApi, LoginRequest } from '@/api/auth.api'
import { useAuthStore } from '@/stores/auth.store'

export function useLogin() {
  const navigate = useNavigate()
  const { setUser, setTokens } = useAuthStore()

  return useMutation({
    mutationFn: (body: LoginRequest) => authApi.login(body),
    onSuccess: (data) => {
      setTokens(data.accessToken, data.refreshToken)
      setUser(data.user)
      navigate('/dashboard')
    },
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
