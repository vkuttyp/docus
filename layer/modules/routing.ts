import { defineNuxtModule, extendPages, createResolver } from '@nuxt/kit'

export default defineNuxtModule({
  meta: {
    name: 'routing',
  },
  async setup(_options, nuxt) {
    const { resolve } = createResolver(import.meta.url)

    const isI18nEnabled = !!(nuxt.options.i18n && nuxt.options.i18n.locales)

    // Ensure useDocusI18n is available in the app
    nuxt.hook('imports:extend', (imports) => {
      if (imports.some(i => i.name === 'useDocusI18n')) return

      imports.push({
        name: 'useDocusI18n',
        from: resolve('../app/composables/useDocusI18n'),
      })
    })

    extendPages((pages) => {
      const landingTemplate = resolve('../app/templates/landing.vue')

      if (isI18nEnabled) {
        // Add a catch-all root redirect for i18n
        const defaultLocale = (nuxt.options.i18n as any)?.defaultLocale || 'en'
        pages.push({
          name: 'index-redirect',
          path: '/',
          redirect: `/${defaultLocale}`,
        })
      }
      else {
        pages.push({
          name: 'index',
          path: '/',
          file: landingTemplate,
        })
      }
    })
  },
})
