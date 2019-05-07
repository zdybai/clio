using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using bpmcli.environment;
using CommandLine;
using ConsoleTables;
using Newtonsoft.Json;

namespace bpmcli
{

	public class StringParser
	{
		public static IEnumerable<string> ParseArray(string input) {
			return input.Split(',').Select(p => p.Trim()).ToList();
		}
	}

	class Program
	{
		private static string _userName;
		private static string _userPassword;
		private static string _url; // Необходимо получить из конфига
		private static string LoginUrl => _url + @"/ServiceModel/AuthService.svc/Login";
		private static string ExecutorUrl => _url + @"/0/IDE/ExecuteScript";
		private static string UnloadAppDomainUrl => _url + @"/0/ServiceModel/AppInstallerService.svc/UnloadAppDomain";
		private static string DownloadPackageUrl => _url + @"/0/ServiceModel/AppInstallerService.svc/LoadPackagesToFileSystem";
		private static string UploadPackageUrl => _url + @"/0/ServiceModel/AppInstallerService.svc/LoadPackagesToDB";
		private static string UploadUrl => _url + @"/0/ServiceModel/PackageInstallerService.svc/UploadPackage";
		private static string InstallUrl => _url + @"/0/ServiceModel/PackageInstallerService.svc/InstallPackage";
		private static string LogUrl => _url + @"/0/ServiceModel/PackageInstallerService.svc/GetLogFile";
		private static string SelectQueryUrl => _url + @"/0/DataService/json/SyncReply/SelectQuery";
		private static string UninstallAppUrl => _url + @"/0/ServiceModel/AppInstallerService.svc/DeletePackage";
		private static string ClearRedisDbUrl => _url + @"/0/ServiceModel/AppInstallerService.svc/ClearRedisDb";
		private static string GetZipPackageUrl => _url + @"/0/ServiceModel/PackageInstallerService.svc/GetZipPackages";
		private static string PingUrl => _url + @"/0/ping";
		private static string LastVersionUrl => "https://api.github.com/repos/Advance-Technologies-Foundation/bpmcli/releases/latest";
		private static string ExecuteSqlScriptUrl => _url + @"/0/rest/BpmcliApiGateway/ExecuteSqlScript";
		private static string ApiVersionUrl => _url + @"/0/rest/BpmcliApiGateway/GetApiVersion";

		public static CookieContainer AuthCookie = new CookieContainer();

		private static string CurrentProj =>
			new DirectoryInfo(Environment.CurrentDirectory).GetFiles("*.csproj").FirstOrDefault()?.FullName;

		public static bool _safe { get; private set; } = true;

		private static void Configure(EnvironmentOptions options) {
			var settingsRepository = new SettingsRepository();
			var settings = settingsRepository.GetEnvironment(options.Environment);
			_url = string.IsNullOrEmpty(options.Uri) ? settings.Uri : options.Uri;
			_userName = string.IsNullOrEmpty(options.Login) ? settings.Login : options.Login;
			_userPassword = string.IsNullOrEmpty(options.Password) ? settings.Password : options.Password;
			if (settings.Safe.HasValue && settings.Safe.Value && _safe) {

				Console.WriteLine($"You try to apply the action on the production site {settings.Uri}");
				Console.Write($"Do you want to continue? [Y/N]:");
				var answer = Console.ReadKey();
				Console.WriteLine();
				if (answer.KeyChar != 'y') {
					Console.WriteLine("Operation was canceled by user");
					Environment.Exit(1);
				}
			}
		}

		private static void CheckUpdate() {
			var currentVersion = GetCurrentVersion();
			var latestVersion = GetLatestVersion();
			if (currentVersion != latestVersion) {
				MessageToConsole($"You are using bpmcli version {currentVersion}, however version {latestVersion} is available." +
								 $"{Environment.NewLine}You should consider upgrading via the \'bpmcli update-cli\' command.",
					ConsoleColor.DarkYellow);
			}
		}



		private static int UpdateCli() {
			try {
				var url = GetLastReleaseUrl();
				var dir = AppDomain.CurrentDomain.BaseDirectory;
				string updaterDirPath = Path.Combine(dir, "Update");
				string tempDirPath = Path.Combine(dir, "Update", "Temp");
				string filePath = Path.Combine(updaterDirPath, "update.zip");
				string updaterName = "updater.dll";
				Directory.CreateDirectory(tempDirPath);
				Console.WriteLine("Download update.");
				using (var client = new WebClient()) {
					client.DownloadFile(url, filePath);
				}
				ZipFile.ExtractToDirectory(filePath, tempDirPath, true);
				var updaterFile = new FileInfo(Path.Combine(tempDirPath, updaterName));
				updaterFile.CopyTo(Path.Combine(dir, updaterFile.Name), true);
				var updateCmdPath = Path.Combine(dir, "update.cmd");
				var proc = new Process { StartInfo = { FileName = updateCmdPath } };
				Console.WriteLine("Start update.");
				proc.Start();
				return 0;
			} catch (Exception) {
				Console.WriteLine("Update error.");
				return 1;
			}
		}

		private static int UpdateGate(EnvironmentOptions options) {
			try {
				Configure(options);
				Login();
				var dir = AppDomain.CurrentDomain.BaseDirectory;
				string packageFilePath = Path.Combine(dir, "bpmcligate", "bpmcligate.gz");
				InstallPackage(packageFilePath);
				UnloadAppDomain();
				return 0;
			} catch (Exception e) {
				Console.WriteLine($"Update error {e.Message}");
				return 1;
			}
		}

		private static string GetLastReleaseUrl() {
			System.Threading.Tasks.Task<byte[]> body;
			using (var client = new HttpClient()) {
				client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
				using (var response = client.GetAsync(LastVersionUrl).Result) {
					response.EnsureSuccessStatusCode();
					body = response.Content.ReadAsByteArrayAsync();
				}
			}
			string json;
			var jsonStream = new MemoryStream(body.Result) { Position = 0 };
			using (var reader = new StreamReader(jsonStream, Encoding.UTF8)) {
				json = reader.ReadToEnd();
			}
			JsonObject jsonDoc = (JsonObject)JsonValue.Parse(json);
			var url = jsonDoc["assets"][0]["browser_download_url"];
			return url;
		}

		private static void MessageToConsole(string text, ConsoleColor color) {
			var currentColor = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(text);
			Console.ForegroundColor = currentColor;
		}

		private static string GetCurrentVersion() {
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
			return fileVersionInfo.FileVersion;
		}

		private static string GetLatestVersion() {
			System.Threading.Tasks.Task<byte[]> body;
			using (var client = new HttpClient()) {
				client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
				using (var response = client.GetAsync(LastVersionUrl).Result) {
					response.EnsureSuccessStatusCode();
					body = response.Content.ReadAsByteArrayAsync();
				}
			}
			string json;
			var jsonStream = new MemoryStream(body.Result) { Position = 0 };
			using (var reader = new StreamReader(jsonStream, Encoding.UTF8)) {
				json = reader.ReadToEnd();
			}
			var jsonDoc = (JsonObject)JsonValue.Parse(json);
			var version = jsonDoc["tag_name"];
			return version;
		}

		public static void SetupAppConnection(EnvironmentOptions options) {
			Configure(options);
			PingApp();
			Login();
			CheckApiVersion();
		}

		public static void Login() {
			var authRequest = HttpWebRequest.Create(LoginUrl) as HttpWebRequest;
			authRequest.Method = "POST";
			authRequest.ContentType = "application/json";
			authRequest.CookieContainer = AuthCookie;
			using (var requestStream = authRequest.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write(@"{
						""UserName"":""" + _userName + @""",
						""UserPassword"":""" + _userPassword + @"""
					}");
				}
			}
			using (var response = (HttpWebResponse)authRequest.GetResponse()) {
				if (response.StatusCode == HttpStatusCode.OK) {
					using (var reader = new StreamReader(response.GetResponseStream())) {
						var responseMessage = reader.ReadToEnd();
						if (responseMessage.Contains("\"Code\":1")) {
							throw new UnauthorizedAccessException($"Unauthotized {_userName} for {_url}");
						}
					}

					string authName = ".ASPXAUTH";
					string headerCookies = response.Headers["Set-Cookie"];
					string authCookeValue = GetCookieValueByName(headerCookies, authName);
					AuthCookie.Add(new Uri(_url), new Cookie(authName, authCookeValue));
				}
			}
		}

		public static void CheckApiVersion() {
			var dir = AppDomain.CurrentDomain.BaseDirectory;
			string versionFilePath = Path.Combine(dir, "bpmcligate", "version.txt");
			var localApiVersion = new Version(File.ReadAllText(versionFilePath));
			var appApiVersion = GetAppApiVersion();
			if (appApiVersion == new Version("0.0.0.0")) {
				MessageToConsole($"You app are not contains bpmcli api" +
				 $"{Environment.NewLine}You should consider install it via the \'bpmcli install-gate\' command.", ConsoleColor.DarkYellow);
			} else if (localApiVersion > appApiVersion) {
				MessageToConsole($"You are using bpmcli api version {appApiVersion}, however version {localApiVersion} is available." +
				 $"{Environment.NewLine}You should consider upgrading via the \'bpmcli update-gate\' command.", ConsoleColor.DarkYellow);
			}
		}

		private static void PingApp() {
			var pingRequest = HttpWebRequest.Create(PingUrl) as HttpWebRequest;
			pingRequest.Method = "POST";
			pingRequest.ContentType = "application/json";
			pingRequest.CookieContainer = AuthCookie;
			pingRequest.Timeout = 60000;
			using (var requestStream = pingRequest.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write(@"{}");
				}
			}
			try {
				using (var response = (HttpWebResponse)pingRequest.GetResponse()) {
				}
			} catch (Exception e) {
				Console.WriteLine(e);
				throw;
			}
		}

		private static string GetCookieValueByName(string headerCookies, string name) {
			string tokens = headerCookies.Replace("HttpOnly,", string.Empty);
			string[] cookies = tokens.Split(';');
			foreach (var cookie in cookies) {
				if (cookie.Contains(name)) {
					return cookie.Split('=')[1];
				}
			}
			return string.Empty;
		}

		private static void AddCsrfToken(HttpWebRequest request) {
			var bpmcsrf = request.CookieContainer.GetCookies(new Uri(_url))["BPMCSRF"];
			if (bpmcsrf != null) {
				request.Headers.Add("BPMCSRF", bpmcsrf.Value);
			}
		}

		private static Version GetAppApiVersion() {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiVersionUrl);
			request.Method = "GET";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			request.ContentType = "application/json";
			var apiVersion = new Version("0.0.0.0");
			;
			try {
				using (var response = (HttpWebResponse)request.GetResponse()) {
					using (var dataStream = response.GetResponseStream()) {
						StreamReader reader = new StreamReader(dataStream);
						string appVersionResponse = reader.ReadToEnd().Trim('"');
						apiVersion = new Version(appVersionResponse);
					}
				}
			} catch (Exception) {
			}
			return apiVersion;
		}

		private static void ExecuteScript(ExecuteAssemblyOptions options) {
			string filePath = options.Name;
			string executorType = options.ExecutorType;
			var fileContent = File.ReadAllBytes(filePath);
			string body = Convert.ToBase64String(fileContent);
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ExecutorUrl);
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write(@"{
						""Body"":""" + body + @""",
						""LibraryType"":""" + executorType + @"""
					}");
				}
			}
			request.ContentType = "application/json";
			Stream dataStream;
			WebResponse response = request.GetResponse();
			Console.WriteLine(((HttpWebResponse)response).StatusDescription);
			dataStream = response.GetResponseStream();
			StreamReader reader = new StreamReader(dataStream);
			string responseFromServer = reader.ReadToEnd();
			Console.WriteLine(responseFromServer);
			reader.Close();
			dataStream.Close();
			response.Close();
		}

		private static void UnloadAppDomain() {
			ExecutePostRequest(UnloadAppDomainUrl, @"{}");
		}

		private static void ClearRedisDbInternal() {
			ExecutePostRequest(ClearRedisDbUrl, @"{}");
		}

		private static int ConfigureEnvironment(RegAppOptions options) {
			try {
				_safe = false;
				var repository = new SettingsRepository();
				var environment = new EnvironmentSettings() {
					Login = options.Login,
					Password = options.Password,
					Uri = options.Uri,
					Maintainer = options.Maintainer,
					Safe = options.SafeValue
				};
				if (!String.IsNullOrEmpty(options.ActiveEnvironment)) {
					if (repository.IsExistInEnvironment(options.ActiveEnvironment))
						repository.SetActiveEnvironment(options.ActiveEnvironment);
					else
						throw new Exception($"Not found environment {options.ActiveEnvironment} in settings");
				}
				repository.ConfigureEnvironment(options.Name, environment);
				options.Environment = options.Name;
				repository.ShowSettingsTo(Console.Out, options.Name);
				Console.WriteLine();
				Configure(options);
				Console.WriteLine($"Try login to {_url} with {_userName} credentials...");
				Login();
				Console.WriteLine($"Login done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine($"{e.Message}");
				return 1;
			}
		}

		private static int ViewEnvironments(AppListOptions options) {
			try {
				var repository = new SettingsRepository();
				repository.ShowSettingsTo(Console.Out, options.Name);
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		private static int UnregApplication(UnregAppOptions options) {
			try {
				var repository = new SettingsRepository();
				repository.RemoveEnvironment(options.Name);
				repository.ShowSettingsTo(Console.Out);
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		private static void DownloadPackages(string packageName) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(DownloadPackageUrl);
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write("[\"" + packageName + "\"]");
				}
			}
			request.ContentType = "application/json";
			Stream dataStream;
			WebResponse response = request.GetResponse();
			//Console.WriteLine(((HttpWebResponse)response).StatusDescription);
			dataStream = response.GetResponseStream();
			StreamReader reader = new StreamReader(dataStream);
			string responseFromServer = reader.ReadToEnd();
			Console.WriteLine(packageName + " - " + responseFromServer);
			reader.Close();
			dataStream.Close();
			response.Close();
		}

		private static void UploadPackages(string packageName) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UploadPackageUrl);
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write("[\"" + packageName + "\"]");
				}
			}
			request.ContentType = "application/json";
			Stream dataStream;
			WebResponse response = request.GetResponse();
			//Console.WriteLine(((HttpWebResponse)response).StatusDescription);
			dataStream = response.GetResponseStream();
			StreamReader reader = new StreamReader(dataStream);
			string responseFromServer = reader.ReadToEnd();
			Console.WriteLine(packageName + " - " + responseFromServer);
			reader.Close();
			dataStream.Close();
			response.Close();
		}

		private static void CompressionProjects(string sourcePath, string destinationPath, IEnumerable<string> names) {
			string tempPath = Path.Combine(Path.GetTempPath(), "Application_");// + DateTime.Now.ToShortDateString());
			if (Directory.Exists(tempPath)) {
				Directory.Delete(tempPath, true);
			}
			if (sourcePath == null) {
				sourcePath = Environment.CurrentDirectory;
			}
			Directory.CreateDirectory(tempPath);
			foreach (var name in names) {
				var currentSourcePath = Path.Combine(sourcePath, name);
				var currentDestinationPath = Path.Combine(tempPath, name + ".gz");
				CompressionProject(currentSourcePath, currentDestinationPath);
			}
			ZipFile.CreateFromDirectory(tempPath, destinationPath);
		}

		internal static IEnumerable<string> GetPackages(string inputline) {
			return StringParser.ParseArray(inputline);
		}

		private static void CompressionProject(string sourcePath, string destinationPath) {
			if (File.Exists(destinationPath)) {
				File.Delete(destinationPath);
			}
			string tempPath = CreateTempPath(sourcePath);
			CopyProjectFiles(sourcePath, tempPath);

			string[] files = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories);
			int directoryPathLength = tempPath.Length;
			using (Stream fileStream =
				File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
				using (var zipStream = new GZipStream(fileStream, CompressionMode.Compress)) {
					foreach (string filePath in files) {
						CompressionUtilities.ZipFile(filePath, directoryPathLength, zipStream);
					}
				}
			}
			Directory.Delete(tempPath, true);
		}

		private static string CreateTempPath(string sourcePath) {
			var directoryInfo = new DirectoryInfo(sourcePath);
			string tempPath = Path.Combine(Path.GetTempPath(), directoryInfo.Name);
			return tempPath;
		}

		private static void CopyProjectFiles(string sourcePath, string destinationPath) {
			if (Directory.Exists(destinationPath)) {
				Directory.Delete(destinationPath, true);
			}
			Directory.CreateDirectory(destinationPath);
			CopyProjectElement(sourcePath, destinationPath, "Assemblies");
			CopyProjectElement(sourcePath, destinationPath, "Bin");
			CopyProjectElement(sourcePath, destinationPath, "Data");
			CopyProjectElement(sourcePath, destinationPath, "Files");
			CopyProjectElement(sourcePath, destinationPath, "Resources");
			CopyProjectElement(sourcePath, destinationPath, "Schemas");
			CopyProjectElement(sourcePath, destinationPath, "SqlScripts");
			File.Copy(Path.Combine(sourcePath, "descriptor.json"), Path.Combine(destinationPath, "descriptor.json"));
		}

		private static void DownloadZipPackagesInternal(string packageName, string destinationPath) {
			Console.WriteLine("Start download packages ({0}).", packageName);
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetZipPackageUrl);
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			var packageNames = string.Format("\"{0}\"", packageName.Replace(" ", string.Empty).Replace(",", "\",\""));
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write("[" + packageNames + "]");
				}
			}
			request.ContentType = "application/json";
			Stream dataStream;
			WebResponse response = request.GetResponse();
			dataStream = response.GetResponseStream();
			if (dataStream != null) {
				var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
				dataStream.CopyTo(fileStream);
				fileStream.Dispose();
				dataStream.Close();
				Console.WriteLine("Download packages ({0}) completed.", packageName);
			} else {
				Console.WriteLine("Download packages ({0}) not completed.", packageName);
			}
			response.Close();
		}

		private static int ExecuteSqlScript(ExecuteSqlScriptOptions opts) {
			try {
				SetupAppConnection(opts);
				string result = string.Empty;
				if (!string.IsNullOrEmpty(opts.Script)) {
					result = ExecuteSqlScript(opts.Script);
				} else if (!string.IsNullOrEmpty(opts.File)) {
					var script = File.ReadAllText(opts.File);
					Console.WriteLine(script);
					script = script.Replace(Environment.NewLine, "|nl|");
					result = ExecuteSqlScript(script);
				} else {
					Console.WriteLine("Enter sql (Ctrl+C for exit): ");
					var sc = Console.ReadLine();
					result = ExecuteSqlScript(sc);
				}
				result = GetSqlScriptResult(result, opts.ViewType);
				Console.WriteLine(result);
				if (opts.DestPath != null) {
					File.WriteAllText(opts.DestPath, result);
				}
				Console.WriteLine("Done");
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			return 0;
		}

		private static string GetSqlScriptResult(string result, string viewType) {
			viewType = viewType.ToLower();
			if (viewType == "table") {
				if (result == "[]") {
					return string.Empty;
				}
				if (int.TryParse(result, out var count)) {
					return $"({count} rows affected)";
				}
				var dataTable = JsonConvert.DeserializeObject<DataTable>(result);
				var table = CreateConsoleTable(dataTable);
				return table.ToString();
			}
			return result;
		}

		private static string ExecuteSqlScript(string script) {
			var scriptData = "{ \"script\":\"" + script + "\"}";
			return ExecutePostRequest(ExecuteSqlScriptUrl, scriptData);
		}

		private static ConsoleTable CreateConsoleTable(DataTable dataTable) {
			var table = new ConsoleTable();
			foreach (var column in dataTable.Columns) {
				table.AddColumn(new [] { column.ToString() });
			}
			for (var i = 0; i < dataTable.Rows.Count; i++) {
				table.AddRow(dataTable.Rows[i].ItemArray);
			}
			return table;
		}

		private static void UnZipPackages(string zipFilePath) {
			var fileInfo = new FileInfo(zipFilePath);
			if (fileInfo.Length == 0) {
				throw new Exception("CompressionUtilities.Exception.FileIsEmpty");
			}
			string targetDirectoryPath = GetPackagePathFromZipFile(zipFilePath, ".zip");
			if (Directory.Exists(targetDirectoryPath)) {
				Directory.Delete(targetDirectoryPath, true);
			}
			ZipFile.ExtractToDirectory(zipFilePath, targetDirectoryPath);
			foreach (var filePath in Directory.GetFiles(targetDirectoryPath)) {
				string packageName = GetPackagePathFromZipFile(new FileInfo(filePath).Name, ".gz");
				string currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, packageName);
				Console.WriteLine("Start unzip package ({0}).", packageName);
				using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None)) {
					using (var zipStream = new GZipStream(fileStream, CompressionMode.Decompress, true)) {
						while (CompressionUtilities.UnzipFile(currentDirectoryPath, zipStream)) {
						}
					}
				}
				Console.WriteLine("Unzip package ({0}) completed.", packageName);
			}
		}

		private static string GetPackagePathFromZipFile(string filePath, string zipFileExtention) {
			string targetDirectoryPath = filePath.Remove(filePath.IndexOf(zipFileExtention,
				StringComparison.Ordinal), zipFileExtention.Length);
			return targetDirectoryPath;
		}

		internal static void CopyProjectElement(string sourcePath, string destinationPath, string name) {
			string fromAssembliesPath = Path.Combine(sourcePath, name);
			if (Directory.Exists(fromAssembliesPath)) {
				string toAssembliesPath = Path.Combine(destinationPath, name);
				CopyDir(fromAssembliesPath, toAssembliesPath);
			}
		}

		internal static void CopyDir(string source, string dest) {
			if (String.IsNullOrEmpty(source) || String.IsNullOrEmpty(dest))
				return;
			Directory.CreateDirectory(dest);
			foreach (string fn in Directory.GetFiles(source)) {
				File.Copy(fn, Path.Combine(dest, Path.GetFileName(fn)), true);
			}
			foreach (string dirFn in Directory.GetDirectories(source)) {
				CopyDir(dirFn, Path.Combine(dest, Path.GetFileName(dirFn)));
			}
		}

		private static string InstallPackage(string filePath) {
			string fileName = string.Empty;
			try {
				fileName = UploadPackage(filePath);
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return e.Message;
			}
			Console.WriteLine("Installing...");
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(InstallUrl);
			request.Timeout = 1800000;
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			request.ContentType = "application/json";
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write("\"" + fileName + "\"");
				}
			}
			try {
				using (WebResponse response = request.GetResponse()) {
					Console.WriteLine(((HttpWebResponse)response).StatusDescription);
					using (var dataStream = response.GetResponseStream()) {
						using (StreamReader reader = new StreamReader(dataStream)) {
							string responseFromServer = reader.ReadToEnd();
							Console.WriteLine(responseFromServer);
						}
					}
					Console.WriteLine("Installed");
				}
			} catch (Exception) {
				Console.WriteLine("Not installed");
			}
			var logText = GetLog();
			Console.WriteLine("Installation log");
			Console.WriteLine($"{logText}");
			return logText;
		}

		private static void DeletePackage(string code) {
			DeleteAppById(code);
		}

		private static string ExecutePostRequest(string url, string requestData) {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "POST";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			using (var requestStream = request.GetRequestStream()) {
				using (var writer = new StreamWriter(requestStream)) {
					writer.Write($"{requestData}");
				}
			}
			request.ContentType = "application/json";
			string responseFromServer = string.Empty;
			using (WebResponse response = request.GetResponse()) {
				Console.WriteLine(((HttpWebResponse)response).StatusDescription);
				using (var dataStream = response.GetResponseStream()) {
					using (StreamReader reader = new StreamReader(dataStream)) {
						responseFromServer = reader.ReadToEnd();
						Console.WriteLine(responseFromServer);
					}
				}
			}
			return responseFromServer;
		}

		private static void DeleteAppById(string id) {
			Console.WriteLine("Deleting...");
			string deleteRequestData = "\"" + id + "\"";
			ExecutePostRequest(UninstallAppUrl, deleteRequestData);
			Console.WriteLine("Deleted.");
		}

		private static string UploadPackage(string filePath) {
			Console.WriteLine("Uploading...");
			FileInfo fileInfo = new FileInfo(filePath);
			string fileName = fileInfo.Name;
			string boundary = DateTime.Now.Ticks.ToString("x");
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UploadUrl);
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			request.Method = "POST";
			request.KeepAlive = true;
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			Stream memStream = new MemoryStream();
			var boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
			var endBoundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--");
			string headerTemplate =
				"Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n" +
				"Content-Type: application/octet-stream\r\n\r\n";
			memStream.Write(boundarybytes, 0, boundarybytes.Length);
			var header = string.Format(headerTemplate, "files", fileName);
			var headerbytes = Encoding.UTF8.GetBytes(header);
			memStream.Write(headerbytes, 0, headerbytes.Length);
			using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
				var buffer = new byte[1024];
				var bytesRead = 0;
				while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0) {
					memStream.Write(buffer, 0, bytesRead);
				}
			}
			memStream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);
			request.ContentLength = memStream.Length;
			using (Stream requestStream = request.GetRequestStream()) {
				memStream.Position = 0;
				byte[] tempBuffer = new byte[memStream.Length];
				memStream.Read(tempBuffer, 0, tempBuffer.Length);
				memStream.Close();
				requestStream.Write(tempBuffer, 0, tempBuffer.Length);
			}
			Stream dataStream;
			WebResponse response = request.GetResponse();
			Console.WriteLine(((HttpWebResponse)response).StatusDescription);
			dataStream = response.GetResponseStream();
			StreamReader reader = new StreamReader(dataStream);
			string responseFromServer = reader.ReadToEnd();
			Console.WriteLine(responseFromServer);
			reader.Close();
			dataStream.Close();
			response.Close();
			Console.WriteLine("Uploaded");
			return fileName;
		}

		private static int Execute(ExecuteAssemblyOptions options) {
			Configure(options);
			Login();
			ExecuteScript(options);
			return 0;
		}

		private static int Register(RegisterOptions options) {
			var bpmcliEnv = new BpmcliEnvironment();
			string path = string.IsNullOrEmpty(options.Path) ? Environment.CurrentDirectory : options.Path;
			IResult result = options.Target == "m"
				? bpmcliEnv.MachineRegisterPath(path)
				: bpmcliEnv.UserRegisterPath(path);
			result.ShowMessagesTo(Console.Out);
			return 1;
		}

		private static int Restart(RestartOptions options) {
			try {
				options.Environment = options.Name;
				SetupAppConnection(options);
				UnloadAppDomain();
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static int ClearRedisDb(ClearRedisOptions options) {
			try {
				options.Environment = options.Name;
				SetupAppConnection(options);
				ClearRedisDbInternal();
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static int DownloadZipPackages(PullPkgOptions options) {
			try {
				SetupAppConnection(options);
				string destPath = options.DestPath != null
					? options.DestPath
					: Path.Combine(Path.GetTempPath(), "packages.zip");
				DownloadZipPackagesInternal(options.Name, destPath);
				UnZipPackages(destPath);
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static int Compression(GeneratePkgZipOptions options) {
			try {
				if (options.Packages == null) {
					var destinationPath = string.IsNullOrEmpty(options.DestinationPath) ? $"{options.Name}.gz" : options.DestinationPath;
					CompressionProject(options.Name, destinationPath);
				} else {
					var packages = GetPackages(options.Packages);
					string zipFileName = $"packages_{DateTime.Now.ToString("yy.MM.dd_hh.mm.ss")}.zip";
					var destinationPath = string.IsNullOrEmpty(options.DestinationPath) ? zipFileName : options.DestinationPath;
					CompressionProjects(options.Name, destinationPath, packages);
				}
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		private static int Install(PushPkgOptions options) {
			try {
				SetupAppConnection(options);
				if (options.Name == null) {
					options.Name = Environment.CurrentDirectory;
				}
				string logText = string.Empty;
				if (File.Exists(options.Name)) {
					logText = InstallPackage(options.Name);
				} else {
					if (Directory.Exists(options.Name)) {
						var folderPath = options.Name;
						var filePath = options.Name + ".gz";
						CompressionProject(folderPath, filePath);
						logText = InstallPackage(filePath);
						File.Delete(filePath);
					}
				}
				if (options.ReportPath != null) {
					SaveLogFile(logText, options.ReportPath);
				}
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		private static int Delete(DeletePkgOptions options) {
			try {
				SetupAppConnection(options);
				DeletePackage(options.Name);
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				return 1;
			}
		}

		private static string GetLog() {
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LogUrl);
			request.Timeout = 100000;
			request.Method = "GET";
			request.CookieContainer = AuthCookie;
			AddCsrfToken(request);
			request.ContentType = "application/json";
			string logText;
			using (WebResponse response = request.GetResponse()) {
				Console.WriteLine(((HttpWebResponse)response).StatusDescription);
				using (var dataStream = response.GetResponseStream()) {
					using (StreamReader reader = new StreamReader(dataStream)) {
						logText = reader.ReadToEnd();
					}
				}
			}
			return logText;
		}

		private static void SaveLogFile(string logText, string reportPath) {
			if (File.Exists(reportPath)) {
				File.Delete(reportPath);
			}
			File.WriteAllText(reportPath, logText, Encoding.UTF8);
		}

		private static int Main(string[] args) {
			var autoupdate = new SettingsRepository().GetAutoupdate();
			if (autoupdate) {
				new Thread(CheckUpdate).Start();
			}
			Parser.Default.Settings.ShowHeader = false;
			return Parser.Default.ParseArguments<ExecuteAssemblyOptions, RestartOptions, ClearRedisOptions, FetchOptions,
					RegAppOptions, AppListOptions, UnregAppOptions, GeneratePkgZipOptions, PushPkgOptions,
					DeletePkgOptions, ReferenceOptions, NewPkgOptions, ConvertOptions, RegisterOptions, PullPkgOptions,
					UpdateCliOptions, ExecuteSqlScriptOptions, InstallGateOptions>(args)
				.MapResult(
					(ExecuteAssemblyOptions opts) => Execute(opts),
					(RestartOptions opts) => Restart(opts),
					(ClearRedisOptions opts) => ClearRedisDb(opts),
					(FetchOptions opts) => Fetch(opts),
					(RegAppOptions opts) => ConfigureEnvironment(opts),
					(AppListOptions opts) => ViewEnvironments(opts),
					(UnregAppOptions opts) => UnregApplication(opts),
					(GeneratePkgZipOptions opts) => Compression(opts),
					(PushPkgOptions opts) => Install(opts),
					(DeletePkgOptions opts) => Delete(opts),
					(ReferenceOptions opts) => ReferenceTo(opts),
					(NewPkgOptions opts) => NewPkg(opts),
					(ConvertOptions opts) => ConvertPackage(opts),
					(RegisterOptions opts) => Register(opts),
					(PullPkgOptions opts) => DownloadZipPackages(opts),
					(UpdateCliOptions opts) => UpdateCli(),
					(ExecuteSqlScriptOptions opts) => ExecuteSqlScript(opts),
					(InstallGateOptions opts) => UpdateGate(opts),
					errs => 1);
		}

		private static int ConvertPackage(ConvertOptions opts) {
			return PackageConverter.Convert(opts);
		}

		private static int Fetch(FetchOptions opts) {
			try {
				SetupAppConnection(opts);
				var packages = GetPackages(opts.Name);
				foreach (var package in packages) {
					if (opts.Operation == "load") {
						DownloadPackages(package);
					} else {
						UploadPackages(package);
					}
				}
				Console.WriteLine("Done");
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			return 0;
		}

		private static int NewPkg(NewPkgOptions options) {
			var settings = new SettingsRepository().GetEnvironment();
			try {
				var packageName = options.Name;
				var packageDirectory = Directory.CreateDirectory(packageName);
				Directory.SetCurrentDirectory(packageDirectory.FullName);
				var pkg = BpmPkg.CreatePackage(options.Name, settings.Maintainer);
				pkg.Create();
				if (!String.IsNullOrEmpty(options.Rebase) && options.Rebase != "nuget") {
					ReferenceTo(new ReferenceOptions { ReferenceType = options.Rebase });
					pkg.RemovePackageConfig();
				}
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}

		private static int ReferenceTo(ReferenceOptions options) {
			options.Path = options.Path ?? CurrentProj;
			if (string.IsNullOrEmpty(options.Path)) {
				throw new ArgumentNullException(nameof(options.Path));
			}
			if (!string.IsNullOrEmpty(options.RefPattern)) {
				options.ReferenceType = "custom";
			}
			try {
				switch (options.ReferenceType) {
					case "bin": {
							BpmPkgProject.LoadFromFile(options.Path)
							.RefToBin()
							.SaveChanges();
						}
						break;
					case "src": {
							BpmPkgProject.LoadFromFile(options.Path)
							.RefToCoreSrc()
							.SaveChanges();
						}
						break;
					case "custom": {
							BpmPkgProject.LoadFromFile(options.Path)
								.RefToCustomPath(options.RefPattern)
								.SaveChanges();
						}
						break;
					case "unit-bin": {
							BpmPkgProject.LoadFromFile(options.Path)
								.RefToUnitBin()
								.SaveChanges();
						}
						break;
					case "unit-src": {
							BpmPkgProject.LoadFromFile(options.Path)
								.RefToUnitCoreSrc()
								.SaveChanges();
						}
						break;
					default: {
							throw new NotSupportedException($"You use not supported option type {options.ReferenceType}");
						}
				}
				Console.WriteLine("Done");
				return 0;
			} catch (Exception e) {
				Console.WriteLine(e);
				return 1;
			}
		}
	}
}
