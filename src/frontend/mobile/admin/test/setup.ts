import '@/i18n'

jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock'),
)
// An in-memory keystore, so the auth store can be tested with a real save / load / clear.
jest.mock('expo-secure-store', () => {
  const mem = new Map<string, string>()
  return {
    getItemAsync: jest.fn(async (k: string) => mem.get(k) ?? null),
    setItemAsync: jest.fn(async (k: string, v: string) => void mem.set(k, v)),
    deleteItemAsync: jest.fn(async (k: string) => void mem.delete(k)),
  }
})
// No notification support in Jest: every call is a harmless stub; tests that check the calls use the same mock functions.
jest.mock('expo-notifications', () => ({
  setNotificationHandler: jest.fn(),
  setNotificationChannelAsync: jest.fn().mockResolvedValue(undefined),
  getPermissionsAsync: jest.fn().mockResolvedValue({ granted: false, canAskAgain: false }),
  requestPermissionsAsync: jest.fn().mockResolvedValue({ granted: false }),
  scheduleNotificationAsync: jest.fn().mockResolvedValue('id'),
  addNotificationResponseReceivedListener: jest.fn(() => ({ remove: jest.fn() })),
  getDevicePushTokenAsync: jest.fn().mockResolvedValue({ type: 'android', data: 'fcm-token' }),
  AndroidImportance: { HIGH: 4 },
}))
jest.mock('react-native-safe-area-context', () => {
  const React = require('react')
  const { View } = require('react-native')
  return {
    SafeAreaProvider: ({ children }: { children: React.ReactNode }) => children,
    SafeAreaView: ({ children, ...p }: { children: React.ReactNode }) => React.createElement(View, p, children),
    useSafeAreaInsets: () => ({ top: 0, bottom: 0, left: 0, right: 0 }),
  }
})
