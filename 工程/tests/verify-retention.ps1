$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFile($env:PECO_CAPTURE_BINARY)
$t=$a.GetType('CaptureHistory');$method=$t.GetMethod('CleanupDirectory',[Reflection.BindingFlags]'Static,NonPublic')
$folder=Join-Path $env:PECO_TEST_OUTPUT ('retention-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
$now=[DateTime]::UtcNow
function Make($age,$suffix='.png'){$name='capture-'+$now.AddHours(-$age).ToString('yyyyMMddHHmmssfffffff')+'-'+[Guid]::NewGuid().ToString('N')+$suffix;$p=Join-Path $folder $name;[IO.File]::WriteAllText($p,'test');return $p}
$old=Make 25;$boundary=Make 24;$recent=Make 23;$future=Make -1;$temp=Make 25 '.png.tmp'
$manual=Join-Path $folder 'saved-picture.png';[IO.File]::WriteAllText($manual,'keep');[IO.File]::SetLastWriteTimeUtc($manual,$now.AddDays(-2))
$nested=Join-Path $folder 'nested';New-Item -ItemType Directory $nested | Out-Null;$nestedFile=Join-Path $nested ([IO.Path]::GetFileName($old));[IO.File]::WriteAllText($nestedFile,'keep')
$method.Invoke($null,[object[]]@([string]$folder,[DateTime]$now))
foreach($p in @($old,$boundary,$temp)){if(Test-Path -LiteralPath $p){throw 'Expired cache not removed'}}
foreach($p in @($recent,$future,$manual,$nestedFile)){if(!(Test-Path -LiteralPath $p)){throw 'Non-expired or unrelated file removed'}}
$resolved=(Resolve-Path -LiteralPath $folder).Path
if(!$resolved.StartsWith((Join-Path $env:PECO_TEST_OUTPUT "retention-test-"))){throw 'Unexpected cleanup target'}
Remove-Item -LiteralPath $resolved -Recurse -Force
'PASS: 24-hour boundary, old temp files, recent/future entries retained, manual files and subdirectories untouched'
