import fs from 'node:fs/promises'
import path from 'node:path'
import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const root = fileURLToPath(new URL('../', import.meta.url))
const brand = path.join(root, 'public/brand')
const read = name => fs.readFile(path.join(brand, name))
const pixels = async name => sharp(await read(name)).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const inventory = JSON.parse(await read('asset-inventory.json'))
for (const item of inventory.pngs) {
  const meta = await sharp(await read(item.file)).metadata()
  assert.equal(meta.width, item.width, item.file)
  assert.equal(meta.height, item.height, item.file)
}
const icon = await pixels('mashal-icon-512.png')
for (const name of ['monochrome', 'black', 'white', 'reversed']) {
  const variant = await pixels(`mashal-${name}-512.png`)
  for (let i = 3; i < icon.data.length; i += 4) {
    assert.equal(variant.data[i], icon.data[i], `${name}: silhouette/alpha differs`)
  }
}
// Interior channels and outside corners must be actually transparent.
for (const [x, y] of [[0, 0], [256, 260], [256, 460]]) {
  assert.equal(icon.data[(y * 512 + x) * 4 + 3], 0, 'Painted background in transparent icon')
}
for (const [name, rgb] of [['monochrome', [11, 31, 58]], ['black', [0, 0, 0]], ['white', [255, 255, 255]]]) {
  const { data } = await pixels(`mashal-${name}-512.png`)
  for (let i = 0; i < data.length; i += 4) {
    if (data[i + 3] === 255) assert.deepEqual([...data.subarray(i, i + 3)], rgb, `${name}: unexpected ink`)
  }
  assert(data[(105 * 512 + 256) * 4 + 3] > 0, `${name}: missing diamond`)
}
// Check opaque app tiles and artwork safety under a circular platform crop.
const app = await pixels('mashal-app-icon-512.png')
for (let y = 0; y < 512; y++) for (let x = 0; x < 512; x++) {
  const i = (y * 512 + x) * 4
  assert.equal(app.data[i + 3], 255, 'App tile has transparent pixels')
  if (app.data[i] !== 11 || app.data[i + 1] !== 31 || app.data[i + 2] !== 58) {
    assert(Math.hypot(x - 255.5, y - 255.5) < 204.8, 'Artwork outside maskable safe circle')
  }
}
for (const [publicName, assetName] of [
  ['favicon.svg', 'mashal-favicon.svg'], ['favicon.ico', 'mashal-favicon.ico'],
  ['apple-touch-icon.png', 'mashal-app-icon-180.png'],
  ['pwa-192x192.png', 'mashal-app-icon-192.png'],
  ['pwa-512x512.png', 'mashal-app-icon-512.png'],
  ['pwa-maskable-512x512.png', 'mashal-app-icon-512.png'],
]) assert((await fs.readFile(path.join(root, 'public', publicName))).equals(await read(assetName)), publicName)
for (const name of ['mashal-favicon.ico', 'mashal-desktop.ico']) {
  const ico = await read(name)
  assert.equal(ico.readUInt16LE(2), 1)
  assert.equal(ico.readUInt16LE(4), inventory.icoSizes.length)
  const sizes = []
  for (let n = 0; n < ico.readUInt16LE(4); n++) {
    const p = 6 + 16 * n, size = ico[p] || 256
    assert.equal(ico[p + 1] || 256, size)
    const length = ico.readUInt32LE(p + 8), offset = ico.readUInt32LE(p + 12)
    assert(offset + length <= ico.length)
    assert.equal(ico.readUInt32LE(offset), 40, 'ICO frame must be a BMP DIB')
    assert.equal(ico.readUInt32LE(offset + 4), size)
    assert.equal(ico.readUInt32LE(offset + 8), size * 2)
    const expected = await pixels(`mashal-favicon-${size}.png`)
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      const a = offset + 40 + ((size - 1 - y) * size + x) * 4
      const b = (y * size + x) * 4
      assert.deepEqual([ico[a + 2], ico[a + 1], ico[a], ico[a + 3]], [...expected.data.subarray(b, b + 4)], 'ICO pixels differ from PNG')
    }
    sizes.push(size)
  }
  assert.deepEqual(sizes.sort((a, b) => a - b), inventory.icoSizes)
}
for (const name of await fs.readdir(brand)) {
  if (!name.endsWith('.svg') || name === 'brand-preview.svg') continue
  const data = (await read(name)).toString()
  assert.equal((data.match(/<svg\b/g) || []).length, 1, name)
  assert(!/<text\b|NaN|Infinity/.test(data), name)
}
console.log('PASS: PNG dimensions, shared silhouettes, true transparency, single-ink diamond, maskable safe area, public copies, outlined type, and pixel-exact ICO frames.')
