import '../global.css'
import '@/i18n'
import { useEffect } from 'react'
import { Stack, useRootNavigationState, useRouter, useSegments } from 'expo-router'
import { QueryClient, QueryClientProvider, focusManager } from '@tanstack/react-query'
import { SafeAreaProvider } from 'react-native-safe-area-context'
import { useTranslation } from 'react-i18next'
import { Spinner, ToastHost } from '@/components/ui'
import { SideMenu } from '@/components/SideMenu'
import { AppState, View } from 'react-native'
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

/** Tells react-query when the app is in the foreground, so the unread-count polling pauses in the background. */
function useForegroundFocus() {
  useEffect(() => {
    const sub = AppState.addEventListener('change', (state) => focusManager.setFocused(state === 'active'))
    return () => sub.remove()
  }, [])
}

export default function RootLayout() {
  const { t } = useTranslation()
  const signedIn = useAuthStore((s) => s.status === 'signedIn')
  useForegroundFocus()
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
          <Stack.Screen name="notifications/index" options={{ headerShown: true, title: t('notifications.title') }} />
          <Stack.Screen name="purchase/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="sales/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="hrm/leave/[id]" options={{ headerShown: true }} />
          <Stack.Screen name="hrm/employee/[id]" options={{ headerShown: true }} />
        </Stack>
        {signedIn ? <SideMenu /> : null}
        <AuthGuard />
        <Booting />
        <ToastHost top={72} />
      </QueryClientProvider>
    </SafeAreaProvider>
  )
}
