# dotnet clean-all

A dotnet tool that permanently removes all bin and obj folders from a directory's subdirectories.

To install globally, just run:

```bash
dotnet tool install -g dotnetkyle.cleanall
```

Then run `CleanAll` in the directory that you want to recursively remove the bin and obj folders in.


## Usage

Delete all from the current directory and it's sub-directories:

```bash
CleanAll
```

Test what the command would do to a specific directory (dry run):

```bash
CleanAll MyFolder --dry-run
```

Delete all from a solution's parent directory:

```bash
CleanAll MyFolder\MyProject.sln
```

Delete all from a project's parent directory:

```bash
CleanAll MyFolder\MyProject\MyProject.csproj
```
