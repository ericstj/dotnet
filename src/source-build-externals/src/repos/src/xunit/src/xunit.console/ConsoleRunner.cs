using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xunit.ConsoleClient
{
    class ConsoleRunner
    {
        volatile bool cancel;
        CommandLine commandLine;
        readonly object consoleLock;
        readonly ConcurrentDictionary<string, ExecutionSummary> completionMessages = new ConcurrentDictionary<string, ExecutionSummary>();
        bool failed;
        IRunnerLogger logger;
        IMessageSinkWithTypes reporterMessageHandler;

        public ConsoleRunner(object consoleLock)
        {
            this.consoleLock = consoleLock;
        }

        public int EntryPoint(string[] args)
        {
            commandLine = CommandLine.Parse(args);

            if (commandLine.UseAnsiColor)
                ConsoleHelper.UseAnsiColor();

            try
            {
                var reporters = GetAvailableRunnerReporters();

                if (args.Length == 0 || args[0] == "-?" || args[0] == "/?" || args[0] == "-h" || args[0] == "--help")
                {
                    PrintHeader();
                    PrintUsage(reporters);
                    return 2;
                }

                if (commandLine.Project.Assemblies.Count == 0)
                    throw new ArgumentException("must specify at least one assembly");

#if NETFRAMEWORK
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
#endif

                Console.CancelKeyPress += (sender, e) =>
                {
                    if (!cancel)
                    {
                        Console.WriteLine("Canceling... (Press Ctrl+C again to terminate)");
                        cancel = true;
                        e.Cancel = true;
                    }
                };

                var defaultDirectory = Directory.GetCurrentDirectory();
                if (!defaultDirectory.EndsWith(new string(new[] { Path.DirectorySeparatorChar }), StringComparison.Ordinal))
                    defaultDirectory += Path.DirectorySeparatorChar;

                var reporter = commandLine.ChooseReporter(reporters);

#if DEBUG
                if (commandLine.Pause)
                {
                    Console.Write("Press any key to start execution...");
                    Console.ReadKey(true);
                    Console.WriteLine();
                }
#endif

                if (commandLine.Debug)
                    Debugger.Launch();

                logger = new ConsoleRunnerLogger(!commandLine.NoColor, commandLine.UseAnsiColor, consoleLock);
                reporterMessageHandler = MessageSinkWithTypesAdapter.Wrap(reporter.CreateMessageHandler(logger));

                if (!commandLine.NoLogo)
                    PrintHeader();

                var failCount = RunProject(commandLine.Project, commandLine.Serialize, commandLine.ParallelizeAssemblies,
                                           commandLine.ParallelizeTestCollections, commandLine.MaxParallelThreads,
                                           commandLine.DiagnosticMessages, commandLine.NoColor, commandLine.AppDomains,
                                           commandLine.FailSkips, commandLine.StopOnFail, commandLine.InternalDiagnosticMessages,
                                           commandLine.ParallelAlgorithm, commandLine.ShowLiveOutput);

                if (cancel)
                    return -1073741510;    // 0xC000013A: The application terminated as a result of a CTRL+C

                if (commandLine.Wait)
                {
                    Console.WriteLine();
                    Console.Write("Press any key to continue...");
                    Console.ReadKey();
                    Console.WriteLine();
                }

                return failCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                if (!commandLine.NoColor)
                    ConsoleHelper.SetForegroundColor(ConsoleColor.Red);

                Console.WriteLine("error: {0}", ex.Message);

                if (commandLine.InternalDiagnosticMessages)
                {
                    if (!commandLine.NoColor)
                        ConsoleHelper.SetForegroundColor(ConsoleColor.DarkGray);

                    Console.WriteLine(ex.StackTrace);
                }

                return ex is ArgumentException ? 3 : 4;
            }
            finally
            {
                if (!commandLine.NoColor)
                    ConsoleHelper.ResetColor();
            }
        }

        List<IRunnerReporter> GetAvailableRunnerReporters()
        {
            var result = RunnerReporterUtility.GetAvailableRunnerReporters(Path.GetDirectoryName(typeof(Program).GetTypeInfo().Assembly.GetLocalCodeBase()), out var messages);

            if (messages.Count > 0)
                lock (consoleLock)
                {
                    if (!commandLine.NoColor)
                        ConsoleHelper.SetForegroundColor(ConsoleColor.Yellow);

                    foreach (var message in messages)
                    {
                        Console.WriteLine(message);
                        Console.WriteLine();
                    }

                    if (!commandLine.NoColor)
                        ConsoleHelper.ResetColor();
                }

            return result;
        }

#if NETFRAMEWORK
        void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                Console.WriteLine(ex.ToString());
            else
                Console.WriteLine("Error of unknown type thrown in application domain");

            Environment.Exit(1);
        }
#endif

        void PrintHeader()
        {
#if NET452
            var platform = ".NET Framework 4.5.2, runtime: " + Environment.Version;
#elif NET46
            var platform = ".NET Framework 4.6, runtime: " + Environment.Version;
#elif NET461
            var platform = ".NET Framework 4.6.1, runtime: " + Environment.Version;
#elif NET462
            var platform = ".NET Framework 4.6.2, runtime: " + Environment.Version;
#elif NET47
            var platform = ".NET Framework 4.7, runtime: " + Environment.Version;
#elif NET471
            var platform = ".NET Framework 4.7.1, runtime: " + Environment.Version;
#elif NET472
            var platform = ".NET Framework 4.7.2, runtime: " + Environment.Version;
#elif NET48
            var platform = ".NET Framework 4.8, runtime: " + Environment.Version;
#elif NET481
            var platform = ".NET Framework 4.8.1, runtime: " + Environment.Version;
#elif NETCOREAPP
            var platform = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#else
#error Unknown target platform
#endif
            var versionAttribute = typeof(ConsoleRunner).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            Console.WriteLine("xUnit.net Console Runner v{0} ({1}-bit {2})", versionAttribute.InformationalVersion, IntPtr.Size * 8, platform);
        }

        void PrintUsage(IReadOnlyList<IRunnerReporter> reporters)
        {
#if NETFRAMEWORK
            var executableName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().GetLocalCodeBase());
#else
            var executableName = "dotnet xunit";
#endif

            Console.WriteLine("Copyright (C) .NET Foundation.");
            Console.WriteLine();
            Console.WriteLine("usage: {0} <assemblyFile> [configFile] [assemblyFile [configFile]...] [options] [reporter] [resultFormat filename [...]]", executableName);
            Console.WriteLine();
#if NETFRAMEWORK
            Console.WriteLine("Note: Configuration files must end in .json (for JSON) or .config (for XML)");
#else
            Console.WriteLine("Note: Configuration files must end in .json (XML is not supported on .NET Core)");
#endif
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine("  -nologo                   : do not show the copyright message");
            Console.WriteLine("  -nocolor                  : do not output results with colors");
            Console.WriteLine("  -failskips                : convert skipped tests into failures");
            Console.WriteLine("  -stoponfail               : stop on first test failure");
            Console.WriteLine("  -parallel option          : set parallelization based on option");
            Console.WriteLine("                            :   none        - turn off all parallelization");
            Console.WriteLine("                            :   collections - only parallelize collections");
            Console.WriteLine("                            :   assemblies  - only parallelize assemblies");
            Console.WriteLine("                            :   all         - parallelize assemblies & collections");
            Console.WriteLine("  -parallelalgorithm option : set the parallelization algoritm");
            Console.WriteLine("                            :   conservative - start the minimum number of tests (default)");
            Console.WriteLine("                            :   aggressive   - start as many tests as possible");
            Console.WriteLine("  -maxthreads count         : maximum thread count for collection parallelization");
            Console.WriteLine("                            :   default   - run with default (1 thread per CPU thread)");
            Console.WriteLine("                            :   unlimited - run with unbounded thread count");
            Console.WriteLine("                            :   (integer) - use exactly this many threads (e.g., '2' = 2 threads)");
            Console.WriteLine("                            :   (float)x  - use a multiple of CPU threads (e.g., '2.0x' = 2.0 * the number of CPU threads)");
#if NETFRAMEWORK
            Console.WriteLine("  -appdomains mode          : choose an app domain mode");
            Console.WriteLine("                            :   ifavailable - choose based on library type");
            Console.WriteLine("                            :   required    - force app domains on");
            Console.WriteLine("                            :   denied      - force app domains off");
            Console.WriteLine("  -noshadow                 : do not shadow copy assemblies");
#endif
            Console.WriteLine("  -wait                     : wait for input after completion");
            Console.WriteLine("  -diagnostics              : enable diagnostics messages for all test assemblies");
            Console.WriteLine("  -internaldiagnostics      : enable internal diagnostics messages for all test assemblies");
            Console.WriteLine("  -showliveoutput           : show output messages from tests live");
#if DEBUG
            Console.WriteLine("  -pause                    : pause before doing any work, to help attach a debugger");
#endif
            Console.WriteLine("  -debug                    : launch the debugger to debug the tests");
            Console.WriteLine("  -useansicolor             : force using ANSI color output on Windows (non-Windows always uses ANSI colors)");
            Console.WriteLine("  -serialize                : serialize all test cases (for diagnostic purposes only)");
            Console.WriteLine("  -trait \"name=value\"       : only run tests with matching name/value traits");
            Console.WriteLine("                            : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -notrait \"name=value\"     : do not run tests with matching name/value traits");
            Console.WriteLine("                            : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -method \"name\"            : run a given test method (can be fully specified or use a wildcard;");
            Console.WriteLine("                            : i.e., 'MyNamespace.MyClass.MyTestMethod' or '*.MyTestMethod')");
            Console.WriteLine("                            : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -nomethod \"name\"          : do not run a given test method (can be fully specified or use a wildcard;");
            Console.WriteLine("                            : i.e., 'MyNamespace.MyClass.MyTestMethod' or '*.MyTestMethod')");
            Console.WriteLine("                            : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -class \"name\"             : run all methods in a given test class (should be fully");
            Console.WriteLine("                            : specified; i.e., 'MyNamespace.MyClass')");
            Console.WriteLine("                            : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -noclass \"name\"           : do not run any methods in a given test class (should be fully");
            Console.WriteLine("                            : specified; i.e., 'MyNamespace.MyClass')");
            Console.WriteLine("                            : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -namespace \"name\"         : run all methods in a given namespace (i.e.,");
            Console.WriteLine("                            : 'MyNamespace.MySubNamespace')");
            Console.WriteLine("                            : if specified more than once, acts as an OR operation");
            Console.WriteLine("  -nonamespace \"name\"       : do not run any methods in a given namespace (i.e.,");
            Console.WriteLine("                            : 'MyNamespace.MySubNamespace')");
            Console.WriteLine("                            : if specified more than once, acts as an AND operation");
            Console.WriteLine("  -noautoreporters          : do not allow reporters to be auto-enabled by environment");
            Console.WriteLine("                            : (for example, auto-detecting TeamCity or AppVeyor)");
#if NETCOREAPP
            Console.WriteLine("  -framework \"name\"         : set the target framework");
#endif
            Console.WriteLine();

            var switchableReporters = reporters.Where(r => !string.IsNullOrWhiteSpace(r.RunnerSwitch)).ToList();
            if (switchableReporters.Count > 0)
            {
                Console.WriteLine("Reporters: (optional, choose only one)");

                foreach (var reporter in switchableReporters.OrderBy(r => r.RunnerSwitch))
                    Console.WriteLine("  -{0} : {1}", reporter.RunnerSwitch.ToLowerInvariant().PadRight(24), reporter.Description);

                Console.WriteLine();
            }

            Console.WriteLine("Result formats: (optional, choose one or more)");
            TransformFactory.AvailableTransforms.ForEach(
                transform => Console.WriteLine("  -{0} : {1}", string.Format(CultureInfo.CurrentCulture, "{0} <filename>", transform.CommandLine).PadRight(24).Substring(0, 24), transform.Description)
            );
        }

        int RunProject(XunitProject project,
                       bool serialize,
                       bool? parallelizeAssemblies,
                       bool? parallelizeTestCollections,
                       int? maxThreadCount,
                       bool diagnosticMessages,
                       bool noColor,
                       AppDomainSupport? appDomains,
                       bool failSkips,
                       bool stopOnFail,
                       bool internalDiagnosticMessages,
                       ParallelAlgorithm? parallelAlgorithm,
                       bool showLiveOutput)
        {
            XElement assembliesElement = null;
            var clockTime = Stopwatch.StartNew();
            var xmlTransformers = TransformFactory.GetXmlTransformers(project);
            var needsXml = xmlTransformers.Count > 0;

            if (!parallelizeAssemblies.HasValue)
                parallelizeAssemblies = project.All(assembly => assembly.Configuration.ParallelizeAssemblyOrDefault);

            if (needsXml)
                assembliesElement = new XElement("assemblies");

            var originalWorkingFolder = Directory.GetCurrentDirectory();

            if (parallelizeAssemblies.GetValueOrDefault())
            {
                var tasks = project.Assemblies.Select(assembly => Task.Run(() => ExecuteAssembly(consoleLock, assembly, serialize, needsXml, parallelizeTestCollections, maxThreadCount, diagnosticMessages, noColor, appDomains, failSkips, stopOnFail, project.Filters, internalDiagnosticMessages, parallelAlgorithm, showLiveOutput)));
                var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
                foreach (var assemblyElement in results.Where(result => result != null))
                    assembliesElement.Add(assemblyElement);
            }
            else
            {
                foreach (var assembly in project.Assemblies)
                {
                    var assemblyElement = ExecuteAssembly(consoleLock, assembly, serialize, needsXml, parallelizeTestCollections, maxThreadCount, diagnosticMessages, noColor, appDomains, failSkips, stopOnFail, project.Filters, internalDiagnosticMessages, parallelAlgorithm, showLiveOutput);
                    if (assemblyElement != null)
                        assembliesElement.Add(assemblyElement);
                }
            }

            clockTime.Stop();

            if (assembliesElement != null)
                assembliesElement.Add(new XAttribute("timestamp", DateTime.Now.ToString(CultureInfo.InvariantCulture)));

            if (completionMessages.Count > 0)
                reporterMessageHandler.OnMessage(new TestExecutionSummary(clockTime.Elapsed, completionMessages.OrderBy(kvp => kvp.Key).ToList()));

            Directory.SetCurrentDirectory(originalWorkingFolder);

            xmlTransformers.ForEach(transformer => transformer(assembliesElement));

            return failed ? 1 : completionMessages.Values.Sum(summary => summary.Failed + summary.Errors);
        }

        XElement ExecuteAssembly(object consoleLock,
                                 XunitProjectAssembly assembly,
                                 bool serialize,
                                 bool needsXml,
                                 bool? parallelizeTestCollections,
                                 int? maxThreadCount,
                                 bool diagnosticMessages,
                                 bool noColor,
                                 AppDomainSupport? appDomains,
                                 bool failSkips,
                                 bool stopOnFail,
                                 XunitFilters filters,
                                 bool internalDiagnosticMessages,
                                 ParallelAlgorithm? parallelAlgorithm,
                                 bool showLiveOutput)
        {
            foreach (var warning in assembly.ConfigWarnings)
                logger.LogWarning(warning);

            if (cancel)
                return null;

            failSkips = failSkips || assembly.Configuration.FailSkipsOrDefault;

            var assemblyElement = needsXml ? new XElement("assembly") : null;

            try
            {
                if (!ValidateFileExists(consoleLock, assembly.AssemblyFilename) || !ValidateFileExists(consoleLock, assembly.ConfigFilename))
                    return null;

                // Turn off pre-enumeration of theories, since there is no theory selection UI in this runner
                assembly.Configuration.PreEnumerateTheories = false;
                assembly.Configuration.DiagnosticMessages |= diagnosticMessages;
                assembly.Configuration.InternalDiagnosticMessages |= internalDiagnosticMessages;

                if (appDomains.HasValue)
                    assembly.Configuration.AppDomain = appDomains;

                // Setup discovery and execution options with command-line overrides
                var discoveryOptions = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
                var executionOptions = TestFrameworkOptions.ForExecution(assembly.Configuration);
                if (maxThreadCount.HasValue)
                    executionOptions.SetMaxParallelThreads(maxThreadCount);
                if (parallelizeTestCollections.HasValue)
                    executionOptions.SetDisableParallelization(!parallelizeTestCollections.GetValueOrDefault());
                if (showLiveOutput)
                    executionOptions.SetShowLiveOutput(showLiveOutput);
                if (stopOnFail)
                    executionOptions.SetStopOnTestFail(stopOnFail);
                if (parallelAlgorithm.HasValue)
                    executionOptions.SetParallelAlgorithm(parallelAlgorithm);

                var assemblyDisplayName = Path.GetFileNameWithoutExtension(assembly.AssemblyFilename);
                var diagnosticMessageSink = DiagnosticMessageSink.ForDiagnostics(consoleLock, assemblyDisplayName, assembly.Configuration.DiagnosticMessagesOrDefault, noColor);
                var internalDiagnosticsMessageSink = DiagnosticMessageSink.ForInternalDiagnostics(consoleLock, assemblyDisplayName, assembly.Configuration.InternalDiagnosticMessagesOrDefault, noColor);
                var appDomainSupport = assembly.Configuration.AppDomainOrDefault;
                var shadowCopy = assembly.Configuration.ShadowCopyOrDefault;
                var longRunningSeconds = assembly.Configuration.LongRunningTestSecondsOrDefault;

                using (AssemblyHelper.SubscribeResolveForAssembly(assembly.AssemblyFilename, internalDiagnosticsMessageSink))
                using (var controller = new XunitFrontController(appDomainSupport, assembly.AssemblyFilename, assembly.ConfigFilename, shadowCopy, diagnosticMessageSink: diagnosticMessageSink))
                using (var discoverySink = new TestDiscoverySink(() => cancel))
                {
                    // Discover & filter the tests
                    reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryStarting(assembly, controller.CanUseAppDomains && appDomainSupport != AppDomainSupport.Denied, shadowCopy, discoveryOptions));

                    controller.Find(false, discoverySink, discoveryOptions);
                    discoverySink.Finished.WaitOne();

                    var testCasesDiscovered = discoverySink.TestCases.Count;
                    var filteredTestCases = discoverySink.TestCases.Where(filters.Filter).ToList();
                    var testCasesToRun = filteredTestCases.Count;

                    reporterMessageHandler.OnMessage(new TestAssemblyDiscoveryFinished(assembly, discoveryOptions, testCasesDiscovered, testCasesToRun));

                    // Run the filtered tests
                    if (testCasesToRun == 0)
                        completionMessages.TryAdd(Path.GetFileName(assembly.AssemblyFilename), new ExecutionSummary());
                    else
                    {
                        if (serialize)
                            filteredTestCases = filteredTestCases.Select(controller.Serialize).Select(controller.Deserialize).ToList();

                        reporterMessageHandler.OnMessage(new TestAssemblyExecutionStarting(assembly, executionOptions));

                        var resultsOptions = new ExecutionSinkOptions
                        {
                            AssemblyElement = assemblyElement,
                            CancelThunk = () => cancel,
                            FinishedCallback = summary => completionMessages.TryAdd(assemblyDisplayName, summary),
                            DiagnosticMessageSink = diagnosticMessageSink,
                            FailSkips = failSkips,
                            LongRunningTestTime = TimeSpan.FromSeconds(longRunningSeconds),
                        };
                        var resultsSink = new ExecutionSink(reporterMessageHandler, resultsOptions);

                        controller.RunTests(filteredTestCases, resultsSink, executionOptions);
                        resultsSink.Finished.WaitOne();

                        reporterMessageHandler.OnMessage(new TestAssemblyExecutionFinished(assembly, executionOptions, resultsSink.ExecutionSummary));
                        if ((resultsSink.ExecutionSummary.Failed != 0 || resultsSink.ExecutionSummary.Errors != 0) && executionOptions.GetStopOnTestFailOrDefault())
                        {
                            Console.WriteLine("Canceling due to test failure...");
                            cancel = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failed = true;

                var e = ex;
                while (e != null)
                {
                    Console.WriteLine("{0}: {1}", e.GetType().FullName, e.Message);

                    if (internalDiagnosticMessages)
                        Console.WriteLine(e.StackTrace);

                    e = e.InnerException;
                }
            }

            return assemblyElement;
        }

        bool ValidateFileExists(object consoleLock, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || File.Exists(fileName))
                return true;

            lock (consoleLock)
            {
                ConsoleHelper.SetForegroundColor(ConsoleColor.Red);
                Console.WriteLine("File not found: {0}", fileName);
                ConsoleHelper.SetForegroundColor(ConsoleColor.Gray);
            }

            return false;
        }
    }
}
