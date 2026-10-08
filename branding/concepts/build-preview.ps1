param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root '..\..\scripts\brand-drawing.ps1') -FontRoot (Join-Path $root 'fonts')

function Mark([string]$Name, [float]$X, [float]$Y, [float]$Size, [string]$Color) {
    SvgMark (Join-Path $root "$Name.svg") $X $Y $Size $Color
}

$options = @(
    @{ File = 'open-ledger'; Name = 'A / Open Ledger'; Idea = 'Clear records, checked work.'; Risk = 'Most literal; check detail is quieter at 16 px.' },
    @{ File = 'stock-flow'; Name = 'B / Stock Flow'; Idea = 'Goods and transactions in one ordered whole.'; Risk = 'Strong at small sizes; more stock-led than record-led.' },
    @{ File = 'balanced-record'; Name = 'C / Balanced Record'; Idea = 'A Business Aid initial built from two balanced entries.'; Risk = 'Distinctive initial; less literal about sales and stock.' }
)
$svg = [System.Text.StringBuilder]::new()
[void]$svg.Append('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1320 1180"><title>Business Aid by RUACH - artwork selection, not approved release assets</title><rect width="1320" height="1180" fill="#F7F3ED"/>')
[void]$svg.Append((TextPath 'Business Aid by RUACH' 'Manrope' 44 48 32 '#141821'))
[void]$svg.Append((TextPath 'Three product emblems. One approved parent identity. Artwork selection pending.' 'Inter' 20 48 96 '#696270'))
for ($i = 0; $i -lt $options.Count; $i++) {
    $o = $options[$i]
    $y = 160 + 330 * $i
    [void]$svg.Append("<path d=`"M48 $y H1272`" stroke=`"#D6CDD2`"/>")
    [void]$svg.Append((TextPath $o.Name 'Manrope' 24 48 ($y + 20) '#141821'))
    [void]$svg.Append((TextPath $o.Idea 'Inter' 17 48 ($y + 62) '#696270'))
    $primary = (Mark $o.File 0 0 96 '#B21F32') + (TextPath 'Business Aid' 'Manrope' 38 124 9 '#141821') + (TextPath 'by RUACH' 'Inter' 20 126 61 '#696270')
    $lockup = "<svg xmlns=`"http://www.w3.org/2000/svg`" viewBox=`"0 0 420 100`"><title>$($o.Name) - Business Aid by RUACH concept</title>$primary</svg>"
    [System.IO.File]::WriteAllText((Join-Path $root "$($o.File)-lockup.svg"), $lockup)
    [void]$svg.Append("<g transform=`"translate(48 $($y+113))`">$primary</g>")
    [void]$svg.Append("<rect x=`"570`" y=`"$($y+106)`" width=`"468`" height=`"120`" rx=`"16`" fill=`"#141821`"/>")
    [void]$svg.Append((Mark $o.File 590 ($y + 121) 84 '#FFFFFF'))
    [void]$svg.Append((TextPath 'Business Aid' 'Manrope' 34 694 ($y + 123) '#FFFFFF'))
    [void]$svg.Append((TextPath 'by RUACH' 'Inter' 18 696 ($y + 172) '#F7F3ED'))
    [void]$svg.Append("<rect x=`"1090`" y=`"$($y+106)`" width=`"112`" height=`"112`" rx=`"24`" fill=`"#B21F32`"/>")
    [void]$svg.Append((Mark $o.File 1112 ($y + 128) 68 '#FFFFFF'))
    foreach ($size in @(16, 24, 32)) {
        $x = 1090 + (@(16, 24, 32).IndexOf($size) * 45)
        [void]$svg.Append((Mark $o.File $x ($y + 250) $size '#141821'))
    }
    [void]$svg.Append((TextPath $o.Risk 'Inter' 16 48 ($y + 262) '#696270'))
}
[void]$svg.Append((TextPath 'C is agent-selected for reversible integration. Owner review pending. Parent mark unchanged. Not deployed.' 'Inter' 16 48 1130 '#696270'))
[void]$svg.Append('</svg>')
[System.IO.File]::WriteAllText((Join-Path $root 'comparison.svg'), $svg.ToString())
$fonts.Dispose()
Write-Host 'Generated three outlined lockups and comparison.svg. No release assets were generated.'
