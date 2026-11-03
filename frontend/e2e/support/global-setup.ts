import { prepareDemoOwner, removeTestProducts } from './demo-store'

export default async function globalSetup(): Promise<void> {
  await prepareDemoOwner()
  // The store's plan allows 100 products, so what earlier runs left behind has to go.
  await removeTestProducts()
}
