$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=4096; $fileSize=$b.Length
function L($o){[int]($b[$o] + $b[$o+1]*256 + $b[$o+2]*65536 + $b[$o+3]*16777216)}
for($p=268;$p -le 288;$p++){
  $oc=L $p
  if($oc -lt 1 -or $oc -gt 2000){ continue }
  $baseOff=$p+4; $pw=8; $entrySize=20
  $off0=L ($baseOff+$pw); $off0abs=$off0+$dataOffset
  $len0=L ($baseOff+$pw+4)
  $tid0=L ($baseOff+$pw+8)
  $ok=$true; $reason=""
  if($off0abs -lt $dataOffset){$ok=$false;$reason+="off0<do "}
  if($off0abs+$len0 -gt $fileSize){$ok=$false;$reason+="off0+len>fs "}
  if($len0 -le 0 -or $len0 -gt 100000000){$ok=$false;$reason+="len? "}
  if($tid0 -lt 0 -or $tid0 -ge 9){$ok=$false;$reason+="tid?($tid0) "}
  if($oc -ge 2){
    $lbase=$baseOff+($oc-1)*$entrySize
    if($lbase+$entrySize -gt $b.Length){$ok=$false;$reason+="lastOOB "}
    else{
      $offL=L ($lbase+$pw); $offLabs=$offL+$dataOffset; $lenL=L ($lbase+$pw+4)
      if($offLabs -lt $dataOffset -or $offLabs+$lenL -gt $fileSize){$ok=$false;$reason+="lastBad "}
    }
  }
  Write-Host ("p=$p oc=$oc off0rel=$off0 off0abs=$off0abs len0=$len0 tid0=$tid0 -> " + $(if($ok){"PASS"}else{"FAIL "+$reason}))
}
