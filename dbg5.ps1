$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$start=4216; $len=100
$i=$start
while($i -lt ($start+$len)){
  $line = ($i.ToString("X4") + ": ")
  $j=0
  while($j -lt 16 -and ($i+$j) -lt ($start+$len)){
    $line += $b[$i+$j].ToString("X2") + " "
    $j++
  }
  if(($i % 4) -eq 0 -and ($i+3) -lt ($start+$len)){
    $v = [uint32]($b[$i] + $b[$i+1]*256 + $b[$i+2]*65536 + $b[$i+3]*16777216)
    $line += ("   LE=" + $v)
  }
  Write-Host $line
  $i += 16
}
