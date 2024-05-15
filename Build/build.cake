#tool "nuget:?package=GitVersion.CommandLine&version=5.12.0"
#tool "nuget:?package=gitlink&version=3.1.0"
#tool "nuget:?package=GitReleaseNotes&version=0.7.1"
#tool dotnet:?package=dotnet-reportgenerator-globaltool&version=5.1.19
#tool nuget:?package=NuGet.CommandLine&version=6.7.0

#addin "nuget:?package=Cake.Coveralls&version=1.1.0"
#addin "nuget:?package=Cake.Coverlet&version=3.0.4"
#addin "nuget:?package=Cake.Git&version=3.0.0"
#addin "nuget:?package=NuGet.Packaging&version=6.6.1"

using NuGet.Packaging;

var target = Argument("target", "Default");
var artifactsDir = "./artifacts/";
var solutionPath = "../SmppClient.sln";
var projectName = "SmppClient";
var projectFolder = "../Source/";
var gitRepoRootFolder = "../";
var solutionVersion = "1.0.0";
var projects = new List<(string path, string name, string version)>
{
	("SmppClient/", "SmppClient.csproj", solutionVersion),
};

var testFolder = "../Test/";
var testProjects = new List<(string path, string name, string dll)>
{
};
var coverageResultsFileName = "coverage.xml";
var testResultsFileName = "nunitResults.xml";
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent(gitRepoRootFolder).FriendlyName);
var isReleaseBuild = string.Equals(currentBranch, "master", StringComparison.OrdinalIgnoreCase);
var configuration = "Release";
var nugetApiKey = Argument<string>("nugetApiKey", null);
var coverallsToken = Argument<string>("coverallsToken", null);
var nugetSource = "https://api.nuget.org/v3/index.json";
var repoUrl = "https://github.com/BSVN/SMPPClient.git";
var projectUrl = "https://github.com/BSVN/SMPPClient";

Task("Clean")
	.Does(() => {
		if (DirectoryExists(artifactsDir))
		{
			DeleteDirectory(
				artifactsDir,
				new DeleteDirectorySettings {
					Recursive = true,
					Force = true
				}
			);
		}
		CreateDirectory(artifactsDir);
		DotNetClean(solutionPath);
});


Task("Restore")
	.Does(() => {
		NuGetRestore(solutionPath);
});

Task("Version")
	.Does(() => {
		foreach (var project in projects)
		{
		   var finalVersion=project.version;
		   if (project.name.Contains("EntityFramework.csproj") || project.name.Contains("Users.csproj"))
		   {
			   string pureName = project.name.Remove(project.name.IndexOf(".csproj"));
			   UpdateVersion(pureName + ".nuspec", projectFolder + project.path + "/Properties/AssemblyInfo.cs", finalVersion);
			   continue;
		   }
		   else
		   {
			   UpdateVersion(projectFolder + project.path + project.name, finalVersion);
		   }
		}
});

Task("Build")
	.IsDependentOn("Restore")
	.IsDependentOn("Clean")
	.Does(() => {
		DotNetBuild(
			solutionPath,
			new DotNetBuildSettings
			{
				Configuration = configuration
			}
		);
});

Task("Test")
	.IsDependentOn("Build")
	.Does(() => {
		foreach (var testProject in testProjects)
		{
			var specificCoverageResultsFileName = testProject.name + coverageResultsFileName;
			var specificTestResultsFileName = testProject.name + testResultsFileName;

			var settings = new DotNetTestSettings {
				VSTestReportPath = new FilePath(artifactsDir + specificTestResultsFileName)
			};

			var coverletSettings = new CoverletSettings {
				CollectCoverage = true,
				CoverletOutputFormat = CoverletOutputFormat.opencover,
				CoverletOutputDirectory = Directory(artifactsDir),
				CoverletOutputName = specificCoverageResultsFileName
			};

			DotNetTest(testFolder + testProject.path + testProject.name, settings, coverletSettings);


			if (AppVeyor.IsRunningOnAppVeyor)
				AppVeyor.UploadTestResults(artifactsDir + specificTestResultsFileName, AppVeyorTestResultsType.XUnit);
		}
});

Task("UploadCoverage")
	.IsDependentOn("Test")
	.Does(() =>
	{
		CoverallsIo(artifactsDir + coverageResultsFileName, new CoverallsIoSettings()
		{
			RepoToken = coverallsToken
		});
});

Task("Package")
	.IsDependentOn("Version")
	.Does(() => {
		var settings = new DotNetPackSettings
		{
			OutputDirectory = artifactsDir,
			NoBuild = true,
			Configuration = configuration
		};

		foreach (var project in projects)
		{
			DotNetPack(projectFolder + project.path + project.name, settings);
		}

		if (AppVeyor.IsRunningOnAppVeyor)
		{
			foreach (var file in GetFiles(artifactsDir + "**/*"))
				AppVeyor.UploadArtifact(file.FullPath);
		}
});

Task("Publish")
	.IsDependentOn("Package")
	.Does(() => {
		var pushSettings = new DotNetNuGetPushSettings
		{
			Source = nugetSource,
			ApiKey = nugetApiKey
		};

		var pkgs = GetFiles(artifactsDir + "*.nupkg");
		foreach(var pkg in pkgs)
		{
			if(!IsNuGetPublished(pkg))
			{
				Information($"Publishing \"{pkg}\".");
				DotNetNuGetPush(pkg.FullPath, pushSettings);
			}
			else {
				Information($"Bypassing publishing \"{pkg}\" as it is already published.");
			}
		}
});

private bool IsNuGetPublished(FilePath packagePath) {
	using var package = new PackageArchiveReader(new FileStream(packagePath.FullPath, FileMode.Open));

	var latestPublishedVersions = NuGetList(
		package.NuspecReader.GetId(),
		new NuGetListSettings
		{
			Prerelease = true
		}
	);

	return latestPublishedVersions.Any(p => package.NuspecReader.GetVersion().Equals(p.Version));
}

private void UpdateVersion(string projectPath, string version)
{
	Information("UpdateVersion .................................................");
	Information(projectPath);
	// Update projectPath.json
	string pureVersion = XmlPeek(projectPath, "//Version");
	Information(pureVersion);
	string assemblyVersion = XmlPeek(projectPath, "//AssemblyVersion");
	string fileVersion = XmlPeek(projectPath, "//FileVersion");

	var updatedProjectJson = System.IO.File.ReadAllText(projectPath)
		.Replace(pureVersion, version)
		.Replace(fileVersion, version)
		.Replace(assemblyVersion, version);

	System.IO.File.WriteAllText(projectPath, updatedProjectJson);
}

private void UpdateVersion(string nuspecPath, string assemblyInfoPath, string version)
{
	Information("UpdateVersion .................................................");
	Information(nuspecPath);
	// Update nuspec file
	string pureVersion = XmlPeek(nuspecPath, "//version");
	Information(pureVersion);

	var updatedProjectJson = System.IO.File.ReadAllText(nuspecPath)
		.Replace(pureVersion, version);

	System.IO.File.WriteAllText(nuspecPath, updatedProjectJson);

	var assemblyInfo = ParseAssemblyInfo(assemblyInfoPath);
	Information("Change AssemblyInfo.cs of " + assemblyInfoPath);
	Information("Before change version is: " + assemblyInfo.AssemblyVersion);
	CreateAssemblyInfo(assemblyInfoPath, new AssemblyInfoSettings {
		FileVersion = version,
		InformationalVersion = version,
		Version = version,
		CLSCompliant = assemblyInfo.ClsCompliant,
		Company = assemblyInfo.Company,
		ComVisible = assemblyInfo.ComVisible,
		Configuration = assemblyInfo.Configuration,
		Copyright = assemblyInfo.Copyright,
		Description = assemblyInfo.Description,
		Guid = assemblyInfo.Guid,
		InternalsVisibleTo = assemblyInfo.InternalsVisibleTo,
		Product = assemblyInfo.Product,
		Title = assemblyInfo.Title,
		Trademark = assemblyInfo.Trademark
	});
}

Task("BuildAndTest")
	.IsDependentOn("Build")
	.IsDependentOn("Test");

Task("CompleteWithoutPublish")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("UploadCoverage");

if(isReleaseBuild)
{
	Information("Release build");
	Task("Complete")
		.IsDependentOn("Build")
		.IsDependentOn("Test")
		.IsDependentOn("UploadCoverage")
		.IsDependentOn("Publish");
}
else
{
	Information("Development build");
	Task("Complete")
		.IsDependentOn("Build")
		.IsDependentOn("Test")
		.IsDependentOn("UploadCoverage");
}

Task("Default")
	.IsDependentOn("Build")
	.IsDependentOn("Test")
	.IsDependentOn("Package");


RunTarget(target);