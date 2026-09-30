$f = "Z:\klsdzj_4.15.1_20190428_025416_686ef\assets\Android\accessory_prefabs_acc@archer_6_11_wing"
$b = [System.IO.File]::ReadAllBytes($f)
$len = $b.Length
Write-Host ("LEN=" + $len + "  compBI=64  infoPos=" + ($len-64))
function DumpRange($s, $e, $label){
  Write-Host ("--- " + $label + " ---")
  $i=$s
  while($i -lt $e){
    $line = ($i.ToString("X4") + ": ")
    $k=0
    while($k -lt 16 -and ($i+$k) -lt $e){ $line += $b[$i+$k].ToString("X2")+" "; $k++ }
    Write-Host $line
    $i += 16
  }
}
DumpRange ($len-64) $len "BlocksInfo (末尾 64 字节)"
DumpRange 46 78 "头部之后的数据区起始"
