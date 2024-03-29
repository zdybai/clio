﻿using System;
using System.IO;
using clio.environment;
using Newtonsoft.Json;

namespace clio
{
	public class CreatioPackage
	{

		public const string DescriptorName = "descriptor.json";
		public const string PropertiesDirName = "Properties";
		public const string CsprojExtension = "csproj";
		public const string PackageConfigName = "packages.config";
		public const string AssemblyInfoName = "AssemblyInfo.cs";
		public const string PlaceholderFileName = "placeholder.txt";
		public static string EditProjTpl => $"tpl{Path.DirectorySeparatorChar}EditProj.{CsprojExtension}.tpl";
		public static string PackageConfigTpl => $"tpl{Path.DirectorySeparatorChar}{PackageConfigName}.tpl";
		public static string AssemblyInfoTpl => $"tpl{Path.DirectorySeparatorChar}{AssemblyInfoName}.tpl";

		private readonly string[] _pkgDirectories = {"Assemblies", "Data", "Schemas", "SqlScripts", "Resources", "Files", "Files\\cs" };

		private static string DescriptorTpl => $"tpl{Path.DirectorySeparatorChar}{DescriptorName}.tpl";
		private static string ProjTpl => $"tpl{Path.DirectorySeparatorChar}Proj.{CsprojExtension}.tpl";

		private readonly ICreatioEnvironment _creatioEnvironment;


		public string PackageName { get; }

		public string Maintainer { get; }

		public Guid ProjectId { get; protected set; }

		public string FullPath { get; protected set; }

		private DateTime _createdOn;
		public DateTime CreatedOn {
			get => _createdOn;
			protected set => _createdOn = GetDateTimeTillSeconds(value);
		}

		protected CreatioPackage(string packageName, string maintainer) {
			_creatioEnvironment = new CreatioEnvironment();
			PackageName = packageName;
			Maintainer = maintainer;
			CreatedOn = DateTime.UtcNow;
			FullPath = Environment.CurrentDirectory;
		}

		private static DateTime GetDateTimeTillSeconds(DateTime dateTime) {
			return dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond));
		}

		private static string ToJsonMsDate(DateTime date) {
			JsonSerializerSettings microsoftDateFormatSettings = new JsonSerializerSettings {
				DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
			};
			return JsonConvert.SerializeObject(date, microsoftDateFormatSettings).Replace("\"", "").Replace("\\", "");
		}

		private string ReplaceMacro(string text) {
			return text.Replace("$safeprojectname$", PackageName)
				.Replace("$userdomain$", Maintainer)
				.Replace("$guid1$", ProjectId.ToString())
				.Replace("$year$", CreatedOn.Year.ToString())
				.Replace("$modifiedon$", ToJsonMsDate(CreatedOn));
		}

		private bool GetTplPath(string tplPath, out string fullPath) {
			if (File.Exists(tplPath)) {
				fullPath = tplPath;
				return true;
			}
			var envPath = _creatioEnvironment.GetRegisteredPath();
			if (!string.IsNullOrEmpty(envPath)) {
				fullPath = Path.Combine(envPath, tplPath);
				return true;
			}
			fullPath = null;
			return false;
		}

		private bool CreateFromTpl(string tplPath, string filePath) {
			if (GetTplPath(tplPath, out string fullTplPath)) {
				var text = ReplaceMacro(File.ReadAllText(fullTplPath));
				FileInfo file = new FileInfo(filePath);
				using (StreamWriter sw = file.CreateText()) {
					sw.Write(text);
				}
				return true;
			}
			return false;
		}

		private void AddPlaceholderFile(string dirPath) {
			var placeholderPath = Path.Combine(dirPath, PlaceholderFileName);
			File.Create(placeholderPath).Dispose();
		}

		protected CreatioPackage CreatePkgDescriptor() {
			var filePath = Path.Combine(FullPath, DescriptorName);
			CreateFromTpl(DescriptorTpl, filePath);
			return this;
		}

		protected CreatioPackage CreateProj() {
			var filePath = Path.Combine(FullPath, PackageName + "." + CsprojExtension);
			CreateFromTpl(ProjTpl, filePath);
			return this;
		}

		protected CreatioPackage CreatePackageConfig() {
			var filePath = Path.Combine(FullPath, PackageConfigName);
			CreateFromTpl(PackageConfigTpl, filePath);
			return this;
		}

		protected CreatioPackage CreateEmptyClass() {
			var filePath = Path.Combine(FullPath, "Files\\cs", "EmptyClass.cs");
			File.Create(filePath).Dispose();
			return this;
		}

		protected CreatioPackage CreateAssemblyInfo() {
			Directory.CreateDirectory(Path.Combine(FullPath, PropertiesDirName));
			var filePath = Path.Combine(FullPath, PropertiesDirName, AssemblyInfoName);
			CreateFromTpl(AssemblyInfoTpl, filePath);
			return this;
		}

		protected CreatioPackage CreatePackageDirectories() {
			foreach (var directory in _pkgDirectories) {
				var dInfo = Directory.CreateDirectory(Path.Combine(FullPath, directory));
				AddPlaceholderFile(dInfo.FullName);
			}
			return this;
		}

		protected CreatioPackage CreatePackageFiles() {
			CreatePkgDescriptor()
				.CreateProj()
				.CreatePackageConfig()
				.CreateAssemblyInfo()
				.CreateEmptyClass();
			return this;
		}

		public static CreatioPackage CreatePackage(string name, string maintainer) {
			return new CreatioPackage(name, maintainer) {
				ProjectId = Guid.NewGuid(),
			};
		}

		public void Create() {
			CreatePackageDirectories().CreatePackageFiles();
		}

		internal void RemovePackageConfig()
		{
			var filePath = Path.Combine(FullPath, PackageConfigName);
			File.Delete(filePath);
		}
	}
}
