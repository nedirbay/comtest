/** Not during development, where a cached file would hide the change someone just made. */
export default defineNuxtPlugin((nuxtApp) => {
  if (import.meta.dev || !('serviceWorker' in navigator)) {
    return
  }

  // After the page has hydrated, so that installing it never competes with the page itself.
  nuxtApp.hook('app:suspense:resolve', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // The site works just the same without it.
    })
  })
})
