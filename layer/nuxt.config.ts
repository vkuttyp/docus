import { extendViteConfig, createResolver, useNuxt } from '@nuxt/kit'

const { resolve } = createResolver(import.meta.url)

export default defineNuxtConfig({
  modules: [
    resolve('./modules/config'),
    resolve('./modules/routing'),
    resolve('./modules/css'),
    '@nuxt/ui',
    '@nuxt/content',
    '@nuxt/image',
    '@nuxtjs/robots',
    '@nuxtjs/mcp-toolkit',
    'nuxt-og-image',
    'nuxt-llms',
    () => {
      extendViteConfig((config) => {
        config.optimizeDeps ||= {}
        config.optimizeDeps.exclude ||= []
        // Exclude server-side markdown processing dependencies
        config.optimizeDeps.exclude.push(
          '@nuxt/content > slugify',
          '@nuxtjs/mdc > remark-gfm',
          '@nuxtjs/mdc > remark-emoji',
          '@nuxtjs/mdc > remark-mdc',
          '@nuxtjs/mdc > remark-rehype',
          '@nuxtjs/mdc > rehype-raw',
          '@nuxtjs/mdc > parse5',
          '@nuxtjs/mdc > unist-util-visit',
          '@nuxtjs/mdc > unified',
          '@nuxtjs/mdc > debug'
        )
      })
    },
  ],
  devtools: {
    enabled: true,
  },
  content: {
    build: {
      markdown: {
        highlight: {
          langs: ['bash', 'diff', 'json', 'js', 'ts', 'html', 'css', 'vue', 'shell', 'mdc', 'md', 'yaml'],
        },
        remarkPlugins: {
          'remark-mdc': {
            options: {
              autoUnwrap: true,
            },
          },
        },
      },
    },
  },
  experimental: {
    asyncContext: true,
  },
  compatibilityDate: '2025-07-22',
  nitro: {
    prerender: {
      crawlLinks: true,
      failOnError: false,
      autoSubfolderIndex: false,
    },
    compatibilityDate: {
      // Don't generate observability routes for now
      vercel: '2025-07-14',
    },
  },
  hooks: {
    'nitro:config'(nitroConfig) {
      const nuxt = useNuxt()

      const i18nOptions = nuxt.options.i18n

      const routes: string[] = []
      if (!i18nOptions) {
        routes.push('/')
      }
      else {
        routes.push(...(i18nOptions.locales?.map(locale => typeof locale === 'string' ? `/${locale}` : `/${locale.code}`) || []))
      }

      nitroConfig.prerender = nitroConfig.prerender || {}
      nitroConfig.prerender.routes = nitroConfig.prerender.routes || []
      nitroConfig.prerender.routes.push(...(routes || []))
    },
  },
  icon: {
    provider: 'iconify',
  },
})
