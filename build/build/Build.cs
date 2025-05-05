using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.IO;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.CI.AzurePipelines;
using System;
using Nuke.Common.Tools.GitVersion;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository GitRepository;

    [Parameter("configuration")]
    public string Configuration { get; set; }

    [Parameter("version-suffix")]
    public string VersionSuffix { get; set; }

    public bool IsRunningOnAzure { get; set; }

    public string Version { get; set; }

    [Parameter("publish-framework")]
    public string PublishFramework { get; set; }

    [Parameter("publish-runtime")]
    public string PublishRuntime { get; set; }

    [Parameter("publish-project")]
    public string PublishProject { get; set; }

    [Parameter("publish-self-contained")]
    public bool PublishSelfContained { get; set; } = true;

    AbsolutePath SourceDirectory => RootDirectory / "src";

    AbsolutePath TestsDirectory => RootDirectory / "tests";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    protected override void OnBuildInitialized()
    {
        Configuration = Configuration ?? "Release";
        VersionSuffix = VersionSuffix ?? "";
        Version = Solution.GetAllProjects("Svg.Skia").First().GetProperty("Version");
        IsRunningOnAzure = Host is AzurePipelines || Environment.GetEnvironmentVariable("LOGNAME") == "vsts";

        Console.WriteLine($"Version is: {Version}");
        Console.WriteLine($"Running on Azure: {IsRunningOnAzure}");

        if(IsRunningOnAzure)
        {
            Console.WriteLine($"Branch is: {AzurePipelines.Instance.SourceBranchName}");

            if (int.TryParse(AzurePipelines.Instance.SourceBranchName, out int minor))
            {
                // Always use branch name as minor part of version (must be an integer, i.e. complete naming release/2)
                var currentVersion = new Version(Version);
                var gruntVersion = new Version(currentVersion.Major, minor, currentVersion.Build, currentVersion.Revision);

                Console.WriteLine($"Grunt Version is: {gruntVersion}");
                Version = gruntVersion.ToString();
            }
        }
    }

    private void DeleteDirectories(IReadOnlyCollection<AbsolutePath> directories)
    {
        foreach (var directory in directories)
        {
            directory.DeleteDirectory();
        }
    }

    Target Clean => _ => _
        .Executes(() =>
        {
            
            DeleteDirectories(SourceDirectory.GlobDirectories("**/bin", "**/obj"));
            DeleteDirectories(TestsDirectory.GlobDirectories("**/bin", "**/obj"));
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetLoggers("trx")
                .SetResultsDirectory(ArtifactsDirectory / "TestResults")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .SetOutputDirectory(ArtifactsDirectory / "NuGet")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Publish => _ => _
        .DependsOn(Test)
        .Requires(() => PublishRuntime)
        .Requires(() => PublishFramework)
        .Requires(() => PublishProject)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetProject( Solution.AllProjects.FirstOrDefault(x => x.Name == PublishProject))
                .SetConfiguration(Configuration)
                .SetVersion(Version)
                .SetVersionSuffix(VersionSuffix)
                .SetFramework(PublishFramework)
                .SetRuntime(PublishRuntime)
                .SetSelfContained(PublishSelfContained)
                .SetOutput(ArtifactsDirectory / "Publish" / PublishProject + "-" + PublishFramework + "-" + PublishRuntime));
        });
}
