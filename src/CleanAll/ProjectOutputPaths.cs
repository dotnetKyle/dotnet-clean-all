using System.Diagnostics.CodeAnalysis;

namespace CleanAll;

internal record ProjectOutputPaths(string? Bin, string? Obj, string? FullBin, string? FullObj)
{
    /// <summary>
    /// Gets a value indicating whether binary output paths are available.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Bin), nameof(FullBin), nameof(BinPath))]
    public bool HasBin => this.Bin is not null && this.FullBin is not null;

    /// <summary>
    /// Determin if any of the obj properties are null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Obj), nameof(FullObj), nameof(ObjPath))]
    public bool HasObj => this.Obj is not null && this.FullObj is not null;

    /// <summary>
    /// UI Friendly path of the bin directory, not null when <c>HasBin</c> returns true
    /// </summary>
    public string? BinPath
    {
        get
        {
            if (this.HasBin)
            {
                return this.Bin.Contains("..") 
                    ? this.FullBin 
                    : this.Bin;
            }

            return null;
        }
    }

    /// <summary>
    /// UI Friendly path of the obj directory, not null when <c>HasObj</c> returns true
    /// </summary>
    public string? ObjPath
    {
        get
        {
            if (this.HasObj)
            {
                return this.Obj.Contains("..")
                    ? this.FullObj
                    : this.Obj;
            }

            return null;
        }
    }
}
