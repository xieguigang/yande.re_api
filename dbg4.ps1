$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$ranges = @(@(37,60), @(218,234))
foreach($rg in $ranges){
  $s=$rg[0]; $e=$rg[1]
  $i=$s
  while($i -lt $e){
    $line = ($i.ToString("X4") + ": ")
    $j=0
    while($j -lt 16 -and ($i+$j) -lt $e){
      $line += $b[$i+$j].ToString("X2") + " "
      $j++
    }
    # also show little-endian int32 at start of this row if 4-aligned
    if(($i % 4) -eq 0){
      $v = [uint32]($b[$i] + $b[$i+1]*256 + $b[$i+2]*65536 + $b[$i+3]*16777216)
      $line += ("   LE=" + $v)
    }
    Write-Host $line
    $i += 16
  }
  Write-Host ""
}
