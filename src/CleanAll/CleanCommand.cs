using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using MinimalCli;
using ShellRunner;
using System.CommandLine;
using System.Runtime.InteropServices;

namespace CleanAll;

internal class CleanCommand
{
    [RootHandler]
    public static async Task<int> ExecuteAsync([Argument] string? path = null, bool dryRun = false)
    {
        try
        {
            // cannot specify current directory as an Argument.SetDefaultValue because it caches
            //   the value. Instead evaluate it when the command is ran during Execute().
            if(string.IsNullOrWhiteSpace(path))
                path = Environment.CurrentDirectory;

            path = Path.GetFullPath(path);

            string? directoryPath;

            bool isDirectoryPath = Directory.Exists(path);
            bool isDotnetPath = false;
            if(isDirectoryPath)
            {
                IEnumerable<string> possibleFiles = Directory.EnumerateFiles(path)
                    .Where(f => {
                        string ext = Path.GetExtension(f);
                        return ext == ".slnx" || ext == ".sln" || ext == ".csproj";
                    });

                if(possibleFiles.Count() == 1)
                {
                    string file = possibleFiles.Single();
                    path = file;
                }
                else
                {
                    ConsoleColorRed();
                    Console.WriteLine("No Solution file or Project file found in directory: {0}", path);
                    ResetConsoleColor();
                    return 1;
                }
            }
            
            if(File.Exists(path))
            {
                string ext = Path.GetExtension(path);
                if(ext != ".sln" && ext != ".slnx" && ext != ".csproj")
                {
                    ConsoleColorRed();
                    Console.Error.WriteLine("File is not a solution or project file.");
                    ResetConsoleColor();
                    return 1;
                }

                isDotnetPath = true;

                // get the directory path, otherwise path is already a directory
                directoryPath = Path.GetDirectoryName(path);
            }
            else
            {
                ConsoleColorRed();
                Console.Error.WriteLine("Could not find file: {0}", path);
                ResetConsoleColor();

                // exit because no directory to search
                return 1;
            }

            if(string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Invalid path");

            if(isDotnetPath)
            {
                string ext = Path.GetExtension(path);
                if(ext == ".sln" || ext == ".slnx")
                {
                    ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(path);

                    if(serializer is not null)
                    {
                        SolutionModel slnModel = await serializer.OpenAsync(path, CancellationToken.None);
                        string solutionDirectory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Solution Directory is invalid");

                        IReadOnlyList<string> platforms = slnModel.Platforms;
                        IReadOnlyList<string> buildTypes = slnModel.BuildTypes;

                        async Task<string> select(SolutionProjectModel project)
                        {
                            string fullProjectPath = Path.Combine(solutionDirectory, project.FilePath);

                            ProjectOutputPaths paths = await CleanProjectOutputPaths(fullProjectPath, dryRun);

                            if (dryRun)
                            {
                                ResetConsoleColor();
                            }

                            string? bin = paths.HasBin ? paths.BinPath : "";
                            string? obj = paths.HasObj ? paths.ObjPath : "";

                            string details = "";

                            // empty msbuild properties should not produce blank 'removed' entries.
                            if (!string.IsNullOrWhiteSpace(bin)) details += $"\n  Removed: {bin}";
                            if (!string.IsNullOrWhiteSpace(obj)) details += $"\n  Removed: {obj}";

                            return $"Cleaned {project.ActualDisplayName}{details}";
                        }

                        List<Task<string>> cleanAllTasks = slnModel.SolutionProjects
                            .Select(project => select(project))
                            .ToList();

                        while(cleanAllTasks.Count > 0)
                        {
                            Task<string> completedTask = await Task.WhenAny(cleanAllTasks);
                            cleanAllTasks.Remove(completedTask);

                            string consoleText = await completedTask;

                            if(dryRun)
                            {
                                Console.Write("Dry run: ");
                                ConsoleColorGray(); 
                            }

                            Console.WriteLine(consoleText);

                            if(dryRun)
                            {
                                ResetConsoleColor();
                            }
                        }
                    }

                    return 0;
                }
                else if(ext == ".csproj")
                {
                    if (dryRun)
                    {
                        Console.Write("Dry run: ");
                        ConsoleColorGray();
                    }

                    Console.WriteLine("Cleaning {0}...", Path.GetFileName(path));

                    ProjectOutputPaths outputPaths = await CleanProjectOutputPaths(path, dryRun);

                    // empty msbuild properties should not produce a blank 'removed' entry.
                    if (!string.IsNullOrWhiteSpace(outputPaths.Bin))
                        Console.WriteLine("  Removed: {0}", outputPaths.Bin.Contains("..") ? outputPaths.FullBin : outputPaths.Bin);
                    if (!string.IsNullOrWhiteSpace(outputPaths.Obj))
                        Console.WriteLine("  Removed: {0}", outputPaths.Obj.Contains("..") ? outputPaths.FullObj : outputPaths.Obj);

                    if (dryRun)
                    {
                        ResetConsoleColor();
                    }

                    return 0;
                }
                else
                {
                    ConsoleColorRed();
                    Console.WriteLine("Extension type:{0} not supported", ext);
                    ResetConsoleColor();
                    return 1;
                }
            }
            else
            { 
                return 1;
            }
        }
        catch (Exception ex)
        {
            ConsoleColorRed();
            Console.Error.WriteLine(ex.Message);
            ResetConsoleColor();
            return 1;
        }
    }

    static async Task<ProjectOutputPaths> CleanProjectOutputPaths(string fullProjectpath, bool dryRun)
    {
        string ext = Path.GetExtension(fullProjectpath);
        if (!File.Exists(fullProjectpath) || ext != ".csproj")
            throw new ArgumentException("A project directory provided was not accurate.");
        
        string fullProjectDirectoryPath = Path.GetDirectoryName(fullProjectpath) ?? throw new ArgumentException("project path is invalid");

        ShellRunner.CommandBuilder cmd = await CommandRunner
            .UsePowershell()
            .StartProcess()
            // BaseOutputPath & BaseIntermediateOutputPath are the correct properties for modern apps,
            //   legacy apps fall back to OutputPath & IntermediateOutputPath
            .AddCommand($"dotnet msbuild \"{fullProjectpath}\" -getProperty:BaseOutputPath", key: "binPath")
            .AddCommand($"dotnet msbuild \"{fullProjectpath}\" -getProperty:OutputPath", key: "outputPath")
            .AddCommand($"dotnet msbuild \"{fullProjectpath}\" -getProperty:BaseIntermediateOutputPath", key: "objPath")
            .AddCommand($"dotnet msbuild \"{fullProjectpath}\" -getProperty:IntermediateOutputPath", key: "intermediateOutputPath")
            .RunAsync();

        string? GetPropertyValue(string key)
        {
            IReadOnlyList<ShellRunner.Output> output = cmd.GetCommand(key).Output;
            return output.Count == 0 ? null : output[output.Count - 1].Data;
        }

        string? baseBinPath = GetPropertyValue("binPath");
        string? baseObjPath = GetPropertyValue("objPath");

        // legacy projects use these instead of the Base* properties
        string? binPath = string.IsNullOrWhiteSpace(baseBinPath) ? GetPropertyValue("outputPath") : baseBinPath;
        string? objPath = string.IsNullOrWhiteSpace(baseObjPath) ? GetPropertyValue("intermediateOutputPath") : baseObjPath;

        // do not combine an empty property with the project directory or it could delete the whole project
        string? ResolveOutputPath(string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                return null;

            string fullOutputPath = Path.GetFullPath(Path.Combine(fullProjectDirectoryPath, outputPath));
            string fullProjectDirectory = Path.GetFullPath(fullProjectDirectoryPath);

            string trimmedOutput = Path.TrimEndingDirectorySeparator(fullOutputPath);
            string trimmedProject = Path.TrimEndingDirectorySeparator(fullProjectDirectory);
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            
            // a configured output path must never resolve to the project directory itself
            return trimmedOutput.Equals(trimmedProject, pathComparison)
                ? null
                : fullOutputPath;
        }

        string? fullBin = ResolveOutputPath(binPath);
        string? fullObj = ResolveOutputPath(objPath);

        if (fullBin is null || !Directory.Exists(fullBin))
        {
            binPath = null;
            fullBin = null;
        }
        if (fullObj is null || !Directory.Exists(fullObj))
        {
            objPath = null;
            fullObj = null;
        }

        if (!dryRun && fullBin is not null)
        {
            Directory.Delete(fullBin, recursive: true);
        }

        if (!dryRun && fullObj is not null)
        {
            Directory.Delete(fullObj, recursive: true);
        }

        return new ProjectOutputPaths(binPath, objPath, fullBin, fullObj);
    }

    static void ConsoleColorGray()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.ForegroundColor = ConsoleColor.DarkGray;
    }
    static void ConsoleColorRed()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.ForegroundColor = ConsoleColor.Red;
    }
    static void ResetConsoleColor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.ResetColor();
    }
}
