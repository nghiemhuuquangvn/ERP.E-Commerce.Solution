const { getDefaultConfig } = require('expo/metro-config')
const { withNativeWind } = require('nativewind/metro')

const config = getDefaultConfig(__dirname)

// Web: the ESM builds some packages ship (zustand 5 uses import.meta) cannot run as a classic script; take the CommonJS entry.
config.resolver.resolverMainFields = ['react-native', 'browser', 'main']

// NativeWind v4 turns the Tailwind classes into styles through this transformer; without it every className is ignored.
module.exports = withNativeWind(config, { input: './global.css' })
