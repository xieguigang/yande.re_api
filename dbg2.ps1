$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=4096; $fileSize=$b.Length
function L($o){[int]($b[$o] + $b[$o+1]*256 + $b[$o+2]*65536 + $b[$o+3]*16777216)}
# find type list
$tl=0; $tlCount=0; $tlEnd=0; $cids=@()
for($T=20;$T -lt 4096;$T++){
  $C=L $T; if($C -lt 1 -or $C -gt 200){continue}
  $found=$false
  foreach($es in @(23,24,22,25,21)){
    if($T+4+$C*$es -gt $b.Length){continue}
    $ok=$true; $list=@()
    for($i=0;$i -lt $C;$i++){ $eo=$T+4+$i*$es; $cid=L $eo; if($cid -lt 1 -or $cid -gt 1000){$ok=$false;break}; $list+=$cid }
    if($ok){$tl=$T;$tlCount=$C;$tlEnd=$T+4+$C*$es;$cids=$list;$found=$true;break}
  }
  if($found){break}
}
Write-Host ("TypeList T=$tl count=$tlCount end=$tlEnd classIDs=(" + ($cids -join ',') + ")")
# validate candidate p=276
$p=276; $oc=L $p; $pw=8; $entrySize=20; $baseOff=$p+4
Write-Host "p=276 oc=$oc"
for($i=0;$i -lt $oc;$i++){
  $eo=$baseOff+$i*$entrySize
  $stored=L ($eo+$pw); $len=L ($eo+$pw+4); $tid=L ($eo+$pw+8)
  $abs=[long]$dataOffset + [long]$stored
  $problems=""
  if($stored -lt 0){$problems+="stored<0 "}
  if($abs -lt $dataOffset){$problems+="abs<do "}
  if($len -le 0 -or $len -gt 200000000){$problems+="len? "}
  if($abs+$len -gt $fileSize){$problems+="overEOF "}
  if($tid -lt 0 -or $tid -ge $tlCount){$problems+="tid?($tid) "}
  if($problems -ne ""){Write-Host ("  entry $i stored=$stored abs=$abs len=$len tid=$tid -> FAIL $problems")}
}
Write-Host "done"
