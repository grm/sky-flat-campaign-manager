namespace SkyFlatCampaignManager.Core.Utilities;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void WriteAllBytes(string path, byte[] contents);
    byte[] ReadAllBytes(string path);
    void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName);
    void Delete(string path);
    void Copy(string source, string destination, bool overwrite);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
}

public sealed class RealFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);
    public void WriteAllBytes(string path, byte[] contents) => File.WriteAllBytes(path, contents);
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void Replace(string sourceFileName, string destinationFileName, string? destinationBackupFileName)
        => File.Replace(sourceFileName, destinationFileName, destinationBackupFileName, ignoreMetadataErrors: true);
    public void Delete(string path) => File.Delete(path);
    public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        => Directory.EnumerateFiles(path, searchPattern);
}
