namespace cliogate.Files.cs.Feature
{
	#if NETFRAMEWORK
	using System;
	using System.Collections.Generic;
	using System.ServiceModel;
	using System.ServiceModel.Activation;
	using System.ServiceModel.Web;
	using ATF.Repository;
	using ATF.Repository.Attributes;
	using Terrasoft.Core;
	using Terrasoft.Core.Store;

	using Terrasoft.Web.Common;

#region Class: Feature

	[Schema("Feature")]
	public class Feature : BaseModel
	{
		[SchemaProperty("Name")]
		public string Name { get; set; }

		[SchemaProperty("Description")]
		public string Description { get; set; }

		[SchemaProperty("Code")]
		public string Code { get; set; }

		[DetailProperty("FeatureId")]
		public virtual List<UserFeatureState> UsersFeatureState { get; set; }
	}

#endregion

#region Class: UserFeatureState

	[Schema("AdminUnitFeatureState")]
	public class UserFeatureState : BaseModel
	{
		[SchemaProperty("SysAdminUnit")]
		public Guid SysAdminUnitId { get; set; }

		[SchemaProperty("Feature")]
		public Guid FeatureId { get; set; }

		[SchemaProperty("FeatureState")]
		public int FeatureState { get; set; }
	}

#endregion

#region Class: FeatureRepository

	/// <summary>
	/// Provides utilities methods for work with features.
	/// </summary>
	public class FeatureRepository
	{

#region Constants: Private

		private const string FeaturesCacheGroupItem = "FeatureInitializing_{0}_{1}";
		private const string AllUsers = "a29a3ba5-4b0d-de11-9a51-005056c00008";
		private const string AllPortalUsers = "720b771c-e7a7-4f31-9cfb-52cd21c3739f";

#endregion

		protected IRepository Repository { get; }

		protected UserConnection UserConnection { get; }

		public FeatureRepository(UserConnection userConnection) {
			Repository = new Repository {
				UserConnection = UserConnection = userConnection
			};
		}

#region Methods: Private

		/// <summary>
		/// Clears cache of the feature by <paramref name="code"/>.
		/// </summary>
		/// <param name="code">Feature code.</param>
		private void ClearFeatureCache(string code) {
			var itemKey = string.Format(FeaturesCacheGroupItem, code, UserConnection.CurrentUser.Id);
			UserConnection.SessionCache.WithLocalCaching().SetOrRemoveValue(itemKey, null);
		}

		/// <summary>
		/// Returns feature identifier by <paramref name="code"/>.
		/// </summary>
		/// <param name="code">Feature code.</param>
		/// <returns>Feature identifier.</returns>
		private Guid GetFeatureByCode(string code) {
			var manager = UserConnection.EntitySchemaManager.GetInstanceByName("Feature");
			var entity = manager.CreateEntity(UserConnection);
			if (entity.FetchFromDB(new Dictionary<string, object> {
				{"Code", code}
			})) {
				return entity.PrimaryColumnValue;
			}
			return Guid.Empty;
		}

		private Guid ExtractFeatureId(string code) {
			ClearFeatureCache(code);
			var featureId = GetFeatureByCode(code);
			return featureId;
		}

		private UserFeatureState CreateNewFeatureState(Guid userId, Guid featureId, int state) {
			var userFeatureState = Repository.CreateItem<UserFeatureState>();
			userFeatureState.SysAdminUnitId = userId;
			userFeatureState.FeatureId = featureId;
			userFeatureState.FeatureState = state;
			return userFeatureState;
		}

		private Feature CreateNewFeature(string code, int state) {
			Feature feature = Repository.CreateItem<Feature>();
			feature.Code = feature.Name = code;
			feature.UsersFeatureState.Add(CreateNewFeatureState(Guid.Parse(AllUsers), feature.Id, state));
			feature.UsersFeatureState.Add(CreateNewFeatureState(Guid.Parse(AllPortalUsers), feature.Id, state));
			feature.UsersFeatureState.Add(CreateNewFeatureState(UserConnection.CurrentUser.Id, feature.Id, state));
			return feature;
		}

#endregion

#region Methods: Public

		/// <summary>
		/// Create Feature if it does not exist and sets feature state.
		/// </summary>
		/// <param name="code">Feature code.</param>
		/// <param name="state">New feature state.</param>
		public void SetFeatureState(string code, int state) {
			Guid featureId = ExtractFeatureId(code);
			if (featureId == Guid.Empty) {
				CreateNewFeature(code, state);
			} else {
				var feature = Repository.GetItem<Feature>(featureId);
				feature.UsersFeatureState.ForEach(item => item.FeatureState = state);
			}
			Repository.Save();
		}

#endregion

	}

#endregion
	
	/// <summary>
	/// Provides web-service for work with features.
	/// </summary>
	[ServiceContract]
	[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
	public class FeatureStateService : BaseService
	{
		/// <summary>
		/// Sets feature state for current user.
		/// </summary>
		/// <param name="code">Feature code.</param>
		/// <param name="state">New feature state.</param>
		[OperationContract]
		[WebInvoke(Method = "POST", UriTemplate = "SetFeatureState", BodyStyle = WebMessageBodyStyle.Wrapped,
			RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
		public void SetFeatureState(string code, int state) {
			if (UserConnection.DBSecurityEngine.GetCanExecuteOperation("CanManageSolution")) {
				var featureRepo = new FeatureRepository(UserConnection);
				featureRepo.SetFeatureState(code, state);
			} else {
				throw new Exception("You don`n have permission for operation CanManageSolution");
			}
		}
	}
	#endif
}
