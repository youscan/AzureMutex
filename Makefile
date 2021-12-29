pack:
	dotnet pack --configuration Release --include-symbols --include-source -p:SymbolPackageFormat=snupkg -p:PackageVersion=1.0.0 -o out/ src

start-storage:
	docker run -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
