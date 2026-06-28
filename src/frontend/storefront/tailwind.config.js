/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{vue,js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: { 500: '#e11d48', 600: '#be123c' },
      },
    },
  },
  plugins: [],
}
