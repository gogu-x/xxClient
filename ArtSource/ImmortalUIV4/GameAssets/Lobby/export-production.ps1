$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$plan=Get-Content "$PSScriptRoot/production-plan.json" -Raw|ConvertFrom-Json
$records=@()
foreach($job in $plan){
 $id=$job[0];$meta=Join-Path $PSScriptRoot "Production/$id.json"
 if(-not(Test-Path $meta)){throw "Missing asset $id"}
 $row=Get-Content $meta -Raw|ConvertFrom-Json
 $dst=Join-Path $PSScriptRoot "Production/$id.png"
 Copy-Item -LiteralPath $row.source -Destination $dst -Force
 $b=[Drawing.Bitmap]::new($dst)
 try{
  $zeros=0;$partial=0;$samples=0;$minX=$b.Width;$minY=$b.Height;$maxX=0;$maxY=0
  for($y=0;$y -lt $b.Height;$y+=3){for($x=0;$x -lt $b.Width;$x+=3){$a=$b.GetPixel($x,$y).A;$samples++;if($a -eq 0){$zeros++};if($a -gt 0 -and $a -lt 255){$partial++};if($a -gt 8){$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}}}
  $records += [pscustomobject]@{id=$id;title=$row.title;file="Production/$id.png";source=$row.source;sha256=(Get-FileHash $dst).Hash.ToLower();width=$b.Width;height=$b.Height;transparentFraction=[Math]::Round($zeros/$samples,4);alphaPass=($zeros/$samples -gt .03);visibleBounds=@($minX,$minY,([Math]::Min($b.Width,$maxX+3)-$minX),([Math]::Min($b.Height,$maxY+3)-$minY));status='individual-asset-review'}
 }finally{$b.Dispose()}
}
$records|ConvertTo-Json -Depth 8|Set-Content "$PSScriptRoot/production-manifest.json" -Encoding utf8
$records|Select-Object id,width,height,transparentFraction,alphaPass|Format-Table
if($records.Where({-not $_.alphaPass}).Count){throw 'Alpha verification failed'}
