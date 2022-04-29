namespace Clio.Workspace
{
	using System;

	#region Interface: IWorkspaceRestorer

	public interface IWorkspaceRestorer
	{

		#region Methods: Public

		void Restore(string rootPath, Version nugetCreatioSdkVersion);

		#endregion

	}

	#endregion

}