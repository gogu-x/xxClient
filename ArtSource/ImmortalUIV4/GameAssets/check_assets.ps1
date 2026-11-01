$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$results=@()
foreach($meta in Get-ChildItem -LiteralPath "$PSScriptRoot/Equipment" -Filter *.json){
 $r=Get-Content -LiteralPath $meta.FullName -Raw|ConvertFrom-Json
 if(-not $r.source){continue}
 $dest=Join-Path $PSScriptRoot "Equipment/$($r.id).png"
 Copy-Item -LiteralPath $r.source -Destination $dest -Force
 $b=[Drawing.Bitmap]::new($dest)
 try{
  $zero=0;$partial=0;$total=0;$minX=$b.Width;$minY=$b.Height;$maxX=0;$maxY=0
  for($y=0;$y -lt $b.Height;$y+=4){for($x=0;$x -lt $b.Width;$x+=4){$a=$b.GetPixel($x,$y).A;$total++;if($a -eq 0){$zero++};if($a -gt 0 -and $a -lt 255){$partial++};if($a -gt 8){$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}}}
  $results += [ordered]@{id=$r.id;width=$b.Width;height=$b.Height;expectedAlpha=$r.alpha;transparentFraction=[Math]::Round($zero/$total,4);partialFraction=[Math]::Round($partial/$total,4);alphaPass=(-not $r.alpha -or $zero/$total -gt .05);visibleBoundsSampled=@($minX,$minY,($maxX-$minX+4),($maxY-$minY+4));file="Equipment/$($r.id).png"}
 }finally{$b.Dispose()}
}
$results|ConvertTo-Json -Depth 6|Set-Content -LiteralPath "$PSScriptRoot/validation.json" -Encoding utf8
$results|Format-Table id,width,height,transparentFraction,partialFraction,alphaPass

