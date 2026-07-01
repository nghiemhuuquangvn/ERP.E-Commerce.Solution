// Facebook JS SDK loader — loaded via script tag (no npm package, per Facebook's
// own recommended integration), not bundled by Vite. Placeholder App ID only in
// this environment (see .env.example) — no live Facebook app registration exists
// here.
declare global {
  interface Window {
    FB?: {
      init: (params: { appId: string; cookie: boolean; xfbml: boolean; version: string }) => void
      login: (
        callback: (response: {
          authResponse: { accessToken: string; userID: string } | null
          status: string
        }) => void,
        options?: { scope: string },
      ) => void
    }
    fbAsyncInit?: () => void
  }
}

let loadPromise: Promise<void> | null = null

export function loadFacebookSdk(): Promise<void> {
  if (window.FB) return Promise.resolve()
  if (loadPromise) return loadPromise

  loadPromise = new Promise((resolve) => {
    window.fbAsyncInit = () => {
      window.FB?.init({
        appId: (import.meta.env.VITE_FACEBOOK_APP_ID as string | undefined) ?? '',
        cookie: true,
        xfbml: false,
        version: 'v21.0',
      })
      resolve()
    }

    const script = document.createElement('script')
    script.src = 'https://connect.facebook.net/en_US/sdk.js'
    script.async = true
    script.defer = true
    document.body.appendChild(script)
  })

  return loadPromise
}
