import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { Permission, ROLE_PERMISSIONS } from '../src/app/permissions'

const rolesFile = join(
  import.meta.dirname,
  '../../../../backend/src/ComTest.Application/Modules/Identity/Contracts/Roles.cs',
)

const source = readFileSync(rolesFile, 'utf8')

function constants(): Record<string, string> {
  return Object.fromEntries(
    [...source.matchAll(/public const string (\w+) = "([a-z.]+)";/g)].map((match) => [
      match[1] as string,
      match[2] as string,
    ]),
  )
}

function listed(name: string): string[] {
  const body = new RegExp(`${name}\\s*=\\s*\\[([^\\]]*)\\]`).exec(source)?.[1] ?? ''

  return body
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

describe('the permission matrix', () => {
  const values = constants()

  it('names the same permissions as the backend', () => {
    expect(Object.values(Permission).sort()).toEqual(
      Object.entries(values)
        .filter(([key]) => key !== 'ClaimType')
        .map(([, value]) => value)
        .sort(),
    )
  })

  it.each([
    ['Manager', 'ManagerPermissions'],
    ['Staff', 'StaffPermissions'],
  ] as const)('gives %s what the backend gives it', (role, list) => {
    const expected = listed(list).map((name) => values[name])

    expect([...ROLE_PERMISSIONS[role]].sort()).toEqual(expected.sort())
  })

  it('gives the owner what the backend gives it', () => {
    const expected = listed('OwnerPermissions').flatMap((name) =>
      name === '.. ManagerPermissions'
        ? listed('ManagerPermissions').map((item) => values[item])
        : [values[name]],
    )

    expect([...ROLE_PERMISSIONS.StoreOwner].sort()).toEqual(expected.sort())
  })
})
