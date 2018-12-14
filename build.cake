#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var artifactsFolder = "./artifacts";
var temporaryFolder = "./temp-build";
var solution = "./CakeTeamCityIntegration.sln";
var nuspec = "./Application/Application.nuspec";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
   // Executed BEFORE the first task.
   Information("Running tasks...");
});

Teardown(ctx =>
{
   // Executed AFTER the last task.
   Information("Finished running tasks.");
});

TaskSetup(setupContext =>
{
   if(TeamCity.IsRunningOnTeamCity)
   {
      TeamCity.WriteStartBuildBlock(setupContext.Task.Description ?? setupContext.Task.Name);

      TeamCity.WriteStartProgress(setupContext.Task.Description ?? setupContext.Task.Name);
   }
});

TaskTeardown(teardownContext =>
{
   if(TeamCity.IsRunningOnTeamCity)
   {
      TeamCity.WriteEndProgress(teardownContext.Task.Description ?? teardownContext.Task.Name);

      TeamCity.WriteEndBuildBlock(teardownContext.Task.Description ?? teardownContext.Task.Name);
   }
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
.IsDependentOn("Publish-Artifacts-On-TeamCity")
.Does(() => {
   Information("Default Task");
});

Task("Clean")
.Description("Create and clean folders with results")
.Does(() => {
   if(!DirectoryExists(artifactsFolder))
   {
      CreateDirectory(artifactsFolder);
   }

   CleanDirectory(artifactsFolder);

   if(DirectoryExists(temporaryFolder))
   {
      DeleteDirectory(temporaryFolder, true);
   }

   CreateDirectory(temporaryFolder);
});

Task("Restore-NuGet-Packages")
.Description("Restore NuGet packages")
.IsDependentOn("Clean")
.Does(() => {
   NuGetRestore(solution);
});

Task("Build")
.Description("Build solution")
.IsDependentOn("Restore-NuGet-Packages")
.Does(() => {
   MSBuild(solution, settings =>
      settings
         .SetConfiguration(configuration)
         .SetMSBuildPlatform(MSBuildPlatform.x86)
         .SetVerbosity(Verbosity.Normal)
         .UseToolVersion(MSBuildToolVersion.VS2017));
});

Task("Run-Tests")
.Description("Run tests")
.IsDependentOn("Build")
.Does(() => {
   var testDllsPattern = string.Format("./**/bin/{0}/*.*Tests.dll", configuration);

   var testDlls = GetFiles(testDllsPattern);

   foreach (var testDll in testDlls)
   {
      Information("\t" + testDll);
   }

   var testResultsFile = System.IO.Path.Combine(temporaryFolder, "testResults.trx");

   // MSTest(testDlls, new MSTestSettings() {
   //    ResultsFile = testResultsFile
   // });
   VSTest(testDlls, new VSTestSettings() {
      Logger = "trx",
      InIsolation = true,
      EnableCodeCoverage = true
   });

   // if(TeamCity.IsRunningOnTeamCity)
   // {
   //    TeamCity.ImportData("vstest", testResultsFile);
   // }
});

Task("Analyse-Test-Coverage")
.Description("Analyse code coverage by tests")
.IsDependentOn("Run-Tests")
.Does(() => {
   var coverageResultFile = System.IO.Path.Combine(temporaryFolder, "coverageResult.dcvr");

   var testDllsPattern = string.Format("./**/bin/{0}/*.*Tests.dll", configuration);

   var testDlls = GetFiles(testDllsPattern);

   foreach (var testDll in testDlls)
   {
      Information("\t" + testDll);
   }

   var testResultsFile = System.IO.Path.Combine(temporaryFolder, "testResults.trx");

//   DotCoverCover(tool => {
//         tool.MSTest(testDlls, new MSTestSettings() {
         // tool.VSTest(testDlls, new VSTestSettings() {
         //    ResultsFile = testResultsFile
         // });
      // },
      // new FilePath(coverageResultFile),
      // new DotCoverCoverSettings()
      //    .WithFilter("+:Application")
      //    .WithFilter("-:Application.*Tests")
      // );

   // if(TeamCity.IsRunningOnTeamCity)
   // {
   //    TeamCity.ImportData("vstest", testResultsFile);
   // }

   DotCoverReport(coverageResultFile,
      System.IO.Path.Combine(temporaryFolder, "coverageResult.html"),
      new DotCoverReportSettings {
         ReportType = DotCoverReportType.HTML
      });

   if(TeamCity.IsRunningOnTeamCity)
   {
      TeamCity.ImportDotCoverCoverage(coverageResultFile);
   }
});

Task("Create-NuGet-Package")
.Description("Create NuGet package")
//.IsDependentOn("Analyse-Test-Coverage")
.IsDependentOn("Run-Tests")
.Does(() => {
   try{
      var nuGetPackSettings = new NuGetPackSettings {
         OutputDirectory = artifactsFolder
      };

      NuGetPack(nuspec, nuGetPackSettings);
   }
   catch(Exception ex){
      Information("error {0}", ex.Message);
   }
});

Task("Publish-Artifacts-On-TeamCity")
.Description("Publish artifacts on TeamCity")
.IsDependentOn("Create-NuGet-Package")
.WithCriteria(TeamCity.IsRunningOnTeamCity)
.Does(() => {
   TeamCity.PublishArtifacts(artifactsFolder);
});

RunTarget(target);