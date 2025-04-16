& dotnet publish ./ImportTemperature `
  --configuration Release `
  --self-contained true `
  -p:PublishSingleFile=true `
  -r win-x86
