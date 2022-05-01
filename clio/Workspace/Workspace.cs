using Clio.UserEnvironment;

namespace Clio.Workspace
{
	using System;
	using System.IO;
	using Clio.Common;
	using Clio.Package;
	using Clio.Project.NuGet;

	#region Class: Workspace

	public class Workspace : IWorkspace
	{

		#region Fields: Private

		private readonly EnvironmentSettings _environmentSettings;
		private readonly ISettingsRepository _settingsRepository;
		private readonly IWorkspacePathBuilder _workspacePathBuilder;
		private readonly IWorkspaceRestorer _workspaceRestorer;
		private readonly IPackageDownloader _packageDownloader;
		private readonly IPackageInstaller _packageInstaller;
		private readonly IPackageArchiver _packageArchiver;
		private readonly ICreatioSdk _creatioSdk;
		private readonly IJsonConverter _jsonConverter;
		private readonly IWorkingDirectoriesProvider _workingDirectoriesProvider;
		private readonly string _rootPath;

		#endregion

		#region Constructors: Public

		public Workspace(EnvironmentSettings environmentSettings, ISettingsRepository settingsRepository, 
				IWorkspacePathBuilder workspacePathBuilder, IWorkspaceRestorer workspaceRestorer,
				IPackageDownloader packageDownloader, IPackageInstaller packageInstaller, 
				IPackageArchiver packageArchiver, ICreatioSdk creatioSdk, IJsonConverter jsonConverter,
				IWorkingDirectoriesProvider workingDirectoriesProvider, IFileSystem fileSystem) {
			environmentSettings.CheckArgumentNull(nameof(environmentSettings));
			settingsRepository.CheckArgumentNull(nameof(settingsRepository));
			workspacePathBuilder.CheckArgumentNull(nameof(workspacePathBuilder));
			workspaceRestorer.CheckArgumentNull(nameof(workspaceRestorer));
			packageDownloader.CheckArgumentNull(nameof(packageDownloader));
			packageInstaller.CheckArgumentNull(nameof(packageInstaller));
			packageArchiver.CheckArgumentNull(nameof(packageArchiver));
			creatioSdk.CheckArgumentNull(nameof(creatioSdk));
			jsonConverter.CheckArgumentNull(nameof(jsonConverter));
			workingDirectoriesProvider.CheckArgumentNull(nameof(workingDirectoriesProvider));
			fileSystem.CheckArgumentNull(nameof(fileSystem));
			_environmentSettings = environmentSettings;
			_settingsRepository = settingsRepository;
			_workspacePathBuilder = workspacePathBuilder;
			_workspaceRestorer = workspaceRestorer;
			_packageDownloader = packageDownloader;
			_packageInstaller = packageInstaller;
			_packageArchiver = packageArchiver;
			_creatioSdk = creatioSdk;
			_jsonConverter = jsonConverter;
			_workingDirectoriesProvider = workingDirectoriesProvider;
			_rootPath = GetRootPath();
			_workspaceSettings = new Lazy<WorkspaceSettings>(ReadWorkspaceSettings);
		}

		#endregion

		#region Properties: Private

		private string WorkspaceSettingsPath => _workspacePathBuilder.BuildWorkspaceSettingsPath(_rootPath);
		private string PackagesPath => _workspacePathBuilder.BuildPackagesDirectoryPath(_rootPath);
		private string ClioDirectoryPath => _workspacePathBuilder.BuildClioDirectoryPath(_rootPath);

		#endregion

		#region Properties: Public

		private readonly Lazy<WorkspaceSettings> _workspaceSettings;
		public WorkspaceSettings WorkspaceSettings => _workspaceSettings.Value;

		#endregion

		#region Methods: Private

		private string GetRootPath() {
			string currentDirectory = _workingDirectoriesProvider.CurrentDirectory;
			DirectoryInfo directoryInfo = new DirectoryInfo(currentDirectory);
			while (true) {
				string presumablyClioDirectoryPath = 
					_workspacePathBuilder.BuildClioDirectoryPath(directoryInfo.FullName); 
				if (Directory.Exists(presumablyClioDirectoryPath)) {
					return directoryInfo.FullName;
				}
				if (directoryInfo.Parent == null) {
					return currentDirectory;
				}
				directoryInfo = directoryInfo.Parent;
			}
		}

		private WorkspaceSettings ReadWorkspaceSettings() {
			var workspaceSettings = _jsonConverter.DeserializeObjectFromFile<WorkspaceSettings>(WorkspaceSettingsPath);
			if (workspaceSettings != null) {
				workspaceSettings.RootPath = _rootPath;
			}
			return workspaceSettings;
		}
		

		private WorkspaceSettings CreateDefaultWorkspaceSettings() {
			WorkspaceSettings workspaceSettings = new WorkspaceSettings() {
				Name = "",
				ApplicationVersion = new Version(0, 0, 0, 0)
			};
			workspaceSettings.Environments.Add("", new WorkspaceEnvironment());
			return workspaceSettings;
		}

		private void CreateClioDirectory() {
			if (Directory.Exists(ClioDirectoryPath)) {
				return;
			}
			Directory.CreateDirectory(ClioDirectoryPath);
		}

		private void CreateWorkspaceSettingsFile() {
			if (File.Exists(WorkspaceSettingsPath)) {
				return;
			}
			WorkspaceSettings defaultWorkspaceSettings = CreateDefaultWorkspaceSettings();
			_jsonConverter.SerializeObjectToFile(defaultWorkspaceSettings, WorkspaceSettingsPath);
		}

		private EnvironmentSettings GetEnvironmentSettings(string workspaceEnvironmentName) {
			if (WorkspaceSettings.Environments.TryGetValue(workspaceEnvironmentName,
				out WorkspaceEnvironment workspaceEnvironment)) {
				string environmentName = _settingsRepository.FindEnvironmentNameByUri(workspaceEnvironment.Uri);
				if (environmentName != null) {
					return _settingsRepository.GetEnvironment(environmentName);
				}
			}
			return _environmentSettings;
		}

		#endregion

		#region Methods: Public

		public void Create() {
			CreateClioDirectory();
			CreateWorkspaceSettingsFile();
		}

		public void Restore(string workspaceEnvironmentName) {
			EnvironmentSettings environmentSettings = GetEnvironmentSettings(workspaceEnvironmentName);
			Version creatioSdkVersion = _creatioSdk.FindSdkVersion(WorkspaceSettings.ApplicationVersion);
			_packageDownloader.DownloadPackages(WorkspaceSettings.Packages, environmentSettings, PackagesPath);
			_workspaceRestorer.Restore(_rootPath, creatioSdkVersion);
		}

		public void Install(string workspaceEnvironmentName) {
			EnvironmentSettings environmentSettings = GetEnvironmentSettings(workspaceEnvironmentName);
			WorkspaceSettings workspaceSettings = WorkspaceSettings;
			_workingDirectoriesProvider.CreateTempDirectory(tempDirectory => {
				string rootPackedPackagePath = Path.Combine(tempDirectory, workspaceSettings.Name);
				Directory.CreateDirectory(rootPackedPackagePath);
				foreach (string packageName in workspaceSettings.Packages) {
					string packagePath = Path.Combine(PackagesPath, packageName);
					string packedPackagePath = Path.Combine(rootPackedPackagePath, $"{packageName}.gz");
					_packageArchiver.Pack(packagePath, packedPackagePath, true, true);
				}
				string applicationZip = Path.Combine(tempDirectory, $"{workspaceSettings.Name}.zip");
				_packageArchiver.ZipPackages(rootPackedPackagePath, 
					applicationZip, true);
				_packageInstaller.Install(applicationZip, environmentSettings);
			});
		}

		#endregion

	}

	#endregion

}