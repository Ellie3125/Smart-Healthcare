/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: "#006a41",
        "primary-hover": "#005231",
        "primary-container": "#028654",
        "on-primary": "#ffffff",
        secondary: "#076c43",
        "secondary-container": "#9ef5c0",
        background: "#ebfef2",
        surface: "#ebfef2",
        "surface-container": "#e0f2e7",
        "surface-container-low": "#e5f8ed",
        "surface-container-lowest": "#ffffff",
        "surface-variant": "#d4e7dc",
        "on-surface": "#0f1f18",
        "on-surface-variant": "#3e4941",
        outline: "#6e7a71",
        "outline-variant": "#bdcabf",
        error: "#ba1a1a",
        "error-container": "#ffdad6",
        tertiary: "#b6171e",
      },
      fontFamily: {
        sans: ["Be Vietnam Pro", "sans-serif"],
      },
    },
  },
  plugins: [],
}

