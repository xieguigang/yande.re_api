$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=0x1000
function B($o,$n){$v=0;for($i=0;$i -lt $n;$i++){$v=$v -bor ($b[$o+$i] -shl (8*$i))};$v}
$s=4096; $e=4200
Write-Host "=== DATA @4096 ==="
$line=""
for($i=$s;$i -lt $e;$i++){
  $line += $b[$i].ToString('X2')+' '
  if(($i-$s+1)%16 -eq 0){ Write-Host (($i-15).ToString()+": "+$line); $line="" }
}
if($line -ne ''){ Write-Host (($e-1).ToString()+": "+$line) }
Write-Host "=== candidate p=308 entry=20 pw=4 rel=true ==="
$p=308
$oc=B $p 4
Write-Host "oc=$oc"
for($i=0;$i -lt 12 -and $i -lt $oc;$i++){
  $eo=$p+4+$i*20
  $os=B ($eo+4) 4
  $len=B ($eo+8) 4
  $tid=B ($eo+12) 4
  $abs=$os+$dataOffset
  $slice = ""
  for($j=0;$j -lt [Math]::Min(40,$len);$j++){
    $c=$b[$abs+$j]
    if($c -ge 32 -and $c -le 126){ $slice += [char]$c } else { $slice += '.' }
  }
  Write-Host ("  i=$i abs=$abs len=$len tid=$tid ["+$slice+"]")
}
