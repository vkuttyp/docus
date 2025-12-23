export default defineNuxtConfig({
  extends: '@murshisoft/docus',
  compatibilityDate: '2025-01-01',
  llms: {
    domain: 'http://localhost:3000',
  },
})
