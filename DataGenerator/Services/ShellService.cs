using System.Diagnostics;
using System.IO;
using DataGenerator.Interfaces;

namespace DataGenerator.Services;

public sealed class ShellService : IShellService
{
	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ShellService"/>.
	/// </summary>
	public ShellService()
	{
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Opens a folder in Explorer, optionally selecting an existing file inside it.
	/// </summary>
	/// <param name="folderPath">
	///	The folder to open when no selectable file is supplied.
	/// </param>
	/// <param name="fileToSelect">
	///	An optional file to select in Explorer when it exists.
	/// </param>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="folderPath"/> is <see langword="null"/>, empty or whitespace.
	/// </exception>
	/// <exception cref="DirectoryNotFoundException">
	///	Thrown when <paramref name="folderPath"/> does not exist and no selectable file was supplied.
	/// </exception>
	public void OpenFolder(string folderPath, string? fileToSelect = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		if (!string.IsNullOrWhiteSpace(fileToSelect) && File.Exists(fileToSelect))
		{
			using Process? explorerWithSelection = Process.Start(new ProcessStartInfo
			{
				FileName        = "explorer.exe",
				Arguments       = $"/select,\"{Path.GetFullPath(fileToSelect)}\"",
				UseShellExecute = true
			});

			return;
		}

		if (!Directory.Exists(folderPath))
		{
			throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist yet.");
		}

		using Process? explorer = Process.Start(new ProcessStartInfo
		{
			FileName        = Path.GetFullPath(folderPath),
			UseShellExecute = true
		});
	}

	/// <summary>
	///	Opens a web or e-mail link with the user's default application.
	/// </summary>
	/// <param name="uri">
	///	An absolute <c>http</c>, <c>https</c> or <c>mailto</c> link.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when <paramref name="uri"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	///	Thrown when <paramref name="uri"/> is relative or uses another scheme, such as <c>file</c>.
	/// </exception>
	public void OpenLink(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);

		if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto)
		{
			throw new ArgumentException($"Only web and e-mail links can be opened, not '{uri.OriginalString}'.", nameof(uri));
		}

		using Process? browser = Process.Start(new ProcessStartInfo
		{
			FileName        = uri.AbsoluteUri,
			UseShellExecute = true
		});
	}
	#endregion PUBLIC
	#endregion METHODS
}