using CleanAll;
using System.CommandLine;

RootCommand command = new CleanCommand();

await command.InvokeAsync(args);

Console.WriteLine();