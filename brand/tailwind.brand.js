// خبراء التسويق · Marketing Experts — Tailwind brand tokens
// انسخ هذا الكائن داخل theme.extend في tailwind.config عند بناء الفرونت إند.
// الخطوط تُحمَّل من Google Fonts:
//   IBM Plex Sans Arabic: wght 300;400;500;600;700
//   Inter: wght 400;500;600;700;800
module.exports = {
  colors: {
    navy: {
      DEFAULT: '#243763',
      900: '#141F3A',
      800: '#1B2A4E',
      600: '#2F4880',
      100: '#E4E8F1',
      50:  '#F1F4F9',
    },
    orange: {
      DEFAULT: '#FF6E31', // CTA
      600: '#ED5A1C',
      100: '#FFE5D7',
      50:  '#FFF3EC',
    },
    ink:     { DEFAULT: '#1A2030', 2: '#52596B', 3: '#8A91A3' },
    paper:   '#FFFFFF',
    offwhite:'#F6F7FA',
    line:    { DEFAULT: '#E6E9F0', 2: '#D4D9E4' },
    success: '#1E9E6A',
    alert:   '#E04141',
    gold:    '#E0A82E',
  },
  fontFamily: {
    sans: ['"IBM Plex Sans Arabic"', 'system-ui', 'sans-serif'], // الواجهة العربية
    en:   ['"Inter"', 'system-ui', 'sans-serif'],                // اللاتينية/الأرقام
  },
};
