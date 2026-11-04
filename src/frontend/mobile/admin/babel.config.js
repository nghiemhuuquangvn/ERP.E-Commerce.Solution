// Some packages (zustand's devtools) read import.meta.env, which Metro on SDK 51 cannot run as a classic script. Give them a constant.
const importMetaEnv = () => ({
  visitor: {
    MetaProperty(path) {
      if (path.node.meta.name === 'import' && path.node.property.name === 'meta') {
        path.replaceWithSourceString('({ env: { MODE: process.env.NODE_ENV } })')
      }
    },
  },
})

module.exports = function (api) {
  api.cache(true)
  // Jest renders with plain React Native: className is ignored there, tests assert roles, labels and state.
  if (process.env.NODE_ENV === 'test') return { presets: ['babel-preset-expo'] }
  return {
    presets: [
      ['babel-preset-expo', { jsxImportSource: 'nativewind' }],
      'nativewind/babel',
    ],
    plugins: [importMetaEnv],
  }
}
