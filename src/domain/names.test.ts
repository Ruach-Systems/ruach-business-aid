import { describe, expect, it } from 'vitest'
import { cleanEntityName, isEntityNameTaken } from './names'

const entities = [
  { id: 'one', businessId: 'business-a', name: 'Buko Juice' },
  { id: 'two', businessId: 'business-b', name: 'Buko Juice' },
  { id: 'deleted', businessId: 'business-a', name: 'Siomai', deletedAt: '2026-09-15T00:00:00.000Z' }
]

describe('business entity names', () => {
  it('stores names with consistent surrounding and internal whitespace', () => {
    expect(cleanEntityName('  Buko   Juice  ')).toBe('Buko Juice')
  })

  it('treats casing and whitespace variants as duplicates in the same business', () => {
    expect(isEntityNameTaken(' buko   juice ', entities, 'business-a')).toBe(true)
  })

  it('allows the current entity, another business, and previously deleted names', () => {
    expect(isEntityNameTaken('Buko Juice', entities, 'business-a', 'one')).toBe(false)
    expect(isEntityNameTaken('Buko Juice', entities, 'business-c')).toBe(false)
    expect(isEntityNameTaken('Siomai', entities, 'business-a')).toBe(false)
  })
})
