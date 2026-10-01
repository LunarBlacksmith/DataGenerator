using System.Windows;
using DataGenerator.Services;
using DataGenerator.ViewModels;

namespace DataGenerator;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		ISqlMetadataService metadataService      = new SqlMetadataService();
		IRegexValueGenerator regexValueGenerator = new RegexValueGenerator();
		IDataGenerationService generationService = new DataGenerationService(regexValueGenerator);
		IFileDialogService fileDialogService     = new FileDialogService();

		MainViewModel viewModel = new MainViewModel(
			 metadataService,
			 generationService,
			 fileDialogService);

		MainWindow window = new MainWindow
		{
			DataContext = viewModel
		};

		window.Show();
	}
}