import '../global.css'
import '@/i18n'
import { useEffect } from 'react'
import { Stack, useRootNavigationState, useRouter, useSegments } from 'expo-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SafeAreaProvider } from 'react-native-safe-area-context'
import { useAuthStore } from '@/stores/auth.store'
import { ToastHost } from '@/components/ui'

const queryClient = new QueryClient()

function AuthGuard() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const segments = useSegments()
  const router = useRouter()
  // Navigating before the root navigator has mounted throws; wait for its key.
  const navigationReady = !!useRootNavigationState()?.key

  useEffect(() => {
    if (!navigationReady) return
    const inAuth = segments[0] === '(auth)'
    if (!isAuthenticated && !inAuth) {
      router.replace('/(auth)/login')
    } else if (isAuthenticated && inAuth) {
      router.replace('/(tabs)')
    }
  }, [isAuthenticated, segments, navigationReady])

  return null
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <AuthGuard />
        <Stack screenOptions={{ headerShown: false }} />
        <ToastHost top={72} />
      </QueryClientProvider>
    </SafeAreaProvider>
  )
}
