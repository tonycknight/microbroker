param (
    [PArameter(Mandatory=$false)]
    [string]$ConnectionString = "mongodb://host.docker.internal:27017",
    
    [PArameter(Mandatory=$false)]
    [string]$DbName = "microbroker-tests",

    [PArameter(Mandatory=$false)]
    [string]$HostUrls = "http://+:8080"
)

docker run -it --rm -p 8080:8080 microbroker --mongoConnection=$ConnectionString --mongoDbName=$DbName --hostUrls=$HostUrls --ttaScanFrequency=00:00:15
