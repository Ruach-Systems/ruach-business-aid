import { describe, expect, it } from 'vitest'
import { currentSalesTotals, grossMarginPercent, migrateLegacyMadeToOrderProduct, productCostCentavos, recipeCostCentavos, weightedAverageCost } from './calculations'
import type { Expense, InventoryItem, Product, Sale } from '@/types/models'

describe('inventory costing', () => {
  it('uses weighted average cost for a new stock receipt', () => {
    expect(weightedAverageCost(1000, 8, 1000, 10_000)).toBe(9)
  })

  it('calculates recipe cost from current ingredient averages', () => {
    const items = [
      { id: 'sugar', averageCostCentavos: 8 },
      { id: 'cup', averageCostCentavos: 200 }
    ] as InventoryItem[]
    expect(recipeCostCentavos([
      { inventoryItemId: 'sugar', quantity: 15 },
      { inventoryItemId: 'cup', quantity: 1 }
    ], items)).toBe(320)
  })

  it('calculates gross margin against the selling price', () => {
    expect(grossMarginPercent(3500, 1550)).toBeCloseTo(55.714, 2)
  })

  it('uses recipe cost for a prepared product before its first completed batch', () => {
    const ingredients = [{ id: 'buko', averageCostCentavos: 800 }] as InventoryItem[]
    const product = {
      inventoryMode: 'prepared',
      recipe: [{ inventoryItemId: 'buko', quantity: 1 }]
    } as Product
    expect(productCostCentavos(product, ingredients)).toBe(800)
  })

  it('migrates legacy made-when-sold products to untracked with their recipe cost', () => {
    const ingredients = [{ id: 'buko', averageCostCentavos: 800 }] as InventoryItem[]
    const legacyProduct = {
      inventoryMode: 'made_to_order',
      manualCostCentavos: 0,
      recipe: [{ inventoryItemId: 'buko', quantity: 2 }]
    } as unknown as Product

    expect(migrateLegacyMadeToOrderProduct(legacyProduct, ingredients)).toMatchObject({
      inventoryMode: 'untracked',
      manualCostCentavos: 1600,
      recipe: []
    })
  })
})

describe('sales totals', () => {
  it('totals the current Monday-to-Sunday week and calendar month', () => {
    const sales = [
      { saleDate: '2026-09-01', totalRevenueCentavos: 1_000, totalCostCentavos: 300 },
      { saleDate: '2026-09-14', totalRevenueCentavos: 2_000, totalCostCentavos: 500 },
      { saleDate: '2026-09-15', totalRevenueCentavos: 3_000, totalCostCentavos: 700 },
      { saleDate: '2026-09-16', totalRevenueCentavos: 4_000, totalCostCentavos: 900 },
      { saleDate: '2026-08-31', totalRevenueCentavos: 8_000, totalCostCentavos: 2_000 }
    ] as Sale[]
    const expenses = [
      { expenseDate: '2026-09-01', amountCentavos: 200 },
      { expenseDate: '2026-09-15', amountCentavos: 500 },
      { expenseDate: '2026-09-16', amountCentavos: 1_000 }
    ] as Expense[]

    expect(currentSalesTotals(sales, expenses, new Date(2026, 8, 15))).toEqual({
      weeklyRevenueCentavos: 5_000,
      weeklyExpenseCentavos: 500,
      weeklyProfitCentavos: 3_300,
      monthlyRevenueCentavos: 6_000,
      monthlyExpenseCentavos: 700,
      monthlyProfitCentavos: 3_800
    })
  })
})
