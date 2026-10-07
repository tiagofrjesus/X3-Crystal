<#
.SYNOPSIS
  Renderiza as paginas de um PDF para PNG com a API nativa Windows.Data.Pdf (sem pdftoppm/gs).
  Correr em PowerShell 64-bit normal (nao precisa do Crystal).
#>
param([Parameter(Mandatory)][string]$Pdf, [string]$OutPrefix, [int]$MaxPages = 3, [double]$Scale = 1.5)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Data.Pdf.PdfDocument,Windows.Data.Pdf,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.Streams.InMemoryRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime] | Out-Null
$ext = [System.WindowsRuntimeSystemExtensions]
$asTaskOp = ($ext.GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskAc = ($ext.GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' })[0]
function Await($op, [type]$t) { $task = $asTaskOp.MakeGenericMethod($t).Invoke($null, @($op)); $task.Wait(); $task.Result }
function AwaitA($op) { $task = $asTaskAc.Invoke($null, @($op)); $task.Wait() }

if (-not $OutPrefix) { $OutPrefix = [IO.Path]::ChangeExtension($Pdf, $null).TrimEnd('.') }
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $Pdf).Path)) ([Windows.Storage.StorageFile])
$doc = Await ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
Write-Host "Paginas: $($doc.PageCount)"
for ($i = 0; $i -lt [Math]::Min($doc.PageCount, $MaxPages); $i++) {
  $page = $doc.GetPage($i)
  $opt = New-Object Windows.Data.Pdf.PdfPageRenderOptions
  $opt.DestinationWidth = [uint32]($page.Size.Width * $Scale)
  $opt.DestinationHeight = [uint32]($page.Size.Height * $Scale)
  $ms = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
  AwaitA ($page.RenderToStreamAsync($ms, $opt))
  $net = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($ms.GetInputStreamAt(0))
  $out = "{0}_p{1}.png" -f $OutPrefix, ($i + 1)
  $fs = [IO.File]::Create($out); $net.CopyTo($fs); $fs.Close()
  Write-Host "-> $out"
}
