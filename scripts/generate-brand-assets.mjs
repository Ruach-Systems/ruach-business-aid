import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'
import sharp from 'sharp'
import pngToIco from 'png-to-ico'
import opentype from 'opentype.js'

const root = fileURLToPath(new URL('../', import.meta.url))
const brand = path.join(root, 'public/brand')
const source = path.join(root, 'scripts/brand-source')
const navy = '#0B1F3A', red = '#A61B29', white = '#F7F9FC', slate = '#536273'
const master = await fs.readFile(path.join(source, 'shield-master.svg'), 'utf8')
const geometry = master.slice(master.indexOf('<g id="shield"'), master.lastIndexOf('</svg>'))
const font = async (name) => {
  const b = await fs.readFile(path.join(source, name))
  return opentype.parse(b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength))
}
const mainFont = await font('Montserrat-ExtraBold.ttf')
const subFont = await font('Montserrat-Bold.ttf')
const svg = (w, h, body, title = 'Mashal Systems') => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${w} ${h}" width="${w}" height="${h}" role="img" aria-label="${title}">${body}</svg>\n`
const mark = (ink = navy, accent = red) => geometry.replaceAll('currentColor', ink).replaceAll(red, accent)
const place = (x, y, scale, body) => `<g transform="translate(${x} ${y}) scale(${scale})">${body}</g>`
// Outlined type prevents font substitutions on other devices and in print.
function lettering(text, face, tracking, x, y, w, h, fill) {
  const outline = new opentype.Path()
  let cursor = 0
  const glyphs = face.stringToGlyphs(text)
  for (let i = 0; i < glyphs.length; i++) {
    const g = glyphs[i]
    outline.extend(g.getPath(cursor, 0, 100))
    cursor += g.advanceWidth * 100 / face.unitsPerEm + tracking
    if (i + 1 < glyphs.length) cursor += face.getKerningValue(g, glyphs[i + 1]) * 100 / face.unitsPerEm
  }
  const b = outline.getBoundingBox()
  return `<g transform="translate(${x} ${y}) scale(${w / (b.x2 - b.x1)} ${h / (b.y2 - b.y1)}) translate(${-b.x1} ${-b.y1})"><path fill="${fill}" d="${outline.toPathData(3)}"/></g>`
}
function words(x, y, w, ink = navy, sub = slate) {
  return lettering('MASHAL', mainFont, 3, x, y, w, w * .145, ink)
    + lettering('SYSTEMS', subFont, 28, x + w * .13, y + w * .20, w * .74, w * .067, sub)
}
const icon = (ink = navy, accent = red) => svg(360, 360, place(42, 23, 1, mark(ink, accent)))
const horizontal = (ink = navy, accent = red, sub = slate, bg = '') => svg(950, 320,
  (bg ? `<rect width="950" height="320" fill="${bg}"/>` : '')
  + place(25, 18.7, .9, mark(ink, accent)) + words(305, 78, 615, ink, sub))
const stacked = (ink = navy, accent = red, sub = slate, bg = '') => svg(640, 640,
  (bg ? `<rect width="640" height="640" fill="${bg}"/>` : '')
  + place(182, 63, 1, mark(ink, accent)) + words(90, 421, 460, ink, sub))
const tile = (rounded) => svg(512, 512, `<rect width="512" height="512" rx="${rounded ? 100 : 0}" fill="${navy}"/>`
  + place(93.64, 71.28, 1.17652173913, mark(white, red)))
const files = {
  'mashal-icon.svg': icon(),
  'mashal-monochrome.svg': icon(navy, navy),
  'mashal-black.svg': icon('#000000', '#000000'),
  'mashal-white.svg': icon('#FFFFFF', '#FFFFFF'),
  'mashal-reversed.svg': icon(white, red),
  'mashal-wordmark.svg': horizontal(),
  'mashal-wordmark-monochrome.svg': horizontal(navy, navy, navy),
  'mashal-wordmark-reversed.svg': horizontal(white, red, white),
  'mashal-dark.svg': horizontal(white, red, white, navy),
  'mashal-stacked.svg': stacked(),
  'mashal-stacked-dark.svg': stacked(white, red, white, navy),
  'mashal-stacked-monochrome.svg': stacked(navy, navy, navy),
  'mashal-logotype.svg': svg(680, 230, words(30, 33, 620)),
  'mashal-favicon.svg': tile(true),
  'mashal-app-icon.svg': tile(false),
}
await fs.mkdir(brand, { recursive: true })
const inventory = []
for (const [name, content] of Object.entries(files)) {
  assert.equal((content.match(/<svg\b/g) || []).length, 1)
  assert(!content.includes('<text'))
  await fs.writeFile(path.join(brand, name), content)
}
async function png(sourceName, name, width, height = width) {
  const data = await sharp(Buffer.from(files[sourceName]), { density: 300 })
    .resize(width, height, { fit: 'contain', background: '#00000000' }).png().toBuffer()
  await fs.writeFile(path.join(brand, name), data)
  const meta = await sharp(data).metadata()
  assert.equal(meta.width, width); assert.equal(meta.height, height)
  inventory.push({ file: name, width, height })
}
for (const name of ['icon', 'monochrome', 'black', 'white', 'reversed']) {
  for (const size of [256, 512, 1024]) await png(`mashal-${name}.svg`, `mashal-${name}-${size}.png`, size)
}
for (const name of ['wordmark', 'wordmark-monochrome', 'wordmark-reversed', 'dark']) {
  await png(`mashal-${name}.svg`, `mashal-${name}-1520x512.png`, 1520, 512)
  await png(`mashal-${name}.svg`, `mashal-${name}-3040x1024.png`, 3040, 1024)
}
for (const name of ['stacked', 'stacked-dark', 'stacked-monochrome']) await png(`mashal-${name}.svg`, `mashal-${name}-1280.png`, 1280)
await png('mashal-logotype.svg', 'mashal-logotype-1360x460.png', 1360, 460)
for (const size of [16, 24, 32, 48, 64, 128, 180, 192, 256, 512]) await png('mashal-favicon.svg', `mashal-favicon-${size}.png`, size)
for (const size of [48, 72, 96, 144, 180, 192, 256, 512, 1024]) await png('mashal-app-icon.svg', `mashal-app-icon-${size}.png`, size)
const icoSizes = [16, 24, 32, 48, 64, 128, 256]
const ico = await pngToIco(icoSizes.map(n => path.join(brand, `mashal-favicon-${n}.png`)))
assert.equal(ico.readUInt16LE(0), 0); assert.equal(ico.readUInt16LE(2), 1)
assert.equal(ico.readUInt16LE(4), icoSizes.length)
await fs.writeFile(path.join(brand, 'mashal-favicon.ico'), ico)
await fs.writeFile(path.join(brand, 'mashal-desktop.ico'), ico)
// Replace the old malformed root SVGs with one complete document each.
await fs.writeFile(path.join(root, 'public/favicon.svg'), files['mashal-favicon.svg'])
await fs.writeFile(path.join(root, 'public/favicon.ico'), ico)
for (const size of [192, 512]) {
  await fs.writeFile(path.join(root, `public/pwa-${size}x${size}.svg`), files['mashal-app-icon.svg'])
  await fs.copyFile(path.join(brand, `mashal-app-icon-${size}.png`), path.join(root, `public/pwa-${size}x${size}.png`))
}
await fs.copyFile(path.join(brand, 'mashal-app-icon-512.png'), path.join(root, 'public/pwa-maskable-512x512.png'))
await fs.copyFile(path.join(brand, 'mashal-app-icon-180.png'), path.join(root, 'public/apple-touch-icon.png'))

// This board uses the actual exported vectors and raster files for visual QA.
const label = (text, x, y, color = slate) => `<text x="${x}" y="${y}" font-family="Arial" font-size="20" letter-spacing="3" fill="${color}">${text}</text>`
const bodyOf = name => files[name].slice(files[name].indexOf('>') + 1, files[name].lastIndexOf('</svg>'))
let board = '<rect width="1536" height="1080" fill="#F7F9FC"/>'
board += '<rect x="26" y="26" width="505" height="448" rx="12" fill="white" stroke="#CCD3DD"/>'
board += '<rect x="550" y="26" width="960" height="448" rx="12" fill="white" stroke="#CCD3DD"/>'
board += label('ICON', 235, 68) + place(98, 98, .96, bodyOf('mashal-icon.svg'))
board += label('WORDMARK', 930, 68) + place(568, 116, .96, bodyOf('mashal-wordmark.svg'))
board += '<rect x="26" y="494" width="452" height="485" rx="12" fill="white" stroke="#CCD3DD"/>'
board += '<rect x="498" y="494" width="522" height="485" rx="12" fill="#0B1F3A"/>'
board += '<rect x="1040" y="494" width="470" height="485" rx="12" fill="white" stroke="#CCD3DD"/>'
board += label('MONOCHROME', 155, 533) + place(80, 571, .96, bodyOf('mashal-monochrome.svg'))
board += place(541, 541, .68, bodyOf('mashal-stacked-dark.svg')) + label('DARK BACKGROUND', 639, 533, white)
board += label('FAVICON / APP ICON', 1127, 533) + place(1135, 601, .55, bodyOf('mashal-favicon.svg'))
board += label('ACTUAL ICON SIZES', 42, 1025)
await fs.writeFile(path.join(brand, 'brand-preview.svg'), svg(1536, 1080, board))
const preview = await sharp(Buffer.from(svg(1536, 1080, board))).png().toBuffer()
const thumbnails = []
let x = 380
for (const size of [16, 24, 32, 48, 64]) {
  thumbnails.push({ input: path.join(brand, `mashal-favicon-${size}.png`), left: x, top: 1000 })
  x += size + 48
}
await sharp(preview).composite(thumbnails).png().toFile(path.join(brand, 'brand-preview.png'))
await fs.writeFile(path.join(brand, 'asset-inventory.json'), JSON.stringify({ master: 'scripts/brand-source/shield-master.svg', pngs: inventory, icoSizes }, null, 2) + '\n')
console.log(`Generated and checked ${Object.keys(files).length} SVG variants, ${inventory.length} PNG exports, and two ${icoSizes.length}-size ICO files.`)
