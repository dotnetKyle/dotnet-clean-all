using System.CommandLine;
using System.Runtime.InteropServices;

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
        this.SetHandler(Execute, pathArgument, dryRunOption);
    }

    static void Execute(string? path, bool dryRun)
    {
        // cannot specify current directory as an Argument.SetDefaultValue because it caches
        //   the value. Instead evaluate it when the command is ran during Execute().
        if(string.IsNullOrWhiteSpace(path))
            path = Environment.CurrentDirectory;


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
                return;
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
            return;
        }

        // TODO: if a solution, go through and only clean the projects that are in the solution

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
            return;
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

                Console.WriteLine($"Deleting {binObj}...");

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

        return;
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
