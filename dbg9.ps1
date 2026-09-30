$f = Get-ChildItem "Z:\klsdzj_4.15.1_20190428_025416_686ef\assets\Android" | Where-Object { $_.Length -gt 100000 } | Select-Object -First 1
Write-Host ("FILE: " + $f.FullName + "  LEN=" + $f.Length)
$b = [System.IO.File]::ReadAllBytes($f.FullName)
$i=0
while($i -lt 96){
  $line = ($i.ToString("X4") + ": ")
  $k=0
  while($k -lt 16){ $line += $b[$i+$k].ToString("X2")+" "; $k++ }
  $line += " |"
  $k=0
  while($k -lt 16){
    $c=$b[$i+$k]
    if($c -ge 32 -and $c -le 126){ $line += [char]$c } else { $line += "." }
    $k++
  }
  Write-Host $line
  $i += 16
}
# BE int32 at various offsets after the strings
function BE32($o){ [int](($b[$o] -shl 24) -bor ($b[$o+1] -shl 16) -bor ($b[$o+2] -shl 8) -bor $b[$o+3]) }
# find end of "UnityFS\0"
$p = 8
Write-Host ("BE32@8 (version) = " + (BE32 8))
# strings: version string then revision string; find their ends
$q = 12
while($b[$q] -ne 0){ $q++ }
Write-Host ("unityVersion ends at " + $q)
$q++
$r2 = $q
while($b[$r2] -ne 0){ $r2++ }
Write-Host ("unityRevision ends at " + $r2)
$q = $r2 + 1
Write-Host ("fields start at " + $q)
Write-Host ("BE64 size @q = " + (([uint32](($b[$q] -shl 24) -bor ($b[$q+1] -shl 16) -bor ($b[$q+2] -shl 8) -bor $b[$q+3])) * 4294967296 + [uint32](($b[$q+4] -shl 24) -bor ($b[$q+5] -shl 16) -bor ($b[$q+6] -shl 8) -bor $b[$q+7])))
Write-Host ("BE32 compBI @(q+8) = " + (BE32 ($q+8)))
Write-Host ("BE32 uncompBI @(q+12) = " + (BE32 ($q+12)))
Write-Host ("BE32 flags @(q+16) = " + (BE32 ($q+16)))
