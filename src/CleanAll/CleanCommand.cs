using System.CommandLine;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;
using ShellRunner;

namespace CleanAll;

internal class CleanCommand : RootCommand
{
    public CleanCommand() : base("CleanAll")
    {
        this.Description = "Delete all of the bin and obj directories and their contents in a solution or project folder.";
        this.AddAlias("CleanAll");

        Argument<string?> pathArgument = new("Solution|Project");
        pathArgument.Description = "Path to the solution file, project file or directory to clean.";
        pathArgument.SetDefaultValue(null);

        Option<bool> dryRunOption = new("--dry-run");
        dryRunOption.AddAlias("-dr");
        dryRunOption.Description = "Run the command without removing anything so you can see what will be deleted.";
        dryRunOption.SetDefaultValue(false);

        // add all the arguments and options for the command
        this.AddArgument(pathArgument);
        this.AddOption(dryRunOption);

        // set the handler
        this.SetHandler(async (context) => {
            string? path = context.ParseResult.GetValueForArgument(pathArgument);
            bool dryRun = context.ParseResult.GetValueForOption(dryRunOption);

            context.ExitCode = await ExecuteAsync(path, dryRun);
        });
    }

    static async Task<int> ExecuteAsync(string? path, bool dryRun)
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
                directoryPath = path;
            }
            else if(File.Exists(path))
            {
                var ext = Path.GetExtension(path);
                if(ext != ".sln" && ext != ".csproj")
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
                Console.Error.WriteLine("Could not find file or directory path.");
                ResetConsoleColor();

                // exit because no directory to search
                return 1;
            }

            if(string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Invalid path");

            if(isDotnetPath)
            {
                string ext = Path.GetExtension(path);
                if(ext == ".sln")
                {
                    ISolutionSerializer? serializer = SolutionSerializers.GetSerializerByMoniker(path);

                    if(serializer is not null)
                    {
                        SolutionModel slnModel = await serializer.OpenAsync(path, CancellationToken.None);
                        string solutionDirectory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Solution Directory is invalid");

                        var platforms = slnModel.Platforms;
                        var buildTypes = slnModel.BuildTypes;

                        foreach(SolutionProjectModel project in slnModel.SolutionProjects)
                        {

                            if (dryRun)
                            {
                                Console.Write("Dry run: ");
                                ConsoleColorGray();
                            }
                            Console.WriteLine("Cleaning {0}...", project.ActualDisplayName);

                            var fullProjectPath = Path.Combine(solutionDirectory, project.FilePath);

                            await CleanProjectOutputPaths(fullProjectPath, dryRun);

                            if (dryRun)
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

                    await CleanProjectOutputPaths(path, dryRun);

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
                IEnumerable<string> allDeletableDirectories = Directory.EnumerateDirectories(directoryPath, "bin",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        MatchCasing = MatchCasing.CaseInsensitive,
                        ReturnSpecialDirectories = false
                    })
                    .Where(directory => directory.EndsWith("\\bin") || directory.EndsWith("/bin"))
                    .Concat(
                    Directory.EnumerateDirectories(directoryPath, "obj",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            MatchCasing = MatchCasing.CaseInsensitive,
                            ReturnSpecialDirectories = false
                        })
                        .Where(directory => directory.EndsWith("\\obj") || directory.EndsWith("/obj"))
                    );

                if (allDeletableDirectories.Any() == false)
                {
                    // this is not an error
                    Console.WriteLine("No bin/ or obj/ directories found to delete.");
                    return 0;
                }

                foreach (var binObj in allDeletableDirectories)
                {
                    try
                    {
                        if (dryRun)
                        {
                            Console.Write("Dry run: ");
                            ConsoleColorGray();
                        }

                        Console.WriteLine($"Deleting {binObj}");

                        if (dryRun)
                        {
                            ResetConsoleColor();
                        }

                        if (!dryRun)
                        {
                            Directory.Delete(binObj, recursive: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleColorRed();
                        Console.Error.WriteLine(ex.Message);
                        ResetConsoleColor();
                    }
                }

                return 0;
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

    static async Task CleanProjectOutputPaths(string fullProjectpath, bool dryRun)
    {
        var ext = Path.GetExtension(fullProjectpath);
        if (!File.Exists(fullProjectpath) || ext != ".csproj")
            throw new ArgumentException("A project directory provided was not accurate.");
        
        string fullProjectDirectoryPath = Path.GetDirectoryName(fullProjectpath) ?? throw new ArgumentException("project path is invalid");

        CommandBuilder cmd = await CommandRunner
            .UsePowershell()
            .StartProcess()
            .AddCommand($"dotnet msbuild {fullProjectpath} -getProperty:BaseOutputPath", key: "binPath")
            .AddCommand($"dotnet msbuild {fullProjectpath} -getProperty:BaseIntermediateOutputPath", key: "objPath")
            .RunAsync();

        string binPath = cmd.GetCommand("binPath").Output[1].Data;
        string objPath = cmd.GetCommand("objPath").Output[1].Data;

        string fullBin = Path.Combine(fullProjectDirectoryPath, binPath);
        string fullObj = Path.Combine(fullProjectDirectoryPath, objPath);


        Console.WriteLine("  Removing {0}...", fullBin);
        if (!dryRun && Directory.Exists(fullBin))
        {
            Directory.Delete(fullBin, recursive: true);
        }
        Console.WriteLine("  Removing {0}...", fullObj);
        if (!dryRun && Directory.Exists(fullObj))
        {
            Directory.Delete(fullObj, recursive: true);
        }

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

record ProjectOutputPaths(string BinPath, string ObjPath);
