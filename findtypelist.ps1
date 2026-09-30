$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
function B($o,$n){$v=0;for($i=0;$i -lt $n;$i++){$v=$v -bor ($b[$o+$i] -shl (8*$i))};$v}
$best = @()
foreach($p in 33..400){
  foreach($S in @(21,22,23,24,25,5,6)){
    foreach($wid in @(4,2)){
      $T = B $p 4
      if($T -lt 1 -or $T -gt 80){continue}
      $ok=0; $bad=0; $cids=@()
      foreach($i in 0..($T-1)){
        $eo = $p+4+$i*$S
        if($eo+$wid -gt $b.Length){$bad++;break}
        $cid = B $eo $wid
        if($cid -ge 1 -and $cid -le 213){$ok++; $cids+=$cid} else {$bad++}
      }
      if($ok -ge 3 -and $ok -ge ($T-2)){
        $best += @{p=$p;S=$S;wid=$wid;T=$T;cids=$cids}
      }
    }
  }
}
"best matches:"
foreach($m in ($best | Sort-Object {$_.cids.Count} -Descending | Select-Object -First 12)){
  "p=$($m.p) S=$($m.S) wid=$($m.wid) T=$($m.T) cids=($($m.cids -join ','))"
}
