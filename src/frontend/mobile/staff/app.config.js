// Static values live in app.json (so `eas init` can write the project id there). This file only adds what depends on the build profile.
// Android refuses plain http:// by default. The internal "preview" build talks to the test stack over the LAN (http://shop.glcheck.lan),
// so it allows cleartext traffic; the "production" profile (APP_VARIANT=production) does not and must use https.
module.exports = ({ config }) => {
  const production = process.env.APP_VARIANT === 'production'
  return {
    ...config,
    plugins: [
      ...(config.plugins ?? []),
      ...(production ? [] : [['expo-build-properties', { android: { usesCleartextTraffic: true } }]]),
    ],
  }
}
