# dotnet clean-all

A dotnet tool that permanently deletes all bin and obj folders from a C# Project or Solution.

#### Installation 

To install globally, just run: `dotnet tool install -g CleanAll`

## Usage

Delete all the `bin\` and `obj\` folders from all the projects in a solution:

 > Attempt to read the solution file, iterate through the projects
 > and use msbuild to get the correct `bin\` and `obj\` folder paths.

```bash
CleanAll MyFolder\MyProject.slnx
```

Delete all from a project file:

 > Use msbuild to get the bin and obj folder paths.

```bash
CleanAll MyFolder\MyProject\MyProject.csproj
```

Delete all from the current directory (requires a solution or project file):

```bash
CleanAll
```

Test what the command would do without deleting anything (dry run):

```bash
CleanAll MyFolder --dry-run
```

Help docs:

```bash
CleanAll --help
```