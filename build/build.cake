Task("Default")
.Does(() =>
{
	var srcDir = Directory("../ImportTemperature");
	var publishDir = Directory("./temp/publish");
	var packedFile = File("./temp/ImportTemperature.zip");

	CleanDirectory(publishDir);

	DotNetPublish(srcDir + File("ImportTemperature.csproj"), new()
	{
		OutputDirectory = publishDir,
		SelfContained = true,
		Runtime = "win-x86",
		Configuration = "Release",
		PublishSingleFile = true
	});

	Zip(publishDir, packedFile);
});


RunTarget("Default");
