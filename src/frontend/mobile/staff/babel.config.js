module.exports = function (api) {
  api.cache(true)
  // Jest renders with plain React Native: className is ignored there, tests assert roles, labels and state.
  if (process.env.NODE_ENV === 'test') return { presets: ['babel-preset-expo'] }
  return {
    presets: [
      ['babel-preset-expo', { jsxImportSource: 'nativewind' }],
      'nativewind/babel',
    ],
  }
}
