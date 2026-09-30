$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
function DumpObj($abs, $len, $title){
  Write-Host ("--- " + $title + " abs=" + $abs + " len=" + $len + " ---")
  $j=$abs
  while($j -lt ($abs+[Math]::Min($len,96))){
    $line = ($j.ToString("X4") + ": ")
    $k=0
    while($k -lt 16 -and ($j+$k) -lt ($abs+$len)){ $line += $b[$j+$k].ToString("X2")+" "; $k++ }
    $line += " |"
    $k=0
    while($k -lt 16 -and ($j+$k) -lt ($abs+[Math]::Min($len,96))){
      $c=$b[$j+$k]
      if($c -ge 32 -and $c -le 126){ $line += [char]$c } else { $line += "." }
      $k++
    }
    Write-Host $line
    $j += 16
  }
  Write-Host ""
}
DumpObj 266720 47648 "Texture2D obj3 (#4 FAIL)"
DumpObj 365536 51164 "Texture2D obj5 (#6 FAIL)"
