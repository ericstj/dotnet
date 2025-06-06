// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;
using NuGet.Commands;
using NuGet.Commands.Restore.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Build.Tasks.Console
{
    internal sealed class MSBuildStaticGraphRestore : IDisposable
    {
        /// <summary>
        /// Represents the name of the environment variable that user can set to specify MSBuild binary logger parameters.
        /// </summary>
        public const string BinaryLoggerParameterEnvironmentVariable = "RESTORE_TASK_BINLOG_PARAMETERS";

        private static readonly Lazy<IMachineWideSettings> MachineWideSettingsLazy = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        /// <summary>
        /// Represents the small list of targets that must be executed in order for various restore input items to be accurate.
        /// </summary>
        private static readonly string[] TargetsToBuild =
        {
            "_CollectRestoreInputs"
        };

        private readonly IEnvironmentVariableReader _environment;

        private readonly Lazy<ConsoleLoggingQueue> _loggingQueueLazy;

        private readonly Lazy<MSBuildLogger> _msBuildLoggerLazy;

        private readonly SettingsLoadingContext _settingsLoadContext = new SettingsLoadingContext();

        public MSBuildStaticGraphRestore(IEnvironmentVariableReader environment = null)
        {
            _environment = environment ?? EnvironmentVariableWrapper.Instance;
            _loggingQueueLazy = new Lazy<ConsoleLoggingQueue>(() => new ConsoleLoggingQueue(LoggerVerbosity.Normal));
            _msBuildLoggerLazy = new Lazy<MSBuildLogger>(() => new MSBuildLogger(LoggingQueue.TaskLoggingHelper));
        }

        /// <summary>
        /// Gets a <see cref="ConsoleLoggingQueue" /> object to be used for logging.
        /// </summary>
        private ConsoleLoggingQueue LoggingQueue => _loggingQueueLazy.Value;

        /// <summary>
        /// Gets a <see cref="MSBuildLogger" /> object to be used for logging.
        /// </summary>
        private MSBuildLogger MSBuildLogger => _msBuildLoggerLazy.Value;

        public void Dispose()
        {
            if (_loggingQueueLazy.IsValueCreated)
            {
                // Disposing the logging queue will wait for the queue to be drained
                _loggingQueueLazy.Value.Dispose();
            }

            _settingsLoadContext.Dispose();
        }

        /// <summary>
        /// Restores the specified projects.
        /// </summary>
        /// <param name="entryProjectFilePath">The main project to restore.  This can be a project for a Visual Studio© Solution File.</param>
        /// <param name="globalProperties">The global properties to use when evaluation MSBuild projects.</param>
        /// <param name="options">The set of options to use when restoring.  These options come from the main MSBuild process and control how restore functions.</param>
        /// <returns><code>true</code> if the restore succeeded, otherwise <code>false</code>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<bool> RestoreAsync(string entryProjectFilePath, IDictionary<string, string> globalProperties, IReadOnlyDictionary<string, string> options)
        {
            bool interactive = IsOptionTrue(nameof(RestoreTaskEx.Interactive), options);

            string binaryLoggerParameters = GetBinaryLoggerParameters(_environment, options);

            var dependencyGraphSpec = GetDependencyGraphSpec(entryProjectFilePath, globalProperties, interactive, binaryLoggerParameters, EnvironmentVariableWrapper.Instance);

            // If the dependency graph spec is null, something went wrong evaluating the projects, so return false
            if (dependencyGraphSpec == null)
            {
                return false;
            }

            static bool HasProjectToRestore(DependencyGraphSpec dgSpec, bool restorePackagesConfig)
            {
                if (dgSpec.Restore.Count > 0)
                {
                    return true;
                }

#if NETFRAMEWORK
                if (restorePackagesConfig)
                {
                    for (int i = 0; i < dgSpec.Projects.Count; i++)
                    {
                        PackageSpec project = dgSpec.Projects[i];
                        if (project.RestoreMetadata?.ProjectStyle == ProjectStyle.PackagesConfig)
                        {
                            return true;
                        }
                    }
                }
#endif

                return false;
            }

            bool restorePackagesConfig = IsOptionTrue(nameof(RestoreTaskEx.RestorePackagesConfig), options);
            if ((string.Equals(Path.GetExtension(entryProjectFilePath), ".sln", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(entryProjectFilePath), ".slnx", StringComparison.OrdinalIgnoreCase))
                    && !HasProjectToRestore(dependencyGraphSpec, restorePackagesConfig))
            {
                MSBuildLogger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.Log_NoProjectsForRestore));
                return true;
            }

            try
            {
                // todo: need to return Restore task output properties, like in NuGet.targets
                // https://github.com/NuGet/Home/issues/13828
                List<RestoreSummary> restoreSummaries = await BuildTasksUtility.RestoreAsync(
                    dependencyGraphSpec: dependencyGraphSpec,
                    interactive,
                    recursive: IsOptionTrue(nameof(RestoreTaskEx.Recursive), options),
                    noCache: IsOptionTrue(nameof(RestoreTaskEx.NoCache), options) || IsOptionTrue(nameof(RestoreTaskEx.NoHttpCache), options),
                    ignoreFailedSources: IsOptionTrue(nameof(RestoreTaskEx.IgnoreFailedSources), options),
                    disableParallel: IsOptionTrue(nameof(RestoreTaskEx.DisableParallel), options),
                    force: IsOptionTrue(nameof(RestoreTaskEx.Force), options),
                    forceEvaluate: IsOptionTrue(nameof(RestoreTaskEx.ForceEvaluate), options),
                    hideWarningsAndErrors: IsOptionTrue(nameof(RestoreTaskEx.HideWarningsAndErrors), options),
                    restorePC: restorePackagesConfig,
                    cleanupAssetsForUnsupportedProjects: IsOptionTrue(nameof(RestoreTaskEx.CleanupAssetsForUnsupportedProjects), options),
                    log: MSBuildLogger,
                cancellationToken: CancellationToken.None);
                bool result = restoreSummaries.All(rs => rs.Success);

                LogFilesToEmbedInBinlog(dependencyGraphSpec, options);

                return result;
            }
            catch (Exception e)
            {
                LogErrorFromException(e);

                return false;
            }
        }

        /// <summary>
        /// Generates a dependency graph spec for the given properties.
        /// </summary>
        /// <param name="entryProjectFilePath">The main project to generate that graph for.  This can be a project for a Visual Studio© Solution File.</param>
        /// <param name="globalProperties">The global properties to use when evaluation MSBuild projects.</param>
        /// <param name="options">The set of options to use to generate the graph, including the restore graph output path.</param>
        /// <returns><code>true</code> if the dependency graph spec was generated and written, otherwise <code>false</code>.</returns>
        public bool WriteDependencyGraphSpec(string entryProjectFilePath, IDictionary<string, string> globalProperties, IReadOnlyDictionary<string, string> options)
        {
            bool interactive = IsOptionTrue(nameof(RestoreTaskEx.Interactive), options);

            string binaryLoggerParameters = GetBinaryLoggerParameters(_environment, options);

            var dependencyGraphSpec = GetDependencyGraphSpec(entryProjectFilePath, globalProperties, interactive, binaryLoggerParameters, EnvironmentVariableWrapper.Instance);

            try
            {
                if (dependencyGraphSpec == null)
                {
                    LoggingQueue.TaskLoggingHelper.LogError(Strings.Error_DgSpecGenerationFailed);
                    return false;
                }

                if (options.TryGetValue("RestoreGraphOutputPath", out var path))
                {
                    dependencyGraphSpec.Save(path);
                    return true;
                }
                else
                {
                    LoggingQueue.TaskLoggingHelper.LogError(Strings.Error_MissingRestoreGraphOutputPath);
                }
            }
            catch (Exception e)
            {
                LogErrorFromException(e);
            }
            return false;
        }

        /// <summary>
        /// Gets parameters for the MSBuild binary logger.
        /// </summary>
        /// <param name="environment">An <see cref="IEnvironmentVariableReader" /> to use when reading environment variables.</param>
        /// <param name="options">The <see cref="IReadOnlyCollection{TKey, TValue}" /> containing user supplied options.</param>
        /// <returns>A <see cref="string" /> containing the parameters for the MSBuild binary logger if specified, otherwise <see langword="null" />.</returns>
        internal static string GetBinaryLoggerParameters(IEnvironmentVariableReader environment, IReadOnlyDictionary<string, string> options)
        {
            string binaryLoggerParameters = environment.GetEnvironmentVariable(BinaryLoggerParameterEnvironmentVariable);

            if (!string.IsNullOrEmpty(binaryLoggerParameters))
            {
                return binaryLoggerParameters;
            }

            // Return null if the binary logger is not enabled
            if (!IsOptionTrue(nameof(RestoreTaskEx.EnableBinaryLogger), options))
            {
                return null;
            }

            if (options.TryGetValue(nameof(RestoreTaskEx.BinaryLoggerParameters), out binaryLoggerParameters) && !string.IsNullOrWhiteSpace(binaryLoggerParameters))
            {
                // User supplied the parameters
                return binaryLoggerParameters;
            }

            // Default parameters
            return binaryLoggerParameters = "LogFile=nuget.binlog";
        }

        /// <summary>
        /// Gets the framework references per target framework for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get framework references for.</param>
        /// <returns>A <see cref="List{FrameworkDependency}" /> containing the framework references for the specified project.</returns>
        internal static IReadOnlyCollection<FrameworkDependency> GetFrameworkReferences(IMSBuildProject project)
        {
            // Get the unique FrameworkReference items, ignoring duplicates
            List<IMSBuildItem> frameworkReferenceItems = GetDistinctItemsOrEmpty(project, "FrameworkReference").ToList();

            if (frameworkReferenceItems.Count == 0)
            {
                return null;
            }

            // For best performance, its better to create a list with the exact number of items needed rather than using a LINQ statement or AddRange.  This is because if the list
            // is not allocated with enough items, the list has to be grown which can slow things down
            var frameworkDependencies = new FrameworkDependency[frameworkReferenceItems.Count];

            for (int i = 0; i < frameworkReferenceItems.Count; i++)
            {
                var frameworkReferenceItem = frameworkReferenceItems[i];
                var privateAssets = MSBuildStringUtility.Split(frameworkReferenceItem.GetProperty("PrivateAssets"));

                frameworkDependencies[i] = new FrameworkDependency(frameworkReferenceItem.Identity, FrameworkDependencyFlagsUtils.GetFlags(privateAssets));
            }

            return frameworkDependencies;
        }

        /// <summary>
        /// Gets the package downloads for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get package downloads for.</param>
        /// <returns>An <see cref="IEnumerable{DownloadDependency}" /> containing the package downloads for the specified project.</returns>
        internal static IEnumerable<DownloadDependency> GetPackageDownloads(IMSBuildProject project)
        {
            // Get the distinct PackageDownload items, ignoring duplicates
            foreach (IMSBuildItem projectItemInstance in GetDistinctItemsOrEmpty(project, "PackageDownload"))
            {
                string id = projectItemInstance.Identity;

                // PackageDownload items can contain multiple versions
                string versionRanges = projectItemInstance.GetProperty("Version");
                if (string.IsNullOrEmpty(versionRanges))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageDownload_NoVersion, id));
                }

                foreach (var version in MSBuildStringUtility.Split(versionRanges))
                {
                    // Validate the version range
                    VersionRange versionRange = !string.IsNullOrWhiteSpace(version) ? VersionRange.Parse(version) : VersionRange.All;

                    if (!(versionRange.HasLowerAndUpperBounds && versionRange.MinVersion.Equals(versionRange.MaxVersion)))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageDownload_OnlyExactVersionsAreAllowed, id, versionRange.OriginalString));
                    }

                    yield return new DownloadDependency(id, versionRange);
                }
            }
        }

        /// <summary>
        /// Gets the centrally defined package version information.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get PackageVersion for.</param>
        /// <returns>An <see cref="IEnumerable{CentralPackageVersion}" /> containing the package versions for the specified project.</returns>
        internal static Dictionary<string, CentralPackageVersion> GetCentralPackageVersions(IMSBuildProject project)
        {
            var result = new Dictionary<string, CentralPackageVersion>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<IMSBuildItem> packageVersionItems = GetDistinctItemsOrEmpty(project, "PackageVersion");

            foreach (var projectItemInstance in packageVersionItems)
            {
                string id = projectItemInstance.Identity;
                string version = projectItemInstance.GetProperty("Version");
                VersionRange versionRange = string.IsNullOrWhiteSpace(version) ? VersionRange.All : VersionRange.Parse(version);

                result.Add(id, new CentralPackageVersion(id, versionRange));
            }

            return result;
        }

        /// <summary>
        /// Gets the package references for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get package references for.</param>
        /// <param name="isCentralPackageVersionManagementEnabled">A flag for central package version management being enabled.</param>
        /// <returns>A <see cref="List{LibraryDependency}" /> containing the package references for the specified project.</returns>
        internal static ImmutableArray<LibraryDependency> GetPackageReferences(IMSBuildProject project, bool isCentralPackageVersionManagementEnabled, IReadOnlyDictionary<string, CentralPackageVersion> centralPackageVersions)
        {
            // Get the distinct PackageReference items, ignoring duplicates
            List<IMSBuildItem> packageReferenceItems = GetDistinctItemsOrEmpty(project, "PackageReference").ToList();

            var libraryDependencies = new LibraryDependency[packageReferenceItems.Count];

            for (int i = 0; i < packageReferenceItems.Count; i++)
            {
                var packageReferenceItem = packageReferenceItems[i];
                bool autoReferenced = packageReferenceItem.IsPropertyTrue("IsImplicitlyDefined");
                string version = packageReferenceItem.GetProperty("Version");

                VersionRange versionRange = string.IsNullOrWhiteSpace(version) ? null : VersionRange.Parse(version);
                bool versionDefined = versionRange != null;
                if (versionRange == null && !isCentralPackageVersionManagementEnabled)
                {
                    versionRange = VersionRange.All;
                }

                string versionOverrideString = packageReferenceItem.GetProperty("VersionOverride");
                var versionOverrideRange = string.IsNullOrWhiteSpace(versionOverrideString) ? null : VersionRange.Parse(versionOverrideString);

                CentralPackageVersion centralPackageVersion = null;
                bool isCentrallyManaged = !versionDefined && !autoReferenced && isCentralPackageVersionManagementEnabled && versionOverrideRange == null && centralPackageVersions != null && centralPackageVersions.TryGetValue(packageReferenceItem.Identity, out centralPackageVersion);
                if (isCentrallyManaged)
                {
                    versionRange = centralPackageVersion.VersionRange;
                }
                versionRange = versionOverrideRange ?? versionRange;

                ImmutableArray<NuGetLogCode> noWarn = MSBuildStringUtility.GetNuGetLogCodes(packageReferenceItem.GetProperty("NoWarn"));

                libraryDependencies[i] = new LibraryDependency()
                {
                    AutoReferenced = autoReferenced,
                    GeneratePathProperty = packageReferenceItem.IsPropertyTrue("GeneratePathProperty"),
                    Aliases = packageReferenceItem.GetProperty("Aliases"),
                    IncludeType = GetLibraryIncludeFlags(packageReferenceItem.GetProperty("IncludeAssets"), LibraryIncludeFlags.All) & ~GetLibraryIncludeFlags(packageReferenceItem.GetProperty("ExcludeAssets"), LibraryIncludeFlags.None),
                    LibraryRange = new LibraryRange(
                        packageReferenceItem.Identity,
                        versionRange,
                        LibraryDependencyTarget.Package),
                    SuppressParent = GetLibraryIncludeFlags(packageReferenceItem.GetProperty("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent),
                    VersionOverride = versionOverrideRange,
                    NoWarn = noWarn,
                    VersionCentrallyManaged = isCentrallyManaged,
                };
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(libraryDependencies);
        }

        internal static Dictionary<string, PrunePackageReference> GetPrunePackageReferences(IMSBuildProject project)
        {
            var result = new Dictionary<string, PrunePackageReference>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<IMSBuildItem> PrunePackageReferences = GetDistinctItemsOrEmpty(project, "PrunePackageReference");

            foreach (var projectItemInstance in PrunePackageReferences)
            {
                string id = projectItemInstance.Identity;
                string versionString = projectItemInstance.GetProperty("Version");
                result.Add(id, PrunePackageReference.Create(id, versionString));
            }

            return result;
        }

        /// <summary>
        /// Gets the packages path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the project.</param>
        /// <returns>The full path to the packages directory for the specified project.</returns>
        internal static string GetPackagesPath(IMSBuildProject project, ISettings settings)
        {
            return RestoreSettingsUtils.GetValue(
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetGlobalProperty("RestorePackagesPath")),
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestorePackagesPath")),
                () => SettingsUtility.GetGlobalPackagesFolder(settings));
        }

        /// <summary>
        /// Gets the name of the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The name of the specified project.</returns>
        internal static string GetProjectName(IMSBuildProject project)
        {
            string packageId = project.GetProperty("PackageId");

            if (!string.IsNullOrWhiteSpace(packageId))
            {
                // If the PackageId property was specified, return that
                return packageId;
            }

            string assemblyName = project.GetProperty("AssemblyName");

            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                // If the AssemblyName property was specified, return that
                return assemblyName;
            }

            // By default return the MSBuildProjectName which is a built-in property that represents the name of the project file without the file extension
            return project.GetProperty("MSBuildProjectName");
        }

        /// <summary>
        /// Gets the project references of the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get project references for.</param>
        /// <returns>A <see cref="List{ProjectRestoreReference}" /> containing the project references for the specified project.</returns>
        internal static List<ProjectRestoreReference> GetProjectReferences(IMSBuildProject project)
        {
            // Get the unique list of ProjectReference items that have the ReferenceOutputAssembly metadata set to "true", ignoring duplicates
            var projectReferenceItems = project.GetItems("ProjectReference")
                .Where(i => i.IsPropertyTrue("ReferenceOutputAssembly", defaultValue: true))
                .Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance)
                .ToList();

            var projectReferences = new List<ProjectRestoreReference>(projectReferenceItems.Count);

            foreach (var projectReferenceItem in projectReferenceItems)
            {
                string fullPath = projectReferenceItem.GetProperty("FullPath");

                projectReferences.Add(new ProjectRestoreReference
                {
                    ExcludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("ExcludeAssets"), LibraryIncludeFlags.None),
                    IncludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("IncludeAssets"), LibraryIncludeFlags.All),
                    PrivateAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent),
                    ProjectPath = fullPath,
                    ProjectUniqueName = fullPath
                });
            }

            return projectReferences;
        }

        /// <summary>
        /// Gets the restore metadata framework information for the specified projects.
        /// </summary>
        /// <param name="projects">A <see cref="IReadOnlyDictionary{NuGetFramework,ProjectInstance}" /> representing the target frameworks and their corresponding projects.</param>
        /// <returns>A <see cref="List{ProjectRestoreMetadataFrameworkInfo}" /> containing the restore metadata framework information for the specified project.</returns>
        internal static List<ProjectRestoreMetadataFrameworkInfo> GetProjectRestoreMetadataFrameworkInfos(List<TargetFrameworkInformation> targetFrameworkInfos, IReadOnlyDictionary<string, IMSBuildProject> projects)
        {
            var projectRestoreMetadataFrameworkInfos = new List<ProjectRestoreMetadataFrameworkInfo>(projects.Count);

            foreach (var targetFrameworkInfo in targetFrameworkInfos)
            {
                var project = projects[targetFrameworkInfo.TargetAlias];
                projectRestoreMetadataFrameworkInfos.Add(new ProjectRestoreMetadataFrameworkInfo(targetFrameworkInfo.FrameworkName)
                {
                    TargetAlias = targetFrameworkInfo.TargetAlias,
                    ProjectReferences = GetProjectReferences(project)
                });
            }

            return projectRestoreMetadataFrameworkInfos;
        }

        /// <summary>
        /// Gets the target frameworks for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> representing the main project.</param>
        /// <param name="innerNodes">An <see cref="IReadOnlyDictionary{String,IMSBuildProject}" /> representing all inner projects by their target framework.</param>
        /// <returns></returns>
        internal static IReadOnlyDictionary<string, IMSBuildProject> GetProjectTargetFrameworks(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> innerNodes)
        {
            var projectFrameworkStrings = GetTargetFrameworkStrings(project);
            var projectTargetFrameworks = new Dictionary<string, IMSBuildProject>();

            if (projectFrameworkStrings.Length > 0)
            {
                foreach (var projectTargetFramework in projectFrameworkStrings)
                {
                    // Attempt to get the corresponding project instance for the target framework.  If one is not found, then the project must not target multiple frameworks
                    // and the main project should be used
                    if (!innerNodes.TryGetValue(projectTargetFramework, out IMSBuildProject innerNode))
                    {
                        innerNode = project;
                    }
                    // Add the target framework and associate it with the project instance to be used for gathering details
                    projectTargetFrameworks[projectTargetFramework] = innerNode;
                }
            }
            else
            {
                // Attempt to get the corresponding project instance for the target framework.  If one is not found, then the project must not target multiple frameworks
                // and the main project should be used
                projectTargetFrameworks[string.Empty] = project;
            }

            return projectTargetFrameworks;
        }

        internal static string[] GetTargetFrameworkStrings(IMSBuildProject project)
        {
            var targetFrameworks = project.GetProperty("TargetFrameworks");
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = project.GetProperty("TargetFramework");
            }
            var projectFrameworkStrings = MSBuildStringUtility.Split(targetFrameworks);
            return projectFrameworkStrings;
        }

        /// <summary>
        /// Gets the version of the project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The <see cref="NuGetVersion" /> of the specified project if one was found, otherwise <see cref="PackageSpec.DefaultVersion" />.</returns>
        internal static NuGetVersion GetProjectVersion(IMSBuildItem project)
        {
            string version = project.GetProperty("PackageVersion") ?? project.GetProperty("Version");

            if (version == null)
            {
                return PackageSpec.DefaultVersion;
            }

            return NuGetVersion.Parse(version);
        }

        /// <summary>
        /// Gets the repository path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>The repository path of the specified project.</returns>
        internal static string GetRepositoryPath(IMSBuildProject project, ISettings settings)
        {
            return RestoreSettingsUtils.GetValue(
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetGlobalProperty("RestoreRepositoryPath")),
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestoreRepositoryPath")),
                () => SettingsUtility.GetRepositoryPath(settings),
                () =>
                {
                    string solutionDir = project.GetProperty("SolutionPath");

                    solutionDir = string.Equals(solutionDir, "*Undefined*", StringComparison.OrdinalIgnoreCase)
                        ? project.Directory
                        : Path.GetDirectoryName(solutionDir);

                    return UriUtility.GetAbsolutePath(solutionDir, PackagesConfig.PackagesNodeName);
                });
        }

        /// <summary>
        /// Gets the restore output path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The full path to the restore output directory for the specified project if a value is specified, otherwise <code>null</code>.</returns>
        internal static string GetRestoreOutputPath(IMSBuildProject project)
        {
            string outputPath = project.GetProperty("RestoreOutputPath") ?? project.GetProperty("MSBuildProjectExtensionsPath");

            return outputPath == null ? null : Path.GetFullPath(Path.Combine(project.Directory, outputPath));
        }

        /// <summary>
        /// Gets the package sources of the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildItem" /> representing the project..</param>
        /// <param name="innerNodes">An <see cref="IReadOnlyCollection{IMSBuildItem}" /> containing the inner nodes of the project if its targets multiple frameworks.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>A <see cref="List{PackageSource}" /> object containing the packages sources for the specified project.</returns>
        internal static List<PackageSource> GetSources(IMSBuildProject project, IReadOnlyCollection<IMSBuildProject> innerNodes, ISettings settings)
        {
            return BuildTasksUtility.GetSources(
                project.GetGlobalProperty("OriginalMSBuildStartupDirectory"),
                project.Directory,
                project.SplitPropertyValueOrNull("RestoreSources"),
                project.SplitGlobalPropertyValueOrNull("RestoreSources"),
                innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectSources"))),
                settings)
                .Select(i => new PackageSource(i))
                .ToList();
        }

        /// <summary>
        /// Gets a value indicating if the specified project is a legacy project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns><code>true</code> if the specified project is considered legacy, otherwise <code>false</code>.</returns>
        internal static bool IsLegacyProject(IMSBuildItem project)
        {
            // We consider the project to be legacy if it does not specify TargetFramework or TargetFrameworks
            return project.GetProperty("TargetFramework") == null && project.GetProperty("TargetFrameworks") == null;
        }

        /// <summary>
        /// Determines of the specified option is <code>true</code>.
        /// </summary>
        /// <param name="name">The name of the option.</param>
        /// <param name="options">A <see cref="Dictionary{String,String}" />containing options.</param>
        /// <returns><code>true</code> if the specified option is true, otherwise <code>false</code>.</returns>
        internal static bool IsOptionTrue(string name, IReadOnlyDictionary<string, string> options)
        {
            return options.TryGetValue(name, out string value) && StringComparer.OrdinalIgnoreCase.Equals(value, bool.TrueString);
        }

        /// <summary>
        /// Gets the <see cref="LibraryIncludeFlags" /> for the specified value.
        /// </summary>
        /// <param name="value">A semicolon delimited list of include flags.</param>
        /// <param name="defaultValue">The default value ot return if the value contains no flags.</param>
        /// <returns>The <see cref="LibraryIncludeFlags" /> for the specified value, otherwise the <paramref name="defaultValue" />.</returns>
        private static LibraryIncludeFlags GetLibraryIncludeFlags(string value, LibraryIncludeFlags defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            string[] parts = MSBuildStringUtility.Split(value);

            return parts.Length > 0 ? LibraryIncludeFlagUtils.GetFlags(parts) : defaultValue;
        }

        /// <summary>
        /// Gets the list of project graph entry points.  If the entry project is a solution, this method returns all of the projects it contains.
        /// </summary>
        /// <param name="entryProjectPath">The full path to the main project or solution file.</param>
        /// <param name="globalProperties">An <see cref="IDictionary{String,String}" /> representing the global properties for the project.</param>
        /// <returns></returns>
        private List<ProjectGraphEntryPoint> GetProjectGraphEntryPoints(string entryProjectPath, IDictionary<string, string> globalProperties)
        {
            // If the project's extension is .sln, parse it as a Visual Studio solution and return the projects it contains
            var extension = Path.GetExtension(entryProjectPath);
            if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var solutionFile = SolutionFile.Parse(entryProjectPath);

                IEnumerable<ProjectInSolution> projectsKnownToMSBuild = solutionFile.ProjectsInOrder.Where(i => i.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat);
                IEnumerable<ProjectInSolution> projectsNotKnownToMSBuild = solutionFile.ProjectsInOrder.Except(projectsKnownToMSBuild);

                if (projectsNotKnownToMSBuild.Any())
                {
                    IList<string> projects = projectsNotKnownToMSBuild.Select(project => project.ProjectName).ToList();

                    MSBuildLogger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                        Strings.Log_ProjectsInSolutionNotKnowntoMSBuild,
                        projects.Count, string.Join(",", projects)));
                }

                return projectsKnownToMSBuild.Select(i => new ProjectGraphEntryPoint(i.AbsolutePath, globalProperties)).ToList();
            }

            // Return just the main project in a list if its not a solution file
            return new List<ProjectGraphEntryPoint>
            {
                new ProjectGraphEntryPoint(entryProjectPath, globalProperties),
            };
        }

        /// <summary>
        /// Gets the target framework information for the specified project.  This includes the package references, package downloads, and framework references.
        /// </summary>
        /// <param name="projectInnerNodes">An <see cref="IReadOnlyDictionary{NuGetFramework,ProjectInstance} "/> containing the projects by their target framework.</param>
        /// <param name="isCpvmEnabled">A flag that is true if the Central Package Management was enabled.</param>
        /// <returns>A <see cref="List{TargetFrameworkInformation}" /> containing the target framework information for the specified project.</returns>
        internal static List<TargetFrameworkInformation> GetTargetFrameworkInfos(IReadOnlyDictionary<string, IMSBuildProject> projectInnerNodes, bool isCpvmEnabled)
        {
            var targetFrameworkInfos = new List<TargetFrameworkInformation>(projectInnerNodes.Count);

            foreach (var projectInnerNode in projectInnerNodes)
            {
                var msBuildProjectInstance = projectInnerNode.Value;
                var targetAlias = string.IsNullOrEmpty(projectInnerNode.Key) ? string.Empty : projectInnerNode.Key;

                NuGetFramework targetFramework = MSBuildProjectFrameworkUtility.GetProjectFramework(
                    projectFilePath: projectInnerNode.Value.FullPath,
                    targetFrameworkMoniker: msBuildProjectInstance.GetProperty("TargetFrameworkMoniker"),
                    targetPlatformMoniker: msBuildProjectInstance.GetProperty("TargetPlatformMoniker"),
                    targetPlatformMinVersion: msBuildProjectInstance.GetProperty("TargetPlatformMinVersion"),
                    clrSupport: msBuildProjectInstance.GetProperty("CLRSupport"),
                    windowsTargetPlatformMinVersion: msBuildProjectInstance.GetProperty("WindowsTargetPlatformMinVersion"));

                var packageTargetFallback = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty("PackageTargetFallback")).Select(NuGetFramework.Parse).ToList();

                var assetTargetFallbackEnum = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.AssetTargetFallback))).Select(NuGetFramework.Parse).ToList();

                AssetTargetFallbackUtility.EnsureValidFallback(packageTargetFallback, assetTargetFallbackEnum, msBuildProjectInstance.FullPath);

                (targetFramework, ImmutableArray<NuGetFramework> imports, bool assetTargetFallback, bool warn) = AssetTargetFallbackUtility.GetFallbackFrameworkInformation(targetFramework, packageTargetFallback, assetTargetFallbackEnum);

                IReadOnlyDictionary<string, CentralPackageVersion> centralPackageVersions = null;
                if (isCpvmEnabled)
                {
                    centralPackageVersions = GetCentralPackageVersions(msBuildProjectInstance);
                }

                var dependencies = GetPackageReferences(msBuildProjectInstance, isCpvmEnabled, centralPackageVersions);
                var prunedReferences = msBuildProjectInstance.IsPropertyTrue("RestoreEnablePackagePruning") ? GetPrunePackageReferences(msBuildProjectInstance) : [];

                var targetFrameworkInformation = new TargetFrameworkInformation()
                {
                    AssetTargetFallback = assetTargetFallback,
                    CentralPackageVersions = centralPackageVersions,
                    Dependencies = dependencies,
                    DownloadDependencies = GetPackageDownloads(msBuildProjectInstance).ToImmutableArray(),
                    FrameworkName = targetFramework,
                    Imports = imports,
                    FrameworkReferences = GetFrameworkReferences(msBuildProjectInstance),
                    PackagesToPrune = prunedReferences,
                    RuntimeIdentifierGraphPath = msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.RuntimeIdentifierGraphPath)),
                    TargetAlias = targetAlias,
                    Warn = warn
                };

                targetFrameworkInfos.Add(targetFrameworkInformation);
            }

            return targetFrameworkInfos;
        }

        /// <summary>
        /// Gets a <see cref="DependencyGraphSpec" /> for the specified project.
        /// </summary>
        /// <param name="entryProjectPath">The full path to a project or Visual Studio Solution File.</param>
        /// <param name="globalProperties">An <see cref="IDictionary{String,String}" /> containing the global properties to use when evaluation MSBuild projects.</param>
        /// <param name="interactive"><see langword="true" /> if the build is allowed to interact with the user, otherwise <see langword="false" />.</param>
        /// <returns>A <see cref="DependencyGraphSpec" /> for the specified project if they could be loaded, otherwise <code>null</code>.</returns>
        private DependencyGraphSpec GetDependencyGraphSpec(string entryProjectPath, IDictionary<string, string> globalProperties, bool interactive, string binaryLoggerParameters, IEnvironmentVariableReader environmentVariableReader)
        {
            string envVar = environmentVariableReader.GetEnvironmentVariable("NUGET_USE_NEW_PACKAGESPEC_FACTORY");
            if (!string.Equals(envVar, bool.FalseString, StringComparison.OrdinalIgnoreCase))
            {
                return GetDependencyGraphSpec(
                    entryProjectPath,
                    globalProperties,
                    interactive,
                    binaryLoggerParameters,
                    createProjectFactory: static (string projectPath, (ProjectInstance projectInstance, string targetFramework) args) =>
                    {
                        var adapter = new RestoreProjectAdapter(args.projectInstance.FullPath);
                        adapter.AddTargetFramework(args.targetFramework, new TargetFrameworkAdapter(args.projectInstance));
                        return adapter;
                    },
                    updateProjectFactory: static (string projectPath, RestoreProjectAdapter project, (ProjectInstance projectInstance, string targetFramework) args) =>
                    {
                        project.AddTargetFramework(args.targetFramework, new TargetFrameworkAdapter(args.projectInstance));
                        return project;
                    },
                    projectFinalizeDelegate: static project => project.Prepare(),
                    getPackageSpec: project =>
                    {
                        var settings = RestoreSettingsUtils.ReadSettings(
                            project.OuterBuild.GetProperty("RestoreSolutionDirectory"),
                            project.OuterBuild.GetProperty("RestoreRootConfigDirectory") ?? project.Directory,
                            UriUtility.GetAbsolutePath(project.Directory, project.OuterBuild.GetProperty("RestoreConfigFile")),
                            MachineWideSettingsLazy,
                            _settingsLoadContext);

                        var packageSpec = PackageSpecFactory.GetPackageSpec(project, settings);
                        return packageSpec;
                    });
            }
            else
            {
                // Delete this code path once PackageSpecFactory.GetPackageSpec has been tested long enough to trust.
                return GetDependencyGraphSpec(
                    entryProjectPath,
                    globalProperties,
                    interactive,
                    binaryLoggerParameters,
                    createProjectFactory: static (string projectPath, (ProjectInstance projectInstance, string targetFramework) args) =>
                        new ProjectWithInnerNodes(args.targetFramework, new MSBuildProjectInstance(args.projectInstance)),
                    updateProjectFactory: static (string projectPath, ProjectWithInnerNodes project, (ProjectInstance projectInstance, string targetFramework) args) =>
                        project.Add(args.targetFramework, new MSBuildProjectInstance(args.projectInstance)),
                    projectFinalizeDelegate: null,
                    getPackageSpec: project =>
                    {
                        var packageSpec = GetPackageSpec(project.OuterProject, project);
                        return packageSpec;
                    });
            }
        }

        private DependencyGraphSpec GetDependencyGraphSpec<TProject>(
            string entryProjectPath,
            IDictionary<string, string> globalProperties,
            bool interactive,
            string binaryLoggerParameters,
            Func<string, (ProjectInstance, string), TProject> createProjectFactory,
            Func<string, TProject, (ProjectInstance, string), TProject> updateProjectFactory,
            Action<TProject> projectFinalizeDelegate,
            Func<TProject, PackageSpec> getPackageSpec)
        {
            try
            {
                MSBuildLogger.LogMinimal(Strings.DeterminingProjectsToRestore);

                var entryProjects = GetProjectGraphEntryPoints(entryProjectPath, globalProperties);

                // Load the projects via MSBuild and create an array of them since Parallel.ForEach is optimized for arrays
                var projects = LoadProjects(entryProjects, interactive, binaryLoggerParameters, createProjectFactory, updateProjectFactory, projectFinalizeDelegate);

                // If no projects were loaded, return an empty DependencyGraphSpec
                if (projects == null || projects.Count == 0)
                {
                    return new DependencyGraphSpec();
                }

                var sw = Stopwatch.StartNew();

                var dependencyGraphSpec = new DependencyGraphSpec(isReadOnly: true);

                // Unique names created by the MSBuild restore target are project paths, these
                // can be different on case-insensitive file systems for the same project file.
                // To workaround this unique names should be compared based on the OS.
                var uniqueNameComparer = PathUtility.GetStringComparerBasedOnOS();
                var projectPathLookup = new ConcurrentDictionary<string, string>(uniqueNameComparer);

                try
                {
                    // Get the PackageSpecs in parallel because creating each one is relatively expensive so parallelism speeds things up
                    Parallel.ForEach(projects, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, project =>
                    {
                        PackageSpec packageSpec = getPackageSpec(project.Value);

                        if (packageSpec != null)
                        {
                            // Keep track of all project path casings
                            var uniqueName = packageSpec.RestoreMetadata.ProjectUniqueName;
                            if (uniqueName != null && !projectPathLookup.ContainsKey(uniqueName))
                            {
                                projectPathLookup.TryAdd(uniqueName, uniqueName);
                            }

                            var projectPath = packageSpec.RestoreMetadata.ProjectPath;
                            if (projectPath != null && !projectPathLookup.ContainsKey(projectPath))
                            {
                                projectPathLookup.TryAdd(projectPath, projectPath);
                            }

                            lock (dependencyGraphSpec)
                            {
                                dependencyGraphSpec.AddProject(packageSpec);
                            }
                        }
                    });
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    LogErrorFromException(e);

                    return null;
                }

                // Fix project reference casings to match the original project on case insensitive file systems.
                MSBuildRestoreUtility.NormalizePathCasings(projectPathLookup, dependencyGraphSpec);

                // Remove references to projects that could not be read by restore.
                MSBuildRestoreUtility.RemoveMissingProjects(dependencyGraphSpec);

                // Add all entry projects if they support restore.  In most cases this is just a single project but if the entry
                // project is a solution, then all projects in the solution are added (if they support restore)
                foreach (var entryPoint in entryProjects)
                {
                    PackageSpec project = dependencyGraphSpec.GetProjectSpec(entryPoint.ProjectFile);

                    if (project != null && BuildTasksUtility.DoesProjectSupportRestore(project))
                    {
                        dependencyGraphSpec.AddRestore(entryPoint.ProjectFile);
                    }
                }

                sw.Stop();

                MSBuildLogger.LogDebug(string.Format(CultureInfo.CurrentCulture, Strings.CreatedDependencyGraphSpec, sw.ElapsedMilliseconds));

                return dependencyGraphSpec;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                LogErrorFromException(e);
            }

            return null;
        }

        /// <summary>
        /// Gets a <see cref="PackageSpec" /> for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> object that represents the project.</param>
        /// <param name="allInnerNodes">An <see cref="IReadOnlyDictionary{String,IMSBuildProject}" /> that represents all inner projects by their target framework.</param>
        /// <returns></returns>
        private PackageSpec GetPackageSpec(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> allInnerNodes)
        {
            var settings = RestoreSettingsUtils.ReadSettings(
                project.GetProperty("RestoreSolutionDirectory"),
                project.GetProperty("RestoreRootConfigDirectory") ?? project.Directory,
                UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestoreConfigFile")),
                MachineWideSettingsLazy,
                _settingsLoadContext);

            // Get the target frameworks for the project and the project instance for each framework
            var projectsByTargetFramework = GetProjectTargetFrameworks(project, allInnerNodes);

            (ProjectRestoreMetadata restoreMetadata, List<TargetFrameworkInformation> targetFrameworkInfos) = GetProjectRestoreMetadataAndTargetFrameworkInformation(project, projectsByTargetFramework, settings);

            if (restoreMetadata == null || targetFrameworkInfos == null)
            {
                return null;
            }

            var packageSpec = new PackageSpec(targetFrameworkInfos)
            {
                FilePath = project.FullPath,
                Name = restoreMetadata.ProjectName,
                RestoreMetadata = restoreMetadata,
                RuntimeGraph = new RuntimeGraph(
                    MSBuildStringUtility.Split($"{project.GetProperty("RuntimeIdentifiers")};{project.GetProperty("RuntimeIdentifier")}")
                        .Concat(projectsByTargetFramework.Values.SelectMany(i => MSBuildStringUtility.Split($"{i.GetProperty("RuntimeIdentifiers")};{i.GetProperty("RuntimeIdentifier")}")))
                        .Distinct(StringComparer.Ordinal)
                        .Select(rid => new RuntimeDescription(rid))
                        .ToList(),
                    MSBuildStringUtility.Split(project.GetProperty("RuntimeSupports"))
                        .Distinct(StringComparer.Ordinal)
                        .Select(s => new CompatibilityProfile(s))
                        .ToList()
                    ),
                Version = GetProjectVersion(project)
            };

            return packageSpec;
        }

        /// <summary>
        /// Gets the restore metadata and target framework information for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> representing the project.</param>
        /// <param name="projectsByTargetFramework">A <see cref="IReadOnlyDictionary{NuGetFramework,IMSBuildProject}" /> containing the inner nodes by target framework.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>A <see cref="Tuple" /> containing the <see cref="ProjectRestoreMetadata" /> and <see cref="List{TargetFrameworkInformation}" /> for the specified project.</returns>
        private (ProjectRestoreMetadata RestoreMetadata, List<TargetFrameworkInformation> TargetFrameworkInfos) GetProjectRestoreMetadataAndTargetFrameworkInformation(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> projectsByTargetFramework, ISettings settings)
        {
            string projectName = GetProjectName(project);

            string outputPath = GetRestoreOutputPath(project);

            (ProjectStyle projectStyle, string packagesConfigFilePath) = GetProjectStyle(project, projectsByTargetFramework, MSBuildLogger);

            (bool isCentralPackageManagementEnabled, bool isCentralPackageVersionOverrideDisabled, bool isCentralPackageTransitivePinningEnabled, bool isCentralPackageFloatingVersionsEnabled) = MSBuildRestoreUtility.GetCentralPackageManagementSettings(project, projectStyle);

            RestoreAuditProperties auditProperties = MSBuildRestoreUtility.GetRestoreAuditProperties(project, projectsByTargetFramework.Values, GetAuditSuppressions(project));

            List<TargetFrameworkInformation> targetFrameworkInfos = GetTargetFrameworkInfos(projectsByTargetFramework, isCentralPackageManagementEnabled);

            List<IMSBuildProject> innerNodes = projectsByTargetFramework.Values.ToList();

            ProjectRestoreMetadata restoreMetadata;

            if (projectStyle == ProjectStyle.PackagesConfig)
            {
                restoreMetadata = new PackagesConfigProjectRestoreMetadata
                {
                    PackagesConfigPath = packagesConfigFilePath,
                    RepositoryPath = GetRepositoryPath(project, settings),
                    RestoreAuditProperties = auditProperties,
                };
            }
            else
            {
                restoreMetadata = new ProjectRestoreMetadata
                {
                    // CrossTargeting is on, even if the TargetFrameworks property has only 1 tfm.
                    CrossTargeting = (projectStyle == ProjectStyle.PackageReference) && (
                        projectsByTargetFramework.Count > 1 || !string.IsNullOrWhiteSpace(project.GetProperty("TargetFrameworks"))),
                    FallbackFolders = BuildTasksUtility.GetFallbackFolders(
                        project.GetProperty("MSBuildStartupDirectory"),
                        project.Directory,
                        project.SplitPropertyValueOrNull("RestoreFallbackFolders"),
                        project.SplitGlobalPropertyValueOrNull("RestoreFallbackFolders"),
                        innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectFallbackFolders"))),
                        innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectFallbackFoldersExcludes"))),
                        settings),
                    SkipContentFileWrite = IsLegacyProject(project),
                    ValidateRuntimeAssets = project.IsPropertyTrue("ValidateRuntimeIdentifierCompatibility"),
                    CentralPackageVersionsEnabled = isCentralPackageManagementEnabled && projectStyle == ProjectStyle.PackageReference,
                    CentralPackageFloatingVersionsEnabled = isCentralPackageFloatingVersionsEnabled,
                    CentralPackageVersionOverrideDisabled = isCentralPackageVersionOverrideDisabled,
                    CentralPackageTransitivePinningEnabled = isCentralPackageTransitivePinningEnabled,
                    RestoreAuditProperties = auditProperties
                };
            }

            restoreMetadata.CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(outputPath, project.FullPath);
            restoreMetadata.ConfigFilePaths = settings.GetConfigFilePaths();
            restoreMetadata.OutputPath = outputPath;
            targetFrameworkInfos.ForEach(tfi =>
                restoreMetadata.OriginalTargetFrameworks.Add(
                        !string.IsNullOrEmpty(tfi.TargetAlias) ?
                            tfi.TargetAlias :
                            tfi.FrameworkName.GetShortFolderName()));
            restoreMetadata.PackagesPath = GetPackagesPath(project, settings);
            restoreMetadata.ProjectName = projectName;
            restoreMetadata.ProjectPath = project.FullPath;
            restoreMetadata.ProjectStyle = projectStyle;
            restoreMetadata.ProjectUniqueName = project.FullPath;
            restoreMetadata.ProjectWideWarningProperties = WarningProperties.GetWarningProperties(project.GetProperty("TreatWarningsAsErrors"), project.GetProperty("WarningsAsErrors"), project.GetProperty("NoWarn"), project.GetProperty("WarningsNotAsErrors"));
            restoreMetadata.RestoreLockProperties = new RestoreLockProperties(project.GetProperty("RestorePackagesWithLockFile"), project.GetProperty("NuGetLockFilePath"), project.IsPropertyTrue("RestoreLockedMode"));
            restoreMetadata.Sources = GetSources(project, innerNodes, settings);
            restoreMetadata.TargetFrameworks = GetProjectRestoreMetadataFrameworkInfos(targetFrameworkInfos, projectsByTargetFramework);
            restoreMetadata.UsingMicrosoftNETSdk = MSBuildRestoreUtility.GetUsingMicrosoftNETSdk(project.GetProperty("UsingMicrosoftNETSdk"));
            restoreMetadata.SdkAnalysisLevel = MSBuildRestoreUtility.GetSdkAnalysisLevel(project.GetProperty("SdkAnalysisLevel"));
            restoreMetadata.UseLegacyDependencyResolver = project.IsPropertyTrue("RestoreUseLegacyDependencyResolver");

            return (restoreMetadata, targetFrameworkInfos);

            static (ProjectStyle, string packagesConfigPath) GetProjectStyle(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> tfms, Common.ILogger log)
            {
                ProjectStyle? projectStyleOrNull = BuildTasksUtility.GetProjectRestoreStyleFromProjectProperty(project.GetProperty("RestoreProjectStyle"));
                bool hasPackageReferenceItems = tfms.Values.Any(p => p.GetItems("PackageReference").Any());
                (ProjectStyle ProjectStyle, string PackagesConfigFilePath) projectStyleResult = BuildTasksUtility.GetProjectRestoreStyle(
                    restoreProjectStyle: projectStyleOrNull,
                    hasPackageReferenceItems: hasPackageReferenceItems,
                    projectJsonPath: project.GetProperty("_CurrentProjectJsonPath"),
                    projectDirectory: project.Directory,
                    projectName: project.GetProperty("MSBuildProjectName"),
                    log: log);

                return (projectStyleResult.ProjectStyle, projectStyleResult.PackagesConfigFilePath);
            }
        }

        private static HashSet<string> GetAuditSuppressions(IMSBuildProject project)
        {
            IEnumerable<string> suppressions = GetDistinctItemsOrEmpty(project, "NuGetAuditSuppress")
                                                    .Select(i => i.Identity);

            return suppressions?.Count() > 0 ? new HashSet<string>(suppressions) : null;
        }

        /// <summary>
        /// Recursively loads and evaluates MSBuild projects.
        /// </summary>
        /// <param name="entryProjects">An <see cref="IEnumerable{ProjectGraphEntryPoint}" /> containing the entry projects to load.</param>
        /// <param name="interactive"><see langword="true" /> if the build is allowed to interact with the user, otherwise <see langword="false" />.</param>
        /// <param name="binaryLoggerParameters">Optional parameters to use for the MSBuild binary log.</param>
        /// <param name="createProjectFactory">A factory method that creates a project adapter from an MSBuild ProjectInstance.</param>
        /// <param name="updateProjectFactory">A factory method that updates a project adapter with a target framework and MSBuild ProjectInstance.</param>
        /// <param name="projectFinalizeDelegate">An option delegate to finalize a project adapter once all projects have been evaluated.</param>
        /// <returns>An <see cref="ICollection{ProjectWithInnerNodes}" /> object containing projects and their inner nodes if they are targeting multiple frameworks.</returns>
        private ConcurrentDictionary<string, TProject> LoadProjects<TProject>(
            IEnumerable<ProjectGraphEntryPoint> entryProjects,
            bool interactive,
            string binaryLoggerParameters,
            Func<string, (ProjectInstance, string), TProject> createProjectFactory,
            Func<string, TProject, (ProjectInstance, string), TProject> updateProjectFactory,
            Action<TProject> projectFinalizeDelegate)
        {
            try
            {
                var loggers = new List<Microsoft.Build.Framework.ILogger>
                {
                    LoggingQueue
                };

                bool logTaskInputs = false;

                // Attach the binary logger if parameters were specified
                if (!string.IsNullOrWhiteSpace(binaryLoggerParameters))
                {
                    loggers.Add(new BinaryLogger
                    {
                        Parameters = Uri.UnescapeDataString(binaryLoggerParameters)
                    });

                    // Log task inputs when the binary logger is attached
                    logTaskInputs = true;
                }

                var projects = new ConcurrentDictionary<string, TProject>(PathUtility.GetStringComparerBasedOnOS());

                using var projectCollection = new ProjectCollection(
                    globalProperties: null,
                    // Attach a logger for evaluation only if the Debug option is set
                    loggers: loggers,
                    remoteLoggers: null,
                    toolsetDefinitionLocations: ToolsetDefinitionLocations.Default,
                    // Having more than 1 node spins up multiple msbuild.exe instances to run builds in parallel
                    // However, these targets complete so quickly that the added overhead makes it take longer
                    maxNodeCount: 1,
                    onlyLogCriticalEvents: false,
                    // Loading projects as readonly makes parsing a little faster since comments and whitespace can be ignored
                    loadProjectsReadOnly: true);

                Stopwatch sw = Stopwatch.StartNew();

                EvaluationContext evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

                // Create a ProjectGraph object and pass a factory method which creates a ProjectInstance
                ProjectGraph projectGraph = new ProjectGraph(entryProjects, projectCollection, (path, properties, collection) =>
                {
                    var projectOptions = new ProjectOptions
                    {
                        EvaluationContext = evaluationContext,
                        GlobalProperties = properties,
                        Interactive = interactive,
                        // Ignore bad imports to maximize the chances of being able to load the project and restore
                        LoadSettings = ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition,
                        ProjectCollection = collection
                    };

                    return ProjectInstance.FromFile(path, projectOptions);
                });

                int buildCount = 0;
                int failedBuildSubmissionCount = 0;

                var buildParameters = new BuildParameters(projectCollection)
                {
                    // Use the same loggers as the project collection
                    Loggers = projectCollection.Loggers,
                    LogTaskInputs = logTaskInputs
                };

                try
                {
                    // BeginBuild starts a queue which accepts build requests and applies the build parameters to all of them
                    BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                    // Loop through each project and run the targets.  There is no need for this to run in parallel since there is only
                    // one node in the process to run builds.
                    foreach (ProjectGraphNode projectGraphItem in projectGraph.ProjectNodes)
                    {
                        ProjectInstance projectInstance = projectGraphItem.ProjectInstance;

                        if (!projectInstance.Targets.ContainsKey("_IsProjectRestoreSupported") || projectInstance.GlobalProperties == null || projectInstance.GlobalProperties.TryGetValue("TargetFramework", out string targetFramework) && string.IsNullOrWhiteSpace(targetFramework))
                        {
                            // In rare cases, users can set an empty TargetFramework value in a project-to-project reference.  Static Graph will respect that
                            // but NuGet does not need to do anything with that instance of the project since the actual project is still loaded correctly
                            // with its actual TargetFramework.
                            var message = MSBuildRestoreUtility.GetMessageForUnsupportedProject(projectInstance.FullPath);
                            MSBuildLogger.Log(message);
                            continue;
                        }

                        // If the project supports restore, queue up a build of the targets needed for restore
                        BuildSubmission buildSubmission = BuildManager.DefaultBuildManager.PendBuildRequest(
                            new BuildRequestData(
                                projectInstance,
                                TargetsToBuild,
                                hostServices: null,
                                // Suppresses an error that a target does not exist because it may or may not contain the targets that we're running
                                BuildRequestDataFlags.SkipNonexistentTargets));

                        buildSubmission.ExecuteAsync((submission) =>
                        {
                            BuildResult result = submission.BuildResult;
                            if (result.OverallResult == BuildResultCode.Failure)
                            {
                                failedBuildSubmissionCount++;
                            }

                            buildCount++;

                            projects.AddOrUpdate(
                                projectInstance.FullPath,
                                createProjectFactory,
                                updateProjectFactory,
                                (projectInstance, targetFramework));
                        }, context: null);
                    }
                }
                finally
                {
                    // EndBuild blocks until all builds are complete
                    BuildManager.DefaultBuildManager.EndBuild();
                }

                sw.Stop();

                if (projectFinalizeDelegate is not null)
                {
                    foreach (var kvp in projects)
                    {
                        projectFinalizeDelegate(kvp.Value);
                    }
                }

                MSBuildLogger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.ProjectEvaluationSummary, projectGraph.ProjectNodes.Count, sw.ElapsedMilliseconds, buildCount, failedBuildSubmissionCount));

                if (failedBuildSubmissionCount != 0)
                {
                    // Return null if any builds failed, they will have logged errors
                    return null;
                }

                // Just return the projects not the whole dictionary as it was just used to group the projects together
                return projects;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                LogErrorFromException(e);

                return null;
            }
        }

        /// <summary>
        /// Returns the list of distinct items with the <paramref name="itemName"/> name.
        /// Two items are equal if they have the same <see cref="IMSBuildItem.Identity"/>.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="itemName">The item name.</param>
        /// <returns>Returns the list of items with the <paramref name="itemName"/>. If the item does not exist it will return an empty list.</returns>
        private static IEnumerable<IMSBuildItem> GetDistinctItemsOrEmpty(IMSBuildProject project, string itemName)
        {
            return project.GetItems(itemName)?.Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance) ?? Enumerable.Empty<IMSBuildItem>();
        }

        /// <summary>
        /// Logs an error from the specified exception.
        /// </summary>
        /// <param name="exception">The <see cref="Exception" /> with details to be logged.</param>
        private void LogErrorFromException(Exception exception)
        {
            switch (exception)
            {
                case AggregateException aggregateException:
                    foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
                    {
                        LogErrorFromException(innerException);
                    }
                    break;

                case InvalidProjectFileException invalidProjectFileException:
                    // Special case the InvalidProjectFileException since it has extra information about what project file couldn't be loaded
                    LoggingQueue.TaskLoggingHelper.LogError(
                        invalidProjectFileException.ErrorSubcategory,
                        invalidProjectFileException.ErrorCode,
                        invalidProjectFileException.HelpKeyword,
                        invalidProjectFileException.ProjectFile,
                        invalidProjectFileException.LineNumber,
                        invalidProjectFileException.ColumnNumber,
                        invalidProjectFileException.EndLineNumber,
                        invalidProjectFileException.EndColumnNumber,
                        invalidProjectFileException.Message);
                    break;

                default:
                    LoggingQueue.TaskLoggingHelper.LogErrorFromException(
                        exception,
                        showStackTrace: true);
                    break;
            }
        }

        /// <summary>
        /// Logs the list of files to embed in the MSBuild binary log.
        /// </summary>
        /// <param name="dependencyGraphSpec"></param>
        private void LogFilesToEmbedInBinlog(DependencyGraphSpec dependencyGraphSpec, IReadOnlyDictionary<string, string> options)
        {
            // Determines what the user wants embedded in the binary log where 0 or false disables embedding anything, 2 embeds everything, and 1 or true embeds just the assets file, g.props, and g.targets.
            options.TryGetValue(nameof(RestoreTaskEx.EmbedFilesInBinlog), out string embedFilesInBinlog);

            int embedInBinlogSelection = BuildTasksUtility.GetFilesToEmbedInBinlogValue(embedFilesInBinlog);

            if (embedInBinlogSelection == 0)
            {
                return;
            }

            IReadOnlyList<PackageSpec> projects = dependencyGraphSpec.Projects;

            foreach (PackageSpec project in projects)
            {
                if (project.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference)
                {
                    LoggingQueue.Enqueue(new ConsoleOutLogEmbedInBinlog(Path.Combine(project.RestoreMetadata.OutputPath, LockFileFormat.AssetsFileName)));
                    LoggingQueue.Enqueue(new ConsoleOutLogEmbedInBinlog(BuildAssetsUtils.GetMSBuildFilePathForPackageReferenceStyleProject(project, BuildAssetsUtils.PropsExtension)));
                    LoggingQueue.Enqueue(new ConsoleOutLogEmbedInBinlog(BuildAssetsUtils.GetMSBuildFilePathForPackageReferenceStyleProject(project, BuildAssetsUtils.TargetsExtension)));

                    // Only include the dgspec if the user wants everything embedded in the binlog.
                    if (embedInBinlogSelection == 2)
                    {
                        LoggingQueue.Enqueue(new ConsoleOutLogEmbedInBinlog(Path.Combine(project.RestoreMetadata.OutputPath, DependencyGraphSpec.GetDGSpecFileName(Path.GetFileName(project.RestoreMetadata.ProjectPath)))));
                    }
                }
                else if (project.RestoreMetadata.ProjectStyle == ProjectStyle.PackagesConfig)
                {
                    string packagesConfigPath = BuildTasksUtility.GetPackagesConfigFilePath(project.RestoreMetadata.ProjectPath);

                    if (packagesConfigPath != null)
                    {
                        LoggingQueue.Enqueue(new ConsoleOutLogEmbedInBinlog(packagesConfigPath));
                    }
                }
            }
        }
    }
}
