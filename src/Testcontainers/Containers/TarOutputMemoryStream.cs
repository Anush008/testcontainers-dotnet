namespace DotNet.Testcontainers.Containers
{
  using System;
  using System.IO;
  using System.Text;
  using System.Threading;
  using System.Threading.Tasks;
  using DotNet.Testcontainers.Configurations;
  using ICSharpCode.SharpZipLib.Tar;

  /// <summary>
  /// Represent a tar archive file.
  /// </summary>
  public sealed class TarOutputMemoryStream : TarOutputStream
  {
    private readonly string _targetDirectoryPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TarOutputMemoryStream" /> class.
    /// </summary>
    /// <param name="targetDirectoryPath">The target directory path to extract the files to.</param>
    public TarOutputMemoryStream(string targetDirectoryPath)
      : this()
    {
      _targetDirectoryPath = targetDirectoryPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TarOutputMemoryStream" /> class.
    /// </summary>
    public TarOutputMemoryStream()
      : base(new MemoryStream(), Encoding.Default)
    {
      IsStreamOwner = false;
    }

    /// <summary>
    /// Adds the content of an implementation of <see cref="IResourceMapping" /> to the archive.
    /// </summary>
    /// <param name="resourceMapping">The resource mapping to add to the archive.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AddAsync(IResourceMapping resourceMapping, CancellationToken ct = default)
    {
      var fileContent = await resourceMapping.GetAllBytesAsync(ct)
        .ConfigureAwait(false);

      var targetFilePath = Unix.Instance.NormalizePath(resourceMapping.Target);

      var tarEntry = new TarEntry(new TarHeader());
      tarEntry.TarHeader.Name = targetFilePath;
      tarEntry.TarHeader.Mode = (int)resourceMapping.FileMode;
      tarEntry.TarHeader.ModTime = DateTime.UtcNow;
      tarEntry.Size = fileContent.Length;

      await PutNextEntryAsync(tarEntry, ct)
        .ConfigureAwait(false);

#if NETSTANDARD2_1_OR_GREATER
      await WriteAsync(fileContent, ct)
        .ConfigureAwait(false);
#else
      await WriteAsync(fileContent, 0, fileContent.Length, ct)
        .ConfigureAwait(false);
#endif

      await CloseEntryAsync(ct)
        .ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a file to the archive.
    /// </summary>
    /// <param name="file">The file to add to the archive.</param>
    /// <param name="fileMode">The POSIX file mode permission.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the file has been added to the archive.</returns>
    public Task AddAsync(FileInfo file, UnixFileModes fileMode, CancellationToken ct = default)
    {
      return AddAsync(file.Directory, file, fileMode, ct);
    }

    /// <summary>
    /// Adds a directory to the archive.
    /// </summary>
    /// <param name="directory">The directory to add to the archive.</param>
    /// <param name="recurse">A value indicating whether the current directory and all its subdirectories are included or not.</param>
    /// <param name="fileMode">The POSIX file mode permission.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AddAsync(DirectoryInfo directory, bool recurse, UnixFileModes fileMode, CancellationToken ct = default)
    {
      var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

      foreach (var file in directory.GetFiles("*", searchOption))
      {
        await AddAsync(directory, file, fileMode, ct)
          .ConfigureAwait(false);
      }
    }

    /// <summary>
    /// Adds a file to the archive.
    /// </summary>
    /// <param name="directory">The root directory of the file to add to the archive.</param>
    /// <param name="file">The file to add to the archive.</param>
    /// <param name="fileMode">The POSIX file mode permission.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AddAsync(DirectoryInfo directory, FileInfo file, UnixFileModes fileMode, CancellationToken ct = default)
    {
      using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
      {
        var targetFilePath = Unix.Instance.NormalizePath(Path.Combine(_targetDirectoryPath, file.FullName.Substring(directory.FullName.TrimEnd(Path.DirectorySeparatorChar).Length + 1)));

        var tarEntry = new TarEntry(new TarHeader());
        tarEntry.TarHeader.Name = targetFilePath;
        tarEntry.TarHeader.Mode = (int)fileMode;
        tarEntry.TarHeader.ModTime = file.LastWriteTimeUtc;
        tarEntry.Size = stream.Length;

        await PutNextEntryAsync(tarEntry, ct)
          .ConfigureAwait(false);

        await stream.CopyToAsync(this, 81920, ct)
          .ConfigureAwait(false);

        await CloseEntryAsync(ct)
          .ConfigureAwait(false);
      }
    }
  }
}
