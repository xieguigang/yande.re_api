$path = "Z:\klsdzj_4.11.1_1_20181220_141000_676167\assets\bin\Data\sharedassets0.assets"
$b = [System.IO.File]::ReadAllBytes($path)
$s=215; $e=252
$i=$s
while ($i -lt $e) {
    $line = ($i.ToString("X4") + ": ")
    $j=0
    while ($j -lt 16 -and ($i+$j) -lt $e) {
        $line += $b[$i+$j].ToString("X2") + " "
        $j++
    }
    Write-Host $line
    $i += 16
}
# Also report int32 (LE) at key offsets
function L($o){[int]($b[$o] + $b[$o+1]*256 + $b[$o+2]*65536 + $b[$o+3]*16777216)}
Write-Host ("LE@37=" + (L 37) + " LE@38=" + (L 38) + " LE@61=" + (L 61) + " LE@221=" + (L 221) + " LE@222=" + (L 222) + " LE@245=" + (L 245) + " LE@276=" + (L 276))
