import * as Notifications from 'expo-notifications'
import { Platform } from 'react-native'
import type { AppNotification } from './api'

// Phone notifications raised by the app itself (no Firebase needed): the unread count is polled while the app is open, and when it grows the
// newest messages are shown as a system notification. Every failure is swallowed - the bell and the inbox never depend on this.

const CHANNEL = 'approvals'

/** What a tap on a phone notification carries back to the app. */
export interface AlertData {
  entityType?: string | null
  entityId?: string | null
  [key: string]: unknown
}

/** Shows the notification even while the app is in the foreground (the default is to hide it). */
export function configureForegroundAlerts(): void {
  try {
    Notifications.setNotificationHandler({
      handleNotification: async () => ({ shouldShowAlert: true, shouldPlaySound: false, shouldSetBadge: false }),
    })
  } catch {
    /* no notification support on this device */
  }
}

/** Asks for the Android 13+ permission once; false when refused (the bell keeps working). */
export async function ensureAlertPermission(): Promise<boolean> {
  try {
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync(CHANNEL, {
        name: 'Approvals',
        importance: Notifications.AndroidImportance.HIGH,
      })
    }
    const current = await Notifications.getPermissionsAsync()
    if (current.granted) return true
    if (!current.canAskAgain) return false
    return (await Notifications.requestPermissionsAsync()).granted
  } catch {
    return false
  }
}

/** Raises one phone notification now. */
export async function showLocalAlert(n: Pick<AppNotification, 'subject' | 'body' | 'entityType' | 'entityId'>): Promise<void> {
  try {
    await Notifications.scheduleNotificationAsync({
      content: {
        title: n.subject,
        body: n.body,
        data: { entityType: n.entityType ?? null, entityId: n.entityId ?? null } satisfies AlertData,
        ...(Platform.OS === 'android' ? { channelId: CHANNEL } : {}),
      } as Notifications.NotificationContentInput,
      trigger: null,
    })
  } catch {
    /* ignore */
  }
}

const handledResponses = new Set<string>()

/** Calls back once per tapped phone notification (a pushed one or a local one); returns the unsubscribe. */
export function onAlertTapped(handler: (data: AlertData, responseId: string) => void): () => void {
  try {
    const sub = Notifications.addNotificationResponseReceivedListener((response) => {
      const id = response.notification.request.identifier
      if (handledResponses.has(id)) return
      handledResponses.add(id)
      handler((response.notification.request.content.data ?? {}) as AlertData, id)
    })
    return () => sub.remove()
  } catch {
    return () => undefined
  }
}
