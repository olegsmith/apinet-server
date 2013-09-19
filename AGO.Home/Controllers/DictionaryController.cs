﻿using System;
using System.Collections.Generic;
using System.Text;
using AGO.Core.Attributes.Constraints;
using AGO.Core.Attributes.Controllers;
using AGO.Core.Controllers;
using AGO.Core;
using AGO.Core.Filters;
using AGO.Core.Filters.Metadata;
using AGO.Core.Json;
using AGO.Core.Model.Dictionary;
using AGO.Core.Model.Security;
using AGO.Core.Modules.Attributes;
using AGO.Home.Model.Dictionary.Projects;
using AGO.Home.Model.Projects;
using NHibernate.Criterion;

namespace AGO.Home.Controllers
{
	public enum TagsRequestMode
	{
		Personal,
		Common
	}

	public class DictionaryController : AbstractController
	{
		#region Properties, fields, constructors

		public DictionaryController(
			IJsonService jsonService,
			IFilteringService filteringService,
			ICrudDao crudDao,
			IFilteringDao filteringDao,
			ISessionProvider sessionProvider,
			AuthController authController)
			: base(jsonService, filteringService, crudDao, filteringDao, sessionProvider, authController)
		{
		}

		#endregion

		#region Json endpoints

		[JsonEndpoint, RequireAuthorization]
		public object LookupProjectStatuses(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			string term)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			var query = _SessionProvider.CurrentSession.QueryOver<ProjectStatusModel>();
			if (!term.IsNullOrWhiteSpace())
				query = query.WhereRestrictionOn(m => m.Name).IsLike(term, MatchMode.Anywhere);

			var result = new List<object>();

			foreach (var model in query.Skip(page * pageSize).Take(pageSize).List())
				result.Add(new {id = model.Id, text = model.Name});

			return new {rows = result};
		}

		[JsonEndpoint, RequireAuthorization]
		public object LookupProjectStatusDescriptions(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			string term)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			var query = _SessionProvider.CurrentSession.QueryOver<ProjectStatusModel>()
				.Select(Projections.Distinct(Projections.Property("Description")));
			if (!term.IsNullOrWhiteSpace())
				query = query.WhereRestrictionOn(m => m.Description).IsLike(term, MatchMode.Anywhere);

			var result = new List<object>();

			foreach (var str in query.Skip(page * pageSize).Take(pageSize).List<string>())
				result.Add(new { id = str, text = str });

			return new { rows = result };
		}

		[JsonEndpoint, RequireAuthorization]
		public object GetProjectStatuses(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			[NotNull] ICollection<IModelFilterNode> filter,
			[NotNull] ICollection<SortInfo> sorters)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			return new
			{
				totalRowsCount = _FilteringDao.RowCount<ProjectStatusModel>(filter),
				rows = _FilteringDao.List<ProjectStatusModel>(filter, new FilteringOptions
				{
					Skip = page * pageSize,
					Take = pageSize,
					Sorters = sorters
				})
			};
		}

		[JsonEndpoint, RequireAuthorization]
		public ProjectStatusModel GetProjectStatus([NotEmpty] Guid id, bool dontFetchReferences)
		{
			return GetModel<ProjectStatusModel, Guid>(id, dontFetchReferences);
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<IModelMetadata> ProjectStatusMetadata()
		{
			return MetadataForModelAndRelations<ProjectStatusModel>();
		}

		[JsonEndpoint, RequireAuthorization(true)]
		public ValidationResult EditProjectStatus([NotNull] ProjectStatusModel model)
		{
			var validationResult = new ValidationResult();

			try
			{
				var persistentModel = default(Guid).Equals(model.Id)
					? new ProjectStatusModel { Creator = _AuthController.GetCurrentUser() }
					: _CrudDao.Get<ProjectStatusModel>(model.Id, true);

				var name = model.Name.TrimSafe();
				if (name.IsNullOrEmpty())
					validationResult.FieldErrors["Name"] = new RequiredFieldException().Message;
				if (_SessionProvider.CurrentSession.QueryOver<ProjectStatusModel>()
						.Where(m => m.Name == name && m.Id != model.Id).RowCount() > 0)
					validationResult.FieldErrors["Name"] = new UniqueFieldException().Message;

				if (!validationResult.Success)
					return validationResult;

				persistentModel.Name = name;
				persistentModel.Description = model.Description.TrimSafe();
				_CrudDao.Store(persistentModel);
			}
			catch (Exception e)
			{
				validationResult.GeneralError = e.Message;
			}

			return validationResult;
		}

		[JsonEndpoint, RequireAuthorization(true)]
		public void DeleteProjectStatus([NotEmpty] Guid id)
		{
			var model = _CrudDao.Get<ProjectStatusModel>(id, true);
			
			if (_SessionProvider.CurrentSession.QueryOver<ProjectModel>()
					.Where(m => m.Status == model).RowCount() > 0)
				throw new CannotDeleteReferencedItemException();

			if (_SessionProvider.CurrentSession.QueryOver<ProjectStatusHistoryModel>()
					.Where(m => m.Status == model).RowCount() > 0)
				throw new CannotDeleteReferencedItemException();

			_CrudDao.Delete(model);
		}

		[JsonEndpoint, RequireAuthorization]
		public object GetProjectTags(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			[NotNull] ICollection<IModelFilterNode> filter,
			[NotNull] ICollection<SortInfo> sorters,
			TagsRequestMode mode)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			IModelFilterNode modeFilter = new ModelFilterNode();
			if (mode == TagsRequestMode.Personal)
			{
				modeFilter.AddItem(new ValueFilterNode
				{
					Path = "Owner", 
					Operator = ValueFilterOperators.Eq, 
					Operand = _AuthController.GetCurrentUser().Id.ToString()
				});
			}
			else
			{
				modeFilter.AddItem(new ValueFilterNode
				{
					Path = "Owner",
					Operator = ValueFilterOperators.Exists,
					Negative = true
				});
			}
			filter.Add(modeFilter);

			return new
			{
				totalRowsCount = _FilteringDao.RowCount<ProjectTagModel>(filter),
				rows = _FilteringDao.List<ProjectTagModel>(filter, new FilteringOptions
				{
					Skip = page * pageSize,
					Take = pageSize,
					Sorters = sorters
				})
			};
		}

		[JsonEndpoint, RequireAuthorization]
		public ProjectTagModel GetProjectTag([NotEmpty] Guid id, bool dontFetchReferences)
		{
			return GetModel<ProjectTagModel, Guid>(id, dontFetchReferences);
		}

		[JsonEndpoint, RequireAuthorization]
		public object LookupProjectTags(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			string term)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			var query = _SessionProvider.CurrentSession.QueryOver<ProjectTagModel>();
			if (!term.IsNullOrWhiteSpace())
				query = query.WhereRestrictionOn(m => m.Name).IsLike(term, MatchMode.Anywhere);

			var result = new List<object>();

			foreach (var model in query.Skip(page * pageSize).Take(pageSize).List())
				result.Add(new { id = model.Id, text = model.Name });

			return new { rows = result };
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<IModelMetadata> ProjectTagMetadata()
		{
			return MetadataForModelAndRelations<ProjectTagModel>();
		}

		[JsonEndpoint, RequireAuthorization]
		public ValidationResult EditProjectTag([NotNull] ProjectTagModel model, TagsRequestMode mode)
		{
			var validationResult = new ValidationResult();

			try
			{
				var currentUser = _AuthController.GetCurrentUser();				
				var persistentModel = default(Guid).Equals(model.Id) 
					? new ProjectTagModel
						{
							Creator = currentUser,
							Owner = mode == TagsRequestMode.Personal ? currentUser : null
						}	
					: _CrudDao.Get<ProjectTagModel>(model.Id, true);

				if (persistentModel.Owner != null && !currentUser.Equals(persistentModel.Owner) && currentUser.SystemRole != SystemRole.Administrator)
					throw new AccessForbiddenException();

				var name = model.Name.TrimSafe();
				if (name.IsNullOrEmpty())
					validationResult.FieldErrors["Name"] = new RequiredFieldException().Message;
				if (_SessionProvider.CurrentSession.QueryOver<ProjectTagModel>().Where(
						m => m.Name == name && m.Owner == model.Owner && m.Id != model.Id).RowCount() > 0)
					validationResult.FieldErrors["Name"] = new UniqueFieldException().Message;

				if (!validationResult.Success)
					return validationResult;

				persistentModel.Name = name;

				var parentsStack = new Stack<TagModel>();

				var current = persistentModel as TagModel;
				while (current != null)
				{
					parentsStack.Push(current);
					current = current.Parent;
				}

				var fullName = new StringBuilder();
				while (parentsStack.Count > 0)
				{
					current = parentsStack.Pop();
					if (fullName.Length > 0)
						fullName.Append(" / ");
					fullName.Append(current.Name);
				}
				persistentModel.FullName = fullName.ToString();

				_CrudDao.Store(persistentModel);
			}
			catch (Exception e)
			{
				validationResult.GeneralError = e.Message;
			}
			
			return validationResult;
		}

		[JsonEndpoint, RequireAuthorization]
		public void DeleteProjectTag([NotEmpty] Guid id)
		{
			var model = _CrudDao.Get<ProjectTagModel>(id, true);

			var currentUser = _AuthController.GetCurrentUser();
			if (model.Owner != null && !currentUser.Equals(model.Owner) && currentUser.SystemRole != SystemRole.Administrator)
				throw new AccessForbiddenException();

			if (_SessionProvider.CurrentSession.QueryOver<ProjectToTagModel>()
					.Where(m => m.Tag == model).RowCount() > 0)
				throw new CannotDeleteReferencedItemException();

			_CrudDao.Delete(model);
		}

		[JsonEndpoint, RequireAuthorization]
		public object GetProjectTypes(
			[InRange(0, null)] int page,
			[InRange(0, MaxPageSize)] int pageSize,
			[NotNull] ICollection<IModelFilterNode> filter,
			[NotNull] ICollection<SortInfo> sorters)
		{
			pageSize = pageSize == 0 ? DefaultPageSize : pageSize;

			return new
			{
				totalRowsCount = _FilteringDao.RowCount<ProjectTypeModel>(filter),
				rows = _FilteringDao.List<ProjectTypeModel>(filter, new FilteringOptions
				{
					Skip = page * pageSize,
					Take = pageSize,
					Sorters = sorters
				})
			};
		}

		[JsonEndpoint, RequireAuthorization]
		public ProjectTypeModel GetProjectType([NotEmpty] Guid id, bool dontFetchReferences)
		{
			return GetModel<ProjectTypeModel, Guid>(id, dontFetchReferences);
		}

		#endregion
	}
}