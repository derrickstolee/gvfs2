using GVFS.FunctionalTests.Properties;
using GVFS.FunctionalTests.Tools;
using GVFS.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GVFS.FunctionalTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Properties.Settings.Default.Initialize();
            Console.WriteLine("Settings.Default.CurrentDirectory: {0}", Settings.Default.CurrentDirectory);
            Console.WriteLine("Settings.Default.PathToGit: {0}", Settings.Default.PathToGit);
            Console.WriteLine("Settings.Default.PathToGVFS: {0}", Settings.Default.PathToGVFS);
            Console.WriteLine("Settings.Default.PathToGVFSService: {0}", Settings.Default.PathToGVFSService);

            NUnitRunner runner = new NUnitRunner(args);
            runner.AddGlobalSetupIfNeeded("GVFS.FunctionalTests.GlobalSetup");

            if (runner.HasCustomArg("--no-shared-gvfs-cache"))
            {
                Console.WriteLine("Running without a shared git object cache");
                GVFSTestConfig.NoSharedCache = true;
            }

            if (runner.HasCustomArg("--replace-inbox-projfs"))
            {
                Console.WriteLine("Tests will replace inbox ProjFS");
                GVFSTestConfig.ReplaceInboxProjFS = true;
            }

            GVFSTestConfig.LocalCacheRoot = runner.GetCustomArgWithParam("--shared-gvfs-cache-root");

            HashSet<string> includeCategories = new HashSet<string>();
            HashSet<string> excludeCategories = new HashSet<string>();

            if (runner.HasCustomArg("--full-suite"))
            {
                Console.WriteLine("Running the full suite of tests");

                List<object[]> modes = new List<object[]>();
                foreach (Settings.ValidateWorkingTreeMode mode in Enum.GetValues(typeof(Settings.ValidateWorkingTreeMode)))
                {
                    modes.Add(new object[] { mode });
                }

                GVFSTestConfig.GitRepoTestsValidateWorkTree = modes.ToArray();
                GVFSTestConfig.FileSystemRunners = FileSystemRunners.FileSystemRunner.AllWindowsRunners;
            }
            else
            {
                Settings.ValidateWorkingTreeMode validateMode = Settings.ValidateWorkingTreeMode.Full;

                if (runner.HasCustomArg("--sparse-mode"))
                {
                    validateMode = Settings.ValidateWorkingTreeMode.SparseMode;

                    // Only test the git commands in sparse mode for splitting out tests in builds
                    includeCategories.Add(Categories.GitCommands);
                }

                GVFSTestConfig.GitRepoTestsValidateWorkTree =
                    new object[]
                    {
                        new object[] { validateMode },
                    };

                if (runner.HasCustomArg("--extra-only"))
                {
                    Console.WriteLine("Running only the tests marked as ExtraCoverage");
                    includeCategories.Add(Categories.ExtraCoverage);
                }
                else
                {
                    excludeCategories.Add(Categories.ExtraCoverage);
                }

                GVFSTestConfig.FileSystemRunners = FileSystemRunners.FileSystemRunner.DefaultRunners;
            }

            GVFSTestConfig.DotGVFSRoot = ".gvfs";

            GVFSTestConfig.RepoToClone =
                runner.GetCustomArgWithParam("--repo-to-clone")
                ?? Properties.Settings.Default.RepoToClone;

            RunBeforeAnyTests();
            Environment.ExitCode = runner.RunTests(includeCategories, excludeCategories);

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Tests completed. Press Enter to exit.");
                Console.ReadLine();
            }
        }

        private static void RunBeforeAnyTests()
        {
            if (GVFSTestConfig.ReplaceInboxProjFS)
            {
                ProjFSFilterInstaller.ReplaceInboxProjFS();
            }

            GVFSServiceProcess.InstallService();

            string serviceProgramDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.Create),
                "GVFS",
                "ProgramData",
                "GVFS.Service");

            string statusCacheVersionTokenPath = Path.Combine(
                serviceProgramDataDir, "EnableGitStatusCacheToken.dat");

            if (!File.Exists(statusCacheVersionTokenPath))
            {
                Directory.CreateDirectory(serviceProgramDataDir);
                File.WriteAllText(statusCacheVersionTokenPath, string.Empty);
            }
        }
    }
}
