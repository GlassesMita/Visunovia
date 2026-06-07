$bytes = [System.IO.File]::ReadAllBytes('D:\Visunovia\wwwroot\css\site.css')
Write-Host "File size: $($bytes.Length) bytes"
Write-Host "First 10 bytes (hex):"
$bytes[0..9] | ForEach-Object { '{0:X2}' -f $_ }
Write-Host ""
Write-Host "BOM check:"
if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "UTF-8 BOM detected!"
} elseif ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
    Write-Host "UTF-16 LE BOM detected!"
} elseif ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
    Write-Host "UTF-16 BE BOM detected!"
} else {
    Write-Host "No BOM detected"
}
