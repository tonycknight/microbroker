
$json = curl "http://localhost:8080/queues/" -s
$queues = $json | ConvertFrom-Json

Write-Host "Found $($queues.Count) queues." -ForegroundColor Cyan

foreach ($queue in $queues) {

    Write-Host "Clearing queue $($queue.name)..."

    Invoke-RestMethod -Uri "http://localhost:8080/queues/$($queue.name)/" -Method DELETE 

}

Write-Host "Done." -ForegroundColor Green