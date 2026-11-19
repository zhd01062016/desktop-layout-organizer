$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$a=[Reflection.Assembly]::LoadFile($env:PECO_CAPTURE_BINARY)
$t=$a.GetType('CaptureHistoryPicker');$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$ctor=$t.GetConstructors($flags)[0]
$paths=[string[]]@(1..10|ForEach-Object{(Join-Path $env:PECO_TEST_OUTPUT '截图导出验证.png')})
$f=$ctor.Invoke([object[]]@($paths,1));$f.Show();[Windows.Forms.Application]::DoEvents()
$preview=[Drawing.Bitmap]::new($f.Width,$f.Height);$f.DrawToBitmap($preview,[Drawing.Rectangle]::new(0,0,$f.Width,$f.Height));$preview.Save((Join-Path $env:PECO_TEST_OUTPUT '历史面板预览.png'));$preview.Dispose()
$cards=$t.GetField('cards',$flags).GetValue($f)
if($cards.Count -ne 10){throw 'History count'}
foreach($width in @(360,600,800)){$f.ClientSize=[Drawing.Size]::new($width,500);$t.GetMethod('LayoutCards',$flags).Invoke($f,@());[Windows.Forms.Application]::DoEvents();foreach($card in $cards){if($card.Width -gt $width){throw 'Responsive layout'}}}
$backup=[Windows.Forms.Clipboard]::GetDataObject()
try{$t.GetMethod('Copy',$flags).Invoke($f,@(0));$image=[Windows.Forms.Clipboard]::GetImage();if(!$image -or $image.Width -ne 850 -or $image.Height -ne 500){throw 'History original resolution copy'};$image.Dispose()}finally{if($backup){[Windows.Forms.Clipboard]::SetDataObject($backup,$true)}}
$f.Dispose()
foreach($entries in @(@(),@((Join-Path $env:PECO_TEST_OUTPUT 'missing-image.png')))){$f=$ctor.Invoke([object[]]@([string[]]$entries,1));$f.Show();[Windows.Forms.Application]::DoEvents();$f.Close();$f.Dispose()}
'PASS: 10 history cards, responsive layouts, original-resolution clipboard, empty and missing history'
