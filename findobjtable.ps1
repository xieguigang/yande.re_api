$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$dataOffset=0x1000; $fileSize=$b.Length
function B($o,$n){$v=0;for($i=0;$i -lt $n;$i++){$v=$v -bor ($b[$o+$i] -shl (8*$i))};$v}
$typeCount=9
$best=@()
foreach($ts in 20..4070){
  $oc=B $ts 4
  if($oc -lt 5 -or $oc -gt 3000){continue}
  foreach($entry in @(20,22,24,26)){
    foreach($pw in @(4,8)){
      if($entry -lt $pw+12){continue}
      foreach($rel in @($false,$true)){
        foreach($cpos in @(0,2,4,6,8,10,12)){
          $ok=$true; $minBS=999999999
          for($i=0;$i -lt $oc;$i++){
            $eo=$ts+4+$i*$entry
            if($eo+$entry -gt $b.Length){$ok=$false;break}
            $os=B ($eo+$pw) 4; $len=B ($eo+$pw+4) 4
            $abs=$os; if($rel){$abs=$os+$dataOffset}
            if($os -lt 0){$ok=$false;break}
            if($abs+$len -gt $fileSize){$ok=$false;break}
            if($len -le 0 -or $len -gt 5000000){$ok=$false;break}
            $fv=B ($eo+$pw+$cpos) 4
            if(($fv -ge 0 -and $fv -lt $typeCount) -or ($fv -ge 1 -and $fv -le 213)){}else{$ok=$false;break}
            if($abs -lt $minBS){$minBS=$abs}
          }
          if($ok -and $minBS -ge $dataOffset -and $minBS -le $dataOffset+24){
            $best+=@{ts=$ts;entry=$entry;pw=$pw;rel=$rel;cpos=$cpos;oc=$oc;minBS=$minBS}
          }
        }
      }
    }
  }
}
"best="+$best.Count
foreach($c in ($best | Sort-Object {$_.oc} -Descending | Select-Object -First 15)){
  "ts=$($c.ts) entry=$($c.entry) pw=$($c.pw) rel=$($c.rel) cpos=$($c.cpos) oc=$($c.oc) minBS=$($c.minBS)"
}
