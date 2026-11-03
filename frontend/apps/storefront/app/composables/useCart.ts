import { unwrap, type Schema } from '@comtest/api-client'
import { useApi, useAuthStore } from '@comtest/auth'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

export type Cart = Schema<'CartDto'>
export type CartLine = Schema<'CartLineDto'>

/**
 * The cart lives on the server, and the browser only shows it: every answer replaces what is shown,
 * so no price or total is ever worked out here. It is not loaded during server rendering, as it
 * belongs to one visitor.
 *
 * The key includes the user. Signing in therefore asks again, and that request is the moment the
 * server merges the guest's cart into the account.
 */
export function useCart() {
  const api = useApi()
  const auth = useAuthStore()
  const queryClient = useQueryClient()
  const { locale } = useI18n()

  const key = computed(() => ['cart', auth.claims?.userId ?? 'guest', locale.value])
  const drawerOpen = useState('cart-drawer-open', () => false)

  const query = useQuery({
    queryKey: key,
    queryFn: () => unwrap(api.client.GET('/api/v1/cart')),
    enabled: computed(() => import.meta.client && auth.status !== 'unknown'),
    staleTime: 0,
  })

  const store = (cart: Cart) => queryClient.setQueryData(key.value, cart)

  const add = useMutation({
    mutationFn: (body: Schema<'AddCartItemRequest'>) =>
      unwrap(api.client.POST('/api/v1/cart/items', { body })),
    onSuccess: store,
  })

  const setQuantity = useMutation({
    mutationFn: ({ variantId, quantity }: { variantId: string; quantity: number }) =>
      unwrap(
        api.client.PUT('/api/v1/cart/items/{variantId}', {
          params: { path: { variantId } },
          body: { quantity },
        }),
      ),
    onSuccess: store,
  })

  const remove = useMutation({
    mutationFn: (variantId: string) =>
      unwrap(
        api.client.DELETE('/api/v1/cart/items/{variantId}', { params: { path: { variantId } } }),
      ),
    onSuccess: store,
  })

  return {
    cart: query.data,
    isLoading: query.isPending,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
    add,
    setQuantity,
    remove,
    drawerOpen,
    itemCount: computed(() => query.data.value?.itemCount ?? 0),
    /** After an order the server's cart is empty, and this makes the page show that. */
    invalidate: () => queryClient.invalidateQueries({ queryKey: ['cart'] }),
  }
}
