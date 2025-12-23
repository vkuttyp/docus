<script setup lang="ts">
import type { Collections } from '@nuxt/content'
import { addPrerenderPath } from '../../utils/prerender'

definePageMeta({
  layout: 'default',
})

const route = useRoute()
const { locale, isEnabled } = useDocusI18n()

const collectionName = computed(() => isEnabled.value ? `landing_${locale.value}` : 'landing')

const { data: page } = await useAsyncData(route.path, () => queryCollection(collectionName.value as keyof Collections).first())

if (!page.value) {
  throw createError({ statusCode: 404, statusMessage: 'Page not found', fatal: true })
}

addPrerenderPath(route.path)
</script>

<template>
  <UPage v-if="page">
    <ContentRenderer :value="page" />
  </UPage>
</template>
