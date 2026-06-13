# Get the list of queues from the API
$response = Invoke-WebRequest -Uri "http://localhost:8080/queues/" -Method Get
$queues = $response.Content | ConvertFrom-Json

# Iterate through queues and delete those matching the test-queue- pattern
foreach ($queue in $queues) {
    if ($queue.name -like "test-queue-*") {
        Write-Host "Deleting queue: $($queue.name)"
        Invoke-WebRequest -Uri "http://localhost:8080/queues/$($queue.name)/" -Method Delete
    }
}

Write-Host "Done." -ForegroundColor Green
