namespace SkyFlatCampaignManager.Core.Utilities;

/// <summary>
/// Writes text atomically via temp file + replace, keeping a .bak copy.
/// </summary>
public sealed class AtomicFileWriter
{
    private readonly IFileSystem _fs;

    public AtomicFileWriter(IFileSystem fs) => _fs = fs;

    public void WriteAtomic(string targetPath, string contents)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !_fs.DirectoryExists(dir))
        {
            _fs.CreateDirectory(dir);
        }

        var tempPath = targetPath + ".tmp";
        var bakPath = targetPath + ".bak";
        _fs.WriteAllText(tempPath, contents);

        if (_fs.FileExists(targetPath))
        {
            _fs.Replace(tempPath, targetPath, bakPath);
        }
        else
        {
            _fs.Copy(tempPath, targetPath, overwrite: true);
            _fs.Delete(tempPath);
        }
    }
}
