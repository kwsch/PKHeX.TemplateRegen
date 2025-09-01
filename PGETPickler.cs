using System.Diagnostics;

namespace PKHeX.TemplateRegen;

public class PGETPickler(string PathPKHeXLegality, string PathRepoPGET)
{
    public void Update()
    {
        var exe = PathRepoPGET;
        if (!File.Exists(exe))
        {
            // find the first file with exe extension in the folder
            exe = Directory.EnumerateFiles(PathRepoPGET, "*.exe", SearchOption.AllDirectories).FirstOrDefault(z => z.Contains("WinForms"));
            if (exe is null)
            {
                LogUtil.Log("PGET executable not found");
                return;
            }
        }

        if (!RepoUpdater.UpdateRepo("pget", PathRepoPGET, "main"))
            return;

        // Build the repository after updating
        if (!BuildRepo(PathRepoPGET))
        {
            LogUtil.Log("Failed to build PGET repo with msbuild");
            return;
        }

        if (!GetIsModified(exe, out var date))
        {
            LogUtil.Log("PGET executable is not recently built. Build failure?");
            return;
        }

        // start the executable with --update passed as arg
        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = "--update",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            LogUtil.Log($"Failed to start {exe} executable");
            return;
        }
        process.WaitForExit();

        // Get all the created .pkl files then copy them to the destination folder
        var dest = Path.Combine(PathPKHeXLegality, "wild");
        var files = Directory.EnumerateFiles(Path.GetDirectoryName(exe) ?? string.Empty, "*.pkl", SearchOption.AllDirectories);
        int ctr = 0;
        foreach (var file in files)
        {
            var filename = Path.GetFileName(file);
            var destFile = Path.Combine(dest, filename);
            File.Copy(file, destFile, true);
            LogUtil.Log($"Copied {filename} to {dest}");
            ctr++;
        }
        LogUtil.Log($"Copied {ctr} files to {dest}");
    }

    private static bool GetIsModified(string exe, out DateTime date)
    {
        date = File.GetLastWriteTime(exe);
        return date.AddMinutes(1) >= DateTime.Now;
    }

    private static bool BuildRepo(string repoPath)
    {
        try
        {
            // Prefer solution file, else fall back to first project file.
            var sln = Directory.EnumerateFiles(repoPath, "*.sln", SearchOption.AllDirectories).FirstOrDefault();
            var target = sln ?? Directory.EnumerateFiles(repoPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
            if (target is null)
            {
                LogUtil.Log("No solution or project file found to build.");
                return false;
            }

            // Assume msbuild is on PATH. If not, fall back to dotnet build.
            string tool = "msbuild"; // Windows environments
            bool useDotNet = false;
            try
            {
                var whereProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "msbuild",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                whereProc?.WaitForExit(3000);
                if (whereProc?.ExitCode != 0)
                    useDotNet = true;
            }
            catch
            {
                useDotNet = true;
            }
            if (useDotNet)
                tool = "dotnet";

            var args = useDotNet
                ? $"build \"{target}\" --configuration Debug --verbosity minimal"
                : $"\"{target}\" /t:Rebuild /p:Configuration=Debug /verbosity:minimal";

            LogUtil.Log($"Building {target} using {(useDotNet ? "dotnet" : "msbuild")}...");
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(target) ?? repoPath,
            };
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                LogUtil.Log("Failed to start build process.");
                return false;
            }
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                LogUtil.Log($"Build failed with exit code {proc.ExitCode}.");
                return false;
            }
            LogUtil.Log("Build succeeded.");
            return true;
        }
        catch (Exception ex)
        {
            LogUtil.Log($"Build error: {ex.Message}");
            return false;
        }
    }
}
