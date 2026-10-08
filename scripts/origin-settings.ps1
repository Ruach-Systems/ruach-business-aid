function Get-OriginSettings([string]$Json) {
    $pairs = $Json | ConvertFrom-Json -NoEnumerate -ErrorAction Stop
    if ($pairs -isnot [array] -or $pairs.Count -eq 0) { throw 'A nonempty array of explicit API/PWA origin pairs is required.' }
    $values = [ordered]@{}
    $apiOrigins = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt $pairs.Count; $index++) {
        foreach ($key in @('ApiOrigin', 'PwaOrigin')) {
            $value = $pairs[$index].$key
            $uri = $null
            if ($value -isnot [string] -or ![Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -or
                $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
                $uri.AbsolutePath -ne '/' -or $uri.Host.Contains('*') -or $value -cne $uri.GetLeftPart([UriPartial]::Authority)) {
                throw 'Origin pairs must contain canonical HTTPS origins without paths or trailing slashes.'
            }
            $values["App__OriginPairs__${index}__$key"] = $value
        }
        if (!$apiOrigins.Add($pairs[$index].ApiOrigin)) { throw 'Duplicate API origin mapping.' }
    }
    return $values
}
