import type { Expense, InventoryItem, Product, RecipeLine, Sale } from '@/types/models'

export const pesosToCentavos = (value: number | string): number =>
  Math.round(Number(value || 0) * 100)

export const centavosToPesos = (value: number): number => value / 100

export function weightedAverageCost(
  currentQuantity: number,
  currentAverageCostCentavos: number,
  addedQuantity: number,
  addedTotalCostCentavos: number
): number {
  const resultingQuantity = currentQuantity + addedQuantity
  if (resultingQuantity <= 0) return 0
  const currentValue = currentQuantity * currentAverageCostCentavos
  return Math.round((currentValue + addedTotalCostCentavos) / resultingQuantity)
}

export function recipeCostCentavos(
  recipe: RecipeLine[],
  inventoryItems: InventoryItem[]
): number {
  return recipe.reduce((total, line) => {
    const item = inventoryItems.find((candidate) => candidate.id === line.inventoryItemId)
    return total + Math.round(line.quantity * (item?.averageCostCentavos ?? 0))
  }, 0)
}

export function productCostCentavos(product: Product, inventoryItems: InventoryItem[]): number {
  if (product.inventoryMode === 'prepared' && product.finishedInventoryItemId) {
    const finishedAverage = inventoryItems.find((item) => item.id === product.finishedInventoryItemId)?.averageCostCentavos ?? 0
    return finishedAverage > 0 ? finishedAverage : recipeCostCentavos(product.recipe, inventoryItems)
  }
  if (product.inventoryMode === 'prepared') {
    return recipeCostCentavos(product.recipe, inventoryItems)
  }
  return product.manualCostCentavos
}

export function migrateLegacyMadeToOrderProduct(product: Product, inventoryItems: InventoryItem[]): Product {
  if ((product.inventoryMode as string) !== 'made_to_order') return product
  const recipeCost = recipeCostCentavos(product.recipe, inventoryItems)
  return {
    ...product,
    inventoryMode: 'untracked',
    manualCostCentavos: recipeCost || product.manualCostCentavos,
    recipe: [],
    updatedAt: nowIso()
  }
}

export function grossMarginPercent(priceCentavos: number, costCentavos: number): number {
  return priceCentavos > 0 ? ((priceCentavos - costCentavos) / priceCentavos) * 100 : 0
}

export function markupPercent(costCentavos: number, priceCentavos: number): number {
  return costCentavos > 0 ? ((priceCentavos - costCentavos) / costCentavos) * 100 : 0
}

export const localDateKey = (date = new Date()): string => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function currentSalesTotals(sales: Sale[], expenses: Expense[] = [], today = new Date()): {
  weeklyRevenueCentavos: number
  weeklyExpenseCentavos: number
  weeklyProfitCentavos: number
  monthlyRevenueCentavos: number
  monthlyExpenseCentavos: number
  monthlyProfitCentavos: number
} {
  const todayKey = localDateKey(today)
  const weekStart = new Date(today)
  weekStart.setHours(0, 0, 0, 0)
  const daysSinceMonday = (weekStart.getDay() + 6) % 7
  weekStart.setDate(weekStart.getDate() - daysSinceMonday)
  const weekStartKey = localDateKey(weekStart)
  const monthStartKey = `${todayKey.slice(0, 7)}-01`

  const totals = sales.reduce((result, sale) => {
    if (sale.saleDate >= monthStartKey && sale.saleDate <= todayKey) {
      result.monthlyRevenueCentavos += sale.totalRevenueCentavos
      result.monthlyProfitCentavos += sale.totalRevenueCentavos - sale.totalCostCentavos
    }
    if (sale.saleDate >= weekStartKey && sale.saleDate <= todayKey) {
      result.weeklyRevenueCentavos += sale.totalRevenueCentavos
      result.weeklyProfitCentavos += sale.totalRevenueCentavos - sale.totalCostCentavos
    }
    return result
  }, {
    weeklyRevenueCentavos: 0,
    weeklyExpenseCentavos: 0,
    weeklyProfitCentavos: 0,
    monthlyRevenueCentavos: 0,
    monthlyExpenseCentavos: 0,
    monthlyProfitCentavos: 0
  })

  for (const expense of expenses) {
    if (expense.expenseDate >= monthStartKey && expense.expenseDate <= todayKey) {
      totals.monthlyExpenseCentavos += expense.amountCentavos
      totals.monthlyProfitCentavos -= expense.amountCentavos
    }
    if (expense.expenseDate >= weekStartKey && expense.expenseDate <= todayKey) {
      totals.weeklyExpenseCentavos += expense.amountCentavos
      totals.weeklyProfitCentavos -= expense.amountCentavos
    }
  }

  return totals
}

export const makeId = (): string =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`

export const nowIso = (): string => new Date().toISOString()
