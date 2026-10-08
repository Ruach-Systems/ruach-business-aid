param([string]$FontRoot)
Add-Type -AssemblyName System.Drawing
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$fonts = [System.Drawing.Text.PrivateFontCollection]::new()
$fonts.AddFontFile((Join-Path $FontRoot 'Manrope-800.ttf'))
$fonts.AddFontFile((Join-Path $FontRoot 'Inter-400.ttf'))

function Number([float]$Value) { $Value.ToString('0.###', $culture) }

function TextPath([string]$Text, [string]$Family, [float]$Size, [float]$X, [float]$Y, [string]$Color) {
    $familyObject = $fonts.Families | Where-Object Name -Like "$Family*" | Select-Object -First 1
    if (!$familyObject) { throw "Missing bundled font family: $Family" }
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $style = if ($Family -eq 'Manrope') { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
        $path.AddString($Text, $familyObject, [int]$style, $Size, [System.Drawing.PointF]::new($X, $Y), [System.Drawing.StringFormat]::GenericTypographic)
        $points = $path.PathPoints
        $types = $path.PathTypes
        $d = [System.Text.StringBuilder]::new()
        for ($i = 0; $i -lt $points.Length; $i++) {
            switch ($types[$i] -band 7) {
                0 { [void]$d.Append("M$(Number $points[$i].X) $(Number $points[$i].Y)") }
                1 { [void]$d.Append("L$(Number $points[$i].X) $(Number $points[$i].Y)") }
                3 {
                    [void]$d.Append("C$(Number $points[$i].X) $(Number $points[$i].Y) $(Number $points[$i+1].X) $(Number $points[$i+1].Y) $(Number $points[$i+2].X) $(Number $points[$i+2].Y)")
                    $i += 2
                }
                default { throw 'Unsupported font outline segment.' }
            }
            if ($types[$i] -band 128) { [void]$d.Append('Z') }
        }
        "<path fill=`"$Color`" d=`"$d`"/>"
    } finally { $path.Dispose() }
}

function SvgMark([string]$File, [float]$X, [float]$Y, [float]$Size, [string]$Color) {
    [xml]$source = Get-Content $File -Raw
    $paths = ($source.svg.path | ForEach-Object { $_.OuterXml }) -join ''
    $paths = $paths.Replace('#B21F32', $Color)
    "<g transform=`"translate($X $Y) scale($(Number ($Size / 96)))`">$paths</g>"
}
