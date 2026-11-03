// Downloads the API description from a running backend. The result is committed, so that the
// frontend builds without one and a change in the API contract shows up in code review.
import { writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

const apiUrl = process.env.API_URL ?? 'http://localhost:5050'
const target = fileURLToPath(new URL('../openapi/v1.json', import.meta.url))

const response = await fetch(`${apiUrl}/openapi/v1.json`).catch((cause) => {
  throw new Error(`Backend is not reachable at ${apiUrl}. Start it first: dotnet run`, { cause })
})

if (!response.ok) {
  throw new Error(`${apiUrl}/openapi/v1.json answered ${response.status}`)
}

await writeFile(target, `${JSON.stringify(await response.json(), null, 2)}\n`)
console.log(`Saved ${target}`)
