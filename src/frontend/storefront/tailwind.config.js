import tokens from './tokens.preset.cjs'

/** @type {import('tailwindcss').Config} */
export default {
  presets: [tokens],
  content: ['./index.html', './src/**/*.{vue,js,ts,jsx,tsx}'],
  theme: {
    extend: {},
  },
  plugins: [],
}
