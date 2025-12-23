export default defineEventHandler(() => {
  // Return empty response for service worker requests
  return new Response('', {
    status: 404,
    headers: {
      'Content-Type': 'application/javascript',
    },
  })
})
