/** @type {import('tailwindcss').Config} */
// ألوان وخطوط الهوية المعتمدة لخبراء التسويق (راجع brand/BRAND-IDENTITY.md)
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        navy: {
          DEFAULT: '#243763',
          900: '#141F3A',
          800: '#1B2A4E',
          600: '#2F4880',
          100: '#E4E8F1',
          50: '#F1F4F9',
        },
        orange: {
          DEFAULT: '#FF6E31',
          600: '#ED5A1C',
          100: '#FFE5D7',
          50: '#FFF3EC',
        },
        ink: { DEFAULT: '#1A2030', 2: '#52596B', 3: '#8A91A3' },
        paper: '#FFFFFF',
        offwhite: '#F6F7FA',
        line: { DEFAULT: '#E6E9F0', 2: '#D4D9E4' },
        success: '#1E9E6A',
        alert: '#E04141',
        gold: '#E0A82E',
      },
      fontFamily: {
        sans: ['"Tajawal"', 'system-ui', 'sans-serif'],
        en: ['"Inter"', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
