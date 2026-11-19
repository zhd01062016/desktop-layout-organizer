$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$a=[Reflection.Assembly]::LoadFile($env:PECO_CAPTURE_BINARY)
$t=$a.GetType('CaptureSelection');$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$b=New-Object Drawing.Bitmap 1000,700
$g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::FromArgb(239,243,249));$g.Dispose()
$f=$t.GetConstructors()[0].Invoke([object[]]@($b.PSObject.BaseObject,[Drawing.Rectangle]::new(0,0,1000,700)))
function Field($name){$t.GetField($name,$flags).GetValue($f)}
function SetField($name,$value){$t.GetField($name,$flags).SetValue($f,$value)}
function Call($name,$arguments=@()){$t.GetMethod($name,$flags).Invoke($f,[object[]]$arguments)}
function Mouse($name,$x,$y){Call $name @([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,$x,$y,0))}
function Tool($name){Call SelectTool @([Enum]::Parse($t.GetNestedType('Tool',[Reflection.BindingFlags]::NonPublic),$name))}
function Assert($ok,$message){if(!$ok){throw $message}}
SetField region ([Drawing.Rectangle]::new(50,50,850,500))
Tool Rectangle
Mouse OnMouseDown 100 100;Mouse OnMouseMove 250 200;Mouse OnMouseUp 250 200
Assert ((Field marks).Count -eq 1) 'Draw rectangle'
Mouse OnMouseDown 170 150;Mouse OnMouseMove 190 170;Mouse OnMouseUp 190 170
$m=Field editable;Assert ($m.GetType().GetField('Start',$flags).GetValue($m).X -eq 120) 'Move mark'
Call Undo
$m=Field editable;Assert ($m.GetType().GetField('Start',$flags).GetValue($m).X -eq 100) 'Undo edit'
Call Redo
Mouse OnMouseDown 270 220;Mouse OnMouseMove 320 250;Mouse OnMouseUp 320 250
$m=Field editable;Assert ($m.GetType().GetField('End',$flags).GetValue($m).X -eq 320) 'Resize mark'
Call Undo;Call Undo;Call Undo
Assert ((Field marks).Count -eq 0) 'Undo all'
Call Redo;Call Redo;Call Redo
Tool Text
Mouse OnMouseDown 400 150
(Field input).Text="简洁标注体验`r`n第二行文字"
Call CommitText
Assert ((Field marks).Count -eq 2) 'Multiline text commit'
Mouse OnMouseDoubleClick 410 160
Assert ($null -ne (Field input)) 'Reopen text'
(Field input).Text='cancelled change'
Call CancelText
$m=Field editable;Assert ($m.GetType().GetField('Text',$flags).GetValue($m).StartsWith('简洁')) 'Cancel preserves text'
$result=Call RenderSelection
Assert ($result.Width -eq 850 -and $result.Height -eq 500) 'Export dimensions'
$result.Save((Join-Path $env:PECO_TEST_OUTPUT '截图导出验证.png'));$result.Dispose()
$f.Show();Call PositionToolbar;[Windows.Forms.Application]::DoEvents()
$preview=New-Object Drawing.Bitmap 1000,700
$f.DrawToBitmap($preview,[Drawing.Rectangle]::new(0,0,1000,700));$preview.Save((Join-Path $env:PECO_TEST_OUTPUT '截图界面预览.png'));$preview.Dispose()
Mouse OnMouseDoubleClick 410 160
[Windows.Forms.Application]::DoEvents()
$preview=New-Object Drawing.Bitmap 1000,700
$f.DrawToBitmap($preview,[Drawing.Rectangle]::new(0,0,1000,700));$preview.Save((Join-Path $env:PECO_TEST_OUTPUT '文字编辑预览.png'));$preview.Dispose()
Call CancelText;$f.Close();$f.Dispose();$b.Dispose()
'PASS: rectangle, move, resize, edit undo/redo, multiline text, re-edit cancel, export, toolbar rendering'
