$all = Import-Csv -Delimiter "`t" -Path "$PSScriptRoot\layers.tsv" -Encoding UTF8
$langs = $all.lang | Sort-Object -Unique
$tables = $all | Where-Object { $_.kind -eq 'pack' } | ForEach-Object { $_.table } | Sort-Object -Unique
"lang table: layers(pack) | winner | keys the winner lacks vs union of base+packs | which packs hold them"
$out = @()
foreach ($lang in $langs) {
  foreach ($table in $tables) {
    $rows = @($all | Where-Object { $_.lang -eq $lang -and $_.table -eq $table })
    $packs = @($rows | Where-Object { $_.kind -ne 'base' })
    if ($packs.Count -eq 0) { continue }
    $layers = @(); $union = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($r in $rows) {
      $set = New-Object 'System.Collections.Generic.HashSet[string]'
      foreach ($k in $r.keys.Split(',')) { if ($k) { [void]$set.Add($k); [void]$union.Add($k) } }
      $name = if ($r.kind -eq 'base') { 'base' } else { ($r.layer -split ' \| ')[1] }
      $layers += [pscustomobject]@{ Name = $name; Pri = [int]$r.priority; Set = $set }
    }
    $win = $layers | Sort-Object Pri -Descending | Select-Object -First 1
    $lost = New-Object 'System.Collections.Generic.List[string]'
    foreach ($k in $union) { if (-not $win.Set.Contains($k)) { $lost.Add($k) } }
    if ($lost.Count -gt 0) {
      $holders = @{}
      foreach ($k in $lost) { foreach ($l in $layers) { if ($l.Set.Contains($k)) { $holders[$l.Name] = 1 + [int]$holders[$l.Name] } } }
      $out += [pscustomobject]@{ Lang = $lang; Table = $table; Packs = $packs.Count; Winner = $win.Name; Lost = $lost.Count; Union = $union.Count; Holders = (($holders.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { $_.Key + ':' + $_.Value }) -join '; ') }
    }
  }
}
$out | Export-Csv -NoTypeInformation -Delimiter "`t" -Path "$PSScriptRoot\lost.tsv" -Encoding UTF8
"tables/lang combos with lost keys: " + $out.Count
$out | Where-Object { $_.Table -in 'Data_Car','List_CarMake' } | Sort-Object Lang | Format-Table Lang,Table,Packs,Winner,Lost,Union -AutoSize | Out-String -Width 200
