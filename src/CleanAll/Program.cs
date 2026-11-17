using MinimalCli;

MinimalCommandLineBuilder builder = new(args);

builder.MapRootCommand(options => {
    options.Command.Description = "Delete all of the bin and obj directories and their contents for a solution or project.";
    
    options.PathArgument.HelpName = "Solution|Project";
    options.PathArgument.Description = "Path to the solution file, project file or directory to clean. (Directory requires one of: *.csproj|*.slnx|*.sln";

    options.DryRunOption.Aliases.Add("-dr");
    options.DryRunOption.Description = "Run the command without removing anything so you can see what directories will be deleted.";
});

MinimalCommandLineApp app = builder.Build();

await app.StartAsync();

