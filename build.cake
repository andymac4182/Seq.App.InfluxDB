///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// EXTERNAL NUGET TOOLS
//////////////////////////////////////////////////////////////////////

#Tool "xunit.runner.console"

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var projectName = "Seq.App.InfluxDB";
var buildNumber = BuildSystem.IsRunningOnTeamCity ? EnvironmentVariable("BUILD_NUMBER") : "0.0.0.0";

var solutions = GetFiles("./**/*.sln");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());

var srcDir = Directory("./src");
var artifactsDir = Directory("./artifacts");
var testResultsDir = artifactsDir + Directory("test-results");
var nupkgDir = artifactsDir + Directory("nupkg");

var globalAssemblyFile = srcDir + File("GlobalAssemblyInfo.cs");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(() =>
{
    Information("");
    Information("██████╗  █████╗ ██████╗  ██████╗ ██████╗ ██████╗ ███████╗");
    Information("██╔══██╗██╔══██╗██╔══██╗██╔════╝██╔═══██╗██╔══██╗██╔════╝");
    Information("██████╔╝███████║██████╔╝██║     ██║   ██║██║  ██║█████╗  ");
    Information("██╔══██╗██╔══██║██╔══██╗██║     ██║   ██║██║  ██║██╔══╝  ");
    Information("██████╔╝██║  ██║██║  ██║╚██████╗╚██████╔╝██████╔╝███████╗");
    Information("╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═════╝ ╚═════╝ ╚══════╝");
    Information("");
});

Teardown(() =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Build")
    .IsDependentOn("__Clean")
    .IsDependentOn("__RestoreNugetPackages")
    .IsDependentOn("__UpdateAssemblyVersionInformation")
    .IsDependentOn("__BuildSolutions")
	.IsDependentOn("__CreateNuGetPackages");

Task("__Clean")
    .Does(() =>
{
    CleanDirectories(new DirectoryPath[] {
        artifactsDir,
        testResultsDir,
        nupkgDir
    });

    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
    }
});

Task("__RestoreNugetPackages")
    .Does(() =>
{
    foreach(var solution in solutions)
    {
        Information("Restoring NuGet Packages for {0}", solution);
        NuGetRestore(solution);
    }
});

Task("__CreateNuGetPackages")
    .Does(() =>
{
    // Create Cake package.
    NuGetPack("./nuspec/Seq.App.InfluxDB.nuspec", new NuGetPackSettings {
        Version = buildNumber,
        ReleaseNotes = new [] { "" },
        BasePath = srcDir + Directory("./Seq.App.InfluxDB/bin/") + Directory(configuration),
        OutputDirectory = nupkgDir,
        Symbols = false,
        NoPackageAnalysis = true,
        Files = new [] {
            new NuSpecContent { Source = "Seq.App.InfluxDB.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "InfluxData.Net.Common.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "InfluxData.Net.InfluxDb.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "InfluxData.Net.Kapacitor.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "Microsoft.Threading.Tasks.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "Microsoft.Threading.Tasks.Extensions.Desktop.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "Microsoft.Threading.Tasks.Extensions.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "Newtonsoft.Json.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "System.Net.Http.Extensions.dll", Target = "lib/net452" },
			new NuSpecContent { Source = "System.Net.Http.Primitives.dll", Target = "lib/net452" }
        }
    });
    
});

Task("__UpdateAssemblyVersionInformation")
    .Does(() =>
{
    Information("Updating assembly version to {0}", buildNumber);

    CreateAssemblyInfo(globalAssemblyFile, new AssemblyInfoSettings {
        Version = buildNumber,
        FileVersion = buildNumber,
        Product = projectName,
        Description = projectName,
        Company = "Andrew McClenaghan",
        Copyright = "Copyright (c) " + DateTime.Now.Year
    });
});

Task("__BuildSolutions")
    .Does(() =>
{
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);

        MSBuild(solution, settings =>
            settings
                .SetConfiguration(configuration)
                .WithProperty("TreatWarningsAsErrors", "false")
                .WithProperty("RunOctoPack", "true")
                .WithProperty("OctoPackPublishPackagesToTeamCity", "false")
                .WithProperty("OctoPackPublishPackageToFileShare", MakeAbsolute(nupkgDir).ToString())
                .UseToolVersion(MSBuildToolVersion.NET46)
                .SetVerbosity(Verbosity.Minimal)
                .SetNodeReuse(false));
    }
});

Task("__RunTests")
    .Does(() =>
{
    XUnit2("./src/**/bin/" + configuration + "/*.*Tests.dll", new XUnit2Settings {
        OutputDirectory = testResultsDir,
        XmlReportV1 = true
    });
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("__Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
