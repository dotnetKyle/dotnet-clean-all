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

        var dir = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        var ext = Path.GetExtension(path);

        // TODO: if a solution, go through and only purge the projects that are in the solution

        if (dir is null || !Directory.Exists(dir))
        {
            Console.Error.WriteLine("The provided directory does not exist.");
            return;
        }


        IEnumerable<string> all = Directory.EnumerateDirectories(dir, "bin",
            new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                ReturnSpecialDirectories = false
            })
            .Where(directory => directory.EndsWith("\\bin") || directory.EndsWith("/bin"))
            .Concat(
            Directory.EnumerateDirectories(dir, "obj",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                    ReturnSpecialDirectories = false
                })
                .Where(directory => directory.EndsWith("\\obj") || directory.EndsWith("/obj"))
            );

        if (all.Any() == false)
        {
            ConsoleColorRed();
            Console.Error.WriteLine("No bin/ or obj/ directories found to delete.");
            RestoreConsoleColor();
            return;
        }

        foreach (var binObj in all)
        {
            try
            {
                Console.WriteLine($"Deleting {binObj}...");
                if (!dryRun)
                {
                    Directory.Delete(binObj, recursive: true);
                }
            }
            catch (Exception ex)
            {
                ConsoleColorRed();
                Console.Error.WriteLine(ex.Message);
                RestoreConsoleColor();
            }
        }
    }

    static void ConsoleColorRed()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.ForegroundColor = ConsoleColor.Red;
    }
    static void RestoreConsoleColor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Console.ResetColor();
    }
}
