import Constants from 'expo-constants'
import * as Notifications from 'expo-notifications'
import * as SecureStore from 'expo-secure-store'
import { Platform } from 'react-native'
import { devicesApi } from './api'

// Real push (a message that reaches a locked phone) needs a Firebase project, so it is OFF unless the build says PUSH_ENABLED=true
// (app.config.js -> extra.pushEnabled) and carries the tenant's google-services.json. Off, nothing here runs and the app is unchanged.

const DEVICE_KEY = 'erp-admin-device-id'

export function pushEnabled(): boolean {
  return (Constants.expoConfig?.extra as { pushEnabled?: boolean } | undefined)?.pushEnabled === true
}

/** A stable id for this install, made once (the server keeps one token per user and device). */
export async function getDeviceId(): Promise<string> {
  try {
    const existing = await SecureStore.getItemAsync(DEVICE_KEY)
    if (existing) return existing
    const made = `${Platform.OS}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
    await SecureStore.setItemAsync(DEVICE_KEY, made)
    return made
  } catch {
    return `${Platform.OS}-session-${Math.random().toString(36).slice(2, 10)}`
  }
}

/** Gives the server this phone's push token. Any failure is swallowed: the bell and the local alerts do not depend on push. */
export async function registerThisDevice(): Promise<boolean> {
  if (!pushEnabled()) return false
  try {
    const token = await Notifications.getDevicePushTokenAsync()
    if (!token?.data) return false
    await devicesApi.register(String(token.data), await getDeviceId(), Platform.OS)
    return true
  } catch {
    return false
  }
}

/** Removes this phone's token at sign-out, so the next person to sign in is not sent this user's messages. */
export async function unregisterThisDevice(): Promise<void> {
  if (!pushEnabled()) return
  try {
    await devicesApi.unregister(await getDeviceId())
  } catch {
    /* the server forgets a dead token by itself */
  }
}
