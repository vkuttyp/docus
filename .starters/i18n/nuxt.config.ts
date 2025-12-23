export default defineNuxtConfig({
  extends: '@murshisoft/docus',
  compatibilityDate: '2025-01-01',
  modules: ['@nuxtjs/i18n'],
  i18n: {
    defaultLocale: 'en',
    locales: [{
      code: 'en',
      name: 'English',
    }, {
      code: 'fr',
      name: 'Français',
    }],
  },
})
