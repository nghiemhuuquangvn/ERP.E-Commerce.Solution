import * as SecureStore from 'expo-secure-store'

const KEY = 'erp-admin-refresh-token'

// The refresh token is the only secret kept on the phone (the access token lives in memory only; it is ~10 KB because it lists
// every permission, which SecureStore does not guarantee to hold). Every call tolerates a failing keystore: no token = signed out.
export async function loadRefreshToken(): Promise<string | null> {
  try {
    return await SecureStore.getItemAsync(KEY)
  } catch {
    return null
  }
}

export async function saveRefreshToken(token: string): Promise<void> {
  try {
    await SecureStore.setItemAsync(KEY, token)
  } catch {
    /* the session then lasts until the app is closed */
  }
}

export async function clearRefreshToken(): Promise<void> {
  try {
    await SecureStore.deleteItemAsync(KEY)
  } catch {
    /* nothing to clear */
  }
}
