$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=4096; $fileSize=$b.Length
function L($o){[int]([uint32]($b[$o] + $b[$o+1]*256 + $b[$o+2]*65536 + $b[$o+3]*16777216))}
$p=276; $oc=L $p; $pw=8; $es=20; $base=$p+4
$cids=@(150,21,28,48,83,1,4,114)
Write-Host "objectCount=$oc"
for($i=0;$i -lt $oc;$i++){
  $eo=$base+$i*$es
  $stored=L ($eo+$pw); $len=L ($eo+$pw+4); $tid=L ($eo+$pw+8)
  $abs=$dataOffset+$stored
  $cid = if($tid -lt $cids.Count){$cids[$tid]}else{99}
  Write-Host ("obj{0,-3} abs={1,-8} len={2,-8} tid={3} classID={4}" -f $i,$abs,$len,$tid,$cid)
}
# dump first 80 bytes of the first Texture2D (typeID=2) object
for($i=0;$i -lt $oc;$i++){
  $eo=$base+$i*$es
  $stored=L ($eo+$pw); $len=L ($eo+$pw+4); $tid=L ($eo+$pw+8)
  if($tid -eq 2){
    $abs=$dataOffset+$stored
    Write-Host ("--- Texture2D obj{0} abs={1} len={2} ---" -f $i,$abs,$len)
    $j=$abs
    while($j -lt [Math]::Min($abs+96,$fileSize)){
      $line = ($j.ToString("X4") + ": ")
      $k=0
      while($k -lt 16 -and ($j+$k) -lt $fileSize){ $line += $b[$j+$k].ToString("X2")+" "; $k++ }
      if(($j % 4) -eq 0){
        $v=[uint32]($b[$j] + $b[$j+1]*256 + $b[$j+2]*65536 + $b[$j+3]*16777216)
        $line += ("  LE=" + $v)
      }
      Write-Host $line
      $j += 16
    }
    break
  }
}
