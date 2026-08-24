pkg_name := "Svelto.Common"

nuget_pack:
	make nuget_clean
	mkdir temp temp/bin temp/bin/debug temp/bin/release

	# Build for Debug
	dotnet pack /p:PackageVersion=1.0.0 -o temp/bin/debug Svelto.Common.csproj -c Debug
	unzip temp/bin/debug/Svelto.Common.1.0.0.nupkg -d temp/bin/debug
	cp temp/bin/debug/lib/netstandard2.0/Svelto.Common.dll temp/bin/debug

	# Build for Release
	dotnet pack /p:PackageVersion=1.0.0 -o temp/bin/release Svelto.Common.csproj -c Release
	unzip temp/bin/release/Svelto.Common.1.0.0.nupkg -d temp/bin/release
	cp temp/bin/release/lib/netstandard2.0/Svelto.Common.dll temp/bin/release

	# Compile into nuget
	dotnet pack /p:PackageVersion=2.0.0 -o . Svelto.Common.csproj -c NugetPack
	make nuget_clean

nuget_clean:
	rm -rf upm-preparator temp
