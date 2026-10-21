param([string]$lang = 'EN', [string]$table = 'Data_Car')
$rows = Import-Csv -Delimiter "`t" -Path "$PSScriptRoot\layers.tsv" -Encoding UTF8 | Where-Object { $_.lang -eq $lang -and $_.table -eq $table }
$layers = @()
foreach ($r in $rows) {
  $set = New-Object 'System.Collections.Generic.HashSet[string]'
  foreach ($k in $r.keys.Split(',')) { [void]$set.Add($k) }
  $name = if ($r.kind -eq 'base') { 'base' } else { ($r.layer -split ' \| ')[1] }
  $layers += [pscustomobject]@{ Name = $name; Pri = [int]$r.priority; Set = $set }
}
$union = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($l in $layers) { foreach ($k in $l.Set) { [void]$union.Add($k) } }
$win = $layers | Sort-Object Pri -Descending | Select-Object -First 1
"{0}/{1}: {2} layers, union {3} keys, winner = {4} (pri {5}, {6} keys)" -f $lang, $table, $layers.Count, $union.Count, $win.Name, $win.Pri, $win.Set.Count
$lost = 0
foreach ($k in $union) { if (-not $win.Set.Contains($k)) { $lost++ } }
"keys missing in the winner: $lost"
foreach ($l in ($layers | Sort-Object Pri)) {
  $own = 0; $miss = 0
  $baseSet = ($layers | Where-Object { $_.Name -eq 'base' }).Set
  foreach ($k in $l.Set) { if (-not $baseSet.Contains($k)) { $own++ } }
  foreach ($k in $baseSet) { if (-not $l.Set.Contains($k)) { $miss++ } }
  "{0,-42} pri {1,-6} keys {2,-5} not in base {3,-4} base keys missing {4}" -f $l.Name, $l.Pri, $l.Set.Count, $own, $miss
}
