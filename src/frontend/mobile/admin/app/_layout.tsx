import '../global.css'
import '@/i18n'
import { useEffect } from 'react'
import { Stack, useRootNavigationState, useRouter, useSegments } from 'expo-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SafeAreaProvider } from 'react-native-safe-area-context'
import { Spinner, ToastHost } from '@/components/ui'
import { View } from 'react-native'
import { useAuthStore } from '@/stores/auth.store'
import { colors } from '@/theme/theme'

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 15_000 } } })

function AuthGuard() {
  const status = useAuthStore((s) => s.status)
  const segments = useSegments()
  const router = useRouter()
  // Navigating before the root navigator has mounted throws; wait for its key.
  const navigationReady = !!useRootNavigationState()?.key

  useEffect(() => {
    void useAuthStore.getState().bootstrap()
  }, [])

  useEffect(() => {
    if (!navigationReady || status === 'booting') return
    const inAuth = segments[0] === '(auth)'
    if (status === 'signedOut' && !inAuth) router.replace('/(auth)/login')
    else if (status === 'signedIn' && inAuth) {
      queryClient.clear() // nothing of a previous session stays on screen
      router.replace('/(tabs)')
    }
    if (status === 'signedOut') queryClient.clear()
  }, [status, segments, navigationReady])

  return null
}

function Booting() {
  const status = useAuthStore((s) => s.status)
  if (status !== 'booting') return null
  return (
    <View className="absolute inset-0 items-center justify-center bg-surface">
      <Spinner />
    </View>
  )
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <Stack
          screenOptions={{
            headerShown: false,
            headerStyle: { backgroundColor: colors.surface },
            headerTintColor: colors.ink,
          }}
        >
          <Stack.Screen name="purchase/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="sales/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="hrm/leave/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="hrm/employee/[id]" options={{ headerShown: true }} />
        </Stack>
        <AuthGuard />
        <Booting />
        <ToastHost top={72} />
      </QueryClientProvider>
    </SafeAreaProvider>
  )
}
