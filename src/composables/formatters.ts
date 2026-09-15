export const formatMoney = (centavos: number, maximumFractionDigits = 2): string =>
  new Intl.NumberFormat('en-PH', {
    style: 'currency',
    currency: 'PHP',
    minimumFractionDigits: maximumFractionDigits,
    maximumFractionDigits
  }).format(centavos / 100)

export const formatQuantity = (value: number): string =>
  new Intl.NumberFormat('en-PH', { maximumFractionDigits: 2 }).format(value)

export const formatDate = (value: string): string =>
  new Intl.DateTimeFormat('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(`${value}T00:00:00`))

export const timeAgo = (value?: string): string => {
  if (!value) return 'Not synced yet'
  const seconds = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 1000))
  if (seconds < 60) return 'Synced just now'
  if (seconds < 3600) return `Synced ${Math.floor(seconds / 60)}m ago`
  return `Synced ${Math.floor(seconds / 3600)}h ago`
}
