using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Resources;
using DataGenerator.Infrastructure;
using DataGenerator.Interfaces;

namespace DataGenerator.ViewModels;

/// <summary>
///	Provides the bundled guides for the offline documentation reader as formatted pages, and follows links between them.
///	<para>
///		Links to another bundled guide open it in the reader, <c>#heading</c> links scroll to the heading and web links open
///		in the user's browser.
///	</para>
/// </summary>
public sealed class DocumentationViewModel : ObservableObject
{
	#region FIELDS
	#region PRIVATE
	private const string RESOURCE_URI_FORMAT = "pack://application:,,,/DataGenerator;component/Documentation/{0}";

	private readonly IMarkdownRenderer          _markdownRenderer;
	private readonly IShellService              _shellService;
	private readonly Dictionary<string, string> _renderedPages;
	private ChoiceOption<string>                _selectedTopic;
	private string?                             _fragment;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
	public IReadOnlyList<ChoiceOption<string>> Topics { get; }

	public ChoiceOption<string> SelectedTopic
	{
		get => _selectedTopic;
		set
		{
			if (value is not null && SetProperty(ref _selectedTopic, value))
			{
				Fragment = null;
				OnPropertyChanged(nameof(Text));
				OnPropertyChanged(nameof(Html));
			}
		}
	}

	public string Text => SelectedTopic.Description;

	public string Html => RenderPage(SelectedTopic);

	public string? Fragment
	{
		get => _fragment;
		private set => SetProperty(ref _fragment, value);
	}

	public ICommand OpenLinkCommand { get; }
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Loads the bundled documentation and starts with the SQL conditions and execution guide.
	/// </summary>
	/// <param name="markdownRenderer">
	///	Converts each guide into a formatted page the first time it is shown.
	/// </param>
	/// <param name="shellService">
	///	Opens web links in the user's browser.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///	Thrown when any argument is <see langword="null"/>.
	/// </exception>
	/// <exception cref="IOException">
	///	Thrown when a bundled document is missing or cannot be read.
	/// </exception>
	public DocumentationViewModel(IMarkdownRenderer markdownRenderer, IShellService shellService)
	{
		_markdownRenderer = markdownRenderer ?? throw new ArgumentNullException(nameof(markdownRenderer));
		_shellService     = shellService ?? throw new ArgumentNullException(nameof(shellService));
		_renderedPages    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		_fragment         = null;

		Topics =
		[
			LoadTopic("StepsUpdatesAndLookups.md", "SQL WHERE, lookups, steps and update sets"),
			LoadTopic("README.md", "Getting started"),
			LoadTopic("PatternLanguage.md", "Pattern language"),
			LoadTopic("SavedSettings.md", "Saved column settings and Set configurations"),
			LoadTopic("PostGenerationSql.md", "Post-generation SQL")
		];
		_selectedTopic  = Topics[0];
		OpenLinkCommand = new RelayCommand(parameter => OpenLink(parameter as string));
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS

	#region METHODS
	#region PUBLIC
	/// <summary>
	///	Follows a link clicked in a guide.
	///	<para>
	///		<c>#heading</c> scrolls the current guide, <c>Guide.md#heading</c> opens a bundled guide at that heading, and
	///		<c>http</c>, <c>https</c> and <c>mailto</c> links open with the user's default application. Anything else, such as
	///		links to files outside the bundled guides, is ignored.
	///	</para>
	/// </summary>
	/// <param name="href">
	///	The link target exactly as written in the guide.
	/// </param>
	/// <returns>
	///	True when the link was followed; otherwise false.
	/// </returns>
	public bool OpenLink(string? href)
	{
		if (string.IsNullOrWhiteSpace(href))
		{
			return false;
		}

		string target = href.Trim();

		if (target.StartsWith('#'))
		{
			return ShowFragment(target[1..]);
		}

		if (Uri.TryCreate(target, UriKind.Absolute, out Uri? absolute) && !absolute.IsFile)
		{
			if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps && absolute.Scheme != Uri.UriSchemeMailto)
			{
				return false;
			}

			_shellService.OpenLink(absolute);

			return true;
		}

		int    hashIndex = target.IndexOf('#');
		string path      =
			hashIndex < 0
				? target
				: target[..hashIndex];
		string fragment  =
			hashIndex < 0
				? string.Empty
				: target[(hashIndex + 1)..];
		string fileName  = Path.GetFileName(Uri.UnescapeDataString(path).Replace('/', Path.DirectorySeparatorChar));

		ChoiceOption<string>? topic =
			Topics
				.FirstOrDefault(
					item => string.Equals(item.Value, fileName, StringComparison.OrdinalIgnoreCase)
				);

		if (topic is null)
		{
			return false;
		}

		Fragment      = null;
		SelectedTopic = topic;
		_             = ShowFragment(fragment);

		return true;
	}
	#endregion PUBLIC

	#region PRIVATE
	/// <summary>
	///	Asks the reader to scroll to a heading of the current guide, even when it is the heading already shown.
	/// </summary>
	/// <param name="fragment">
	///	The heading identifier, which may be URL-encoded.
	/// </param>
	/// <returns>
	///	True when there was a heading to scroll to; otherwise false.
	/// </returns>
	private bool ShowFragment(string fragment)
	{
		string identifier = Uri.UnescapeDataString(fragment);

		if (identifier.Length == 0)
		{
			return false;
		}

		Fragment = null;
		Fragment = identifier;

		return true;
	}

	/// <summary>
	///	Returns the formatted page for a guide, rendering it only the first time it is needed.
	/// </summary>
	/// <param name="topic">
	///	The guide to render.
	/// </param>
	/// <returns>
	///	The guide as an HTML fragment.
	/// </returns>
	private string RenderPage(ChoiceOption<string> topic)
	{
		if (!_renderedPages.TryGetValue(topic.Value, out string? html))
		{
			html = _markdownRenderer.RenderHtml(topic.Description);
			_renderedPages[topic.Value] = html;
		}

		return html;
	}

	/// <summary>
	///	Reads one document embedded in the application so installed and single-file builds have the same guides.
	/// </summary>
	/// <param name="fileName">
	///	The bundled Markdown file name.
	/// </param>
	/// <param name="title">
	///	The readable title shown in the topic selector.
	/// </param>
	/// <returns>
	///	A topic containing its file name, title and complete Markdown text.
	/// </returns>
	/// <exception cref="IOException">
	///	Thrown when the resource cannot be found or read.
	/// </exception>
	private static ChoiceOption<string> LoadTopic(string fileName, string title)
	{
		Uri                uri      = new(string.Format(RESOURCE_URI_FORMAT, fileName), UriKind.Absolute);
		StreamResourceInfo resource =
			Application.GetResourceStream(uri)
				?? throw new IOException($"The bundled documentation '{fileName}' could not be found.");
		using StreamReader reader   = new(resource.Stream);

		return new ChoiceOption<string>(fileName, title, reader.ReadToEnd());
	}
	#endregion PRIVATE
	#endregion METHODS
}
