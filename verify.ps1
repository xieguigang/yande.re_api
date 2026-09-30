$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=0x1000
function B($o,$n){$v=0;for($i=0;$i -lt $n;$i++){$v=$v -bor ($b[$o+$i] -shl (8*$i))};$v}
$typeList=@(150,21,28,48,83,1,4,114,201)   # indices 0..8
$ts=276; $entry=20; $pw=8
$oc=B $ts 4
Write-Host "oc=$oc"
for($i=0;$i -lt $oc;$i++){
  $eo=$ts+4+$i*$entry
  $pathID=B $eo 8
  $os=B ($eo+$pw) 4
  $len=B ($eo+$pw+4) 4
  $tid=B ($eo+$pw+8) 4
  $abs=$os+$dataOffset
  $cid="?"
  if($tid -ge 0 -and $tid -lt $typeList.Count){$cid=$typeList[$tid]}
  Write-Host ("  i=$i pathID=$pathID byteStart=$abs len=$len tid=$tid -> classID=$cid")
}
