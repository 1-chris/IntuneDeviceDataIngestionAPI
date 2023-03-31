# Intune Device Data Ingestion (Minimal API)
This minimal API is used to receive signals from devices directly. 

This has been set up with Azure Log Analytics in mind.

Freely available client available here: [IntuneDeviceDataIngestionClient](https://github.com/1-chris/IntuneDeviceDataIngestionClient)

------------------
## To build and deploy

Using Azure CLI + Compress-Archive 

https://docs.microsoft.com/en-us/azure/app-service/app-service-deploy-zip

### Linux App Service Plan
```ps1
# Before continuing, create an appropriate app service and download the publish profile from the Azure portal and save it to Properties\PublishProfiles\your-profile.PublishSettings

# Built for 64bit Linux App Service Plan
dotnet build -r linux-x64 -c Release -o ./bin/Release/netcoreapp2.0/publish -p:PublishProfile=your-profile -p:DeployOnBuild=true

# Compress the publish folder
Compress-Archive -Path ./bin/Release/netcoreapp2.0/publish/* -DestinationPath ./bin/Release/netcoreapp2.0/publish.zip -Force

# Deploy the zip file to the app service
az webapp deployment source config-zip --resource-group your-resource-group --name your-app-service --src ./bin/Release/netcoreapp2.0/publish.zip
```

### Windows App Service Plan (free tier)
```ps1
# Build for 32bit Windows App Service Plan
dotnet build -r win-x86 --self-contained true -c Release -o ./bin/Release/netcoreapp2.0/publish -p:PublishProfile=your-profile -p:DeployOnBuild=true

# Compress the publish folder
Compress-Archive -Path ./bin/Release/netcoreapp2.0/publish/* -DestinationPath ./bin/Release/netcoreapp2.0/publish.zip -Force

# Deploy the zip file to the app service
az webapp deployment source config-zip --resource-group your-resource-group --name your-app-service --src ./bin/Release/netcoreapp2.0/publish.zip
```
