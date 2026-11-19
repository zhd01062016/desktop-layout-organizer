$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$a=[Reflection.Assembly]::LoadFile($env:PECO_CAPTURE_BINARY)
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$b=[Drawing.Bitmap]::new(600,160);$g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::White);$font=[Drawing.Font]::new('Arial',36);$g.DrawString('SCREENSHOT TEST',$font,[Drawing.Brushes]::Black,20,40);$g.Dispose();$font.Dispose()
$task=$a.GetType('CaptureText').GetMethod('Read',$flags).Invoke($null,@($b))
$until=[DateTime]::UtcNow.AddSeconds(20)
while(!$task.IsCompleted -and [DateTime]::UtcNow -lt $until){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 25}
if(!$task.IsCompleted){throw 'OCR timeout'}
$text=$task.GetAwaiter().GetResult();if($text -notmatch 'SCREENSHOT'){throw "OCR unexpected: $text"}
$dialog=[Windows.Forms.SaveFileDialog]::new();$dialog.FileName=(Join-Path $env:PECO_TEST_OUTPUT 'export-regression.png');$dialog.FilterIndex=1
$a.GetType('ImageExport').GetMethod('Save',$flags).Invoke($null,@($b,$dialog));$dialog.Dispose()
$pin=$a.GetType('PasteWindow').GetConstructors()[0].Invoke(@($b));$pin.Show();[Windows.Forms.Application]::DoEvents();if($pin.ClientSize.Width -le 0){throw 'Pin size'};$pin.Close();$pin.Dispose();$b.Dispose()
"PASS: OCR ($text), PNG export, pin window"
