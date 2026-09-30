$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$targets = @(@(460512,108,"Texture2D obj7"), @(524600,112,"AudioClip obj13"))
foreach($t in $targets){
  $abs=$t[0]; $len=$t[1]
  Write-Host ("--- " + $t[2] + " abs=" + $abs + " len=" + $len + " ---")
  $j=$abs
  while($j -lt ($abs+$len)){
    $line = ($j.ToString("X4") + ": ")
    $k=0
    while($k -lt 16 -and ($j+$k) -lt ($abs+$len)){ $line += $b[$j+$k].ToString("X2")+" "; $k++ }
    $line += "  |"
    # ASCII
    $k=0
    while($k -lt 16 -and ($j+$k) -lt ($abs+$len)){
      $c=$b[$j+$k]
      if($c -ge 32 -and $c -le 126){ $line += [char]$c } else { $line += "." }
      $k++
    }
    Write-Host $line
    $j += 16
  }
  Write-Host ""
}
