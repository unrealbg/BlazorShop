/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.razor",
    "./**/*.cshtml",
    "./**/*.html",
    "./**/*.cs",
    "../BlazorShop.Web.Shared/**/*.razor",
    "../BlazorShop.Web.Shared/**/*.cs"
  ],
  theme: {
    extend: {
      fontFamily: { sans: ["Inter", "ui-sans-serif", "system-ui"] },
      colors: {
        brand: {
          50:  '#f5f7fb',
          100: '#e9edf6',
          200: '#cfd8ea',
          300: '#a9b8d7',
          400: '#7c93c1',
          500: '#5f78b2',
          600: '#4a5f93',
          700: '#3f517c',
          800: '#344163',
          900: '#28324a'
        }
      }
    },
  },
  plugins: [],
}
