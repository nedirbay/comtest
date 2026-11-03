import { useAuthStore } from '@comtest/auth'

export default defineNuxtPlugin({
  name: 'auth',
  dependsOn: ['api'],
  setup(nuxtApp) {
    const auth = useAuthStore(nuxtApp.$pinia as never)
    const router = useRouter()
    const localePath = useLocalePath()

    // Whatever ends the session, in this tab or another, nothing of the user's may stay behind.
    auth.onSignedOut(() => {
      ;(nuxtApp.$queryClient as { clear(): void }).clear()

      if (router.currentRoute.value.meta.requiresAuth) {
        void router.replace(localePath('/login'))
      }
    })

    // Only once all of the page has hydrated. The server renders the state where nobody is known to
    // be signed in yet, and layouts hydrate later than the app itself, so an earlier start would
    // make them disagree with the server's HTML. Later calls do nothing, as bootstrap only runs once.
    nuxtApp.hook('app:suspense:resolve', () => {
      void auth.bootstrap()
    })
  },
})
