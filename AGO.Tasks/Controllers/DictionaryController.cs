﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AGO.Core;
using AGO.Core.Attributes.Constraints;
using AGO.Core.Attributes.Controllers;
using AGO.Core.Controllers;
using AGO.Core.Filters;
using AGO.Core.Filters.Metadata;
using AGO.Core.Json;
using AGO.Core.Localization;
using AGO.Core.Model.Dictionary;
using AGO.Core.Model.Processing;
using AGO.Core.Modules.Attributes;
using AGO.Tasks.Controllers.DTO;
using AGO.Tasks.Model.Dictionary;
using AGO.Tasks.Model.Task;
using NHibernate.Criterion;


namespace AGO.Tasks.Controllers
{
	/// <summary>
    /// Контроллер справочников модуля задач
    /// </summary>
    public class DictionaryController: AbstractTasksController
    {
        public DictionaryController(
            IJsonService jsonService, 
            IFilteringService filteringService,
            ICrudDao crudDao, 
            IFilteringDao filteringDao,
			ISessionProvider sessionProvider,
			ILocalizationService localizationService,
			IModelProcessingService modelProcessingService,
			AuthController authController)
			: base(jsonService, filteringService, crudDao, filteringDao, sessionProvider, localizationService, modelProcessingService, authController)
		{
		}

		private static IDictionary<string, LookupEntry[]> taskStatuses;
		private static IDictionary<string, LookupEntry[]> taskPriorities;

		private IEnumerable<LookupEntry> LookupEnum<TEnum>(
			string term, 
			int page,
			ref IDictionary<string, LookupEntry[]> cache)
		{
			if (page > 0) return Enumerable.Empty<LookupEntry>(); //while size of enum less than defaul page size (10)

			var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
			if (cache == null)
			{
				//no need to locking - replace with same value from another thread has no negative effect
				cache = new Dictionary<string, LookupEntry[]>();
			}
			if (!cache.ContainsKey(lang))
			{
				//no need to locking - replace with same value from another thread has no negative effect
				cache[lang] = Enum.GetValues(typeof(TEnum))
					.OfType<TEnum>() //GetValues preserve enum order, no OrderBy used
					.Select(s => new LookupEntry 
								{
									Id = s.ToString(),
									Text = (_LocalizationService.MessageForType(s.GetType(), s) ?? s.ToString())
								})
					.ToArray();
			}

			if (term.IsNullOrWhiteSpace())
				return cache[lang];

			return cache[lang]
				.Where(l => l.Text.IndexOf(term, StringComparison.InvariantCultureIgnoreCase) >= 0)
				.ToArray();
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<LookupEntry> LookupTaskStatuses(string term, [InRange(0, null)] int page)
		{
			return LookupEnum<TaskStatus>(term, page, ref taskStatuses);
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<LookupEntry> LookupTaskPriorities(string term, [InRange(0, null)] int page)
		{
			return LookupEnum<TaskPriority>(term, page, ref taskPriorities);
		}
			
		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<LookupEntry> LookupTaskTypes(
			[NotEmpty] string project,
			string term,
			[InRange(0, null)] int page)
		{
			return Lookup<TaskTypeModel>(project, term, page, m => m.Name);
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<TaskTypeDTO> GetTaskTypes(
			[NotEmpty] string project,
			[NotNull] ICollection<IModelFilterNode> filter,
			[NotNull] ICollection<SortInfo> sorters, 
			[InRange(0, null)] int page)
		{
			var projectPredicate = _FilteringService.Filter<TaskTypeModel>().Where(m => m.ProjectCode == project);
			var predicate = filter.Concat(new[] {projectPredicate}).ToArray();
			var adapter = new TaskTypeAdapter();

			return _FilteringDao.List<TaskTypeModel>(predicate, page, sorters)
				.Select(adapter.Fill)
				.ToArray();
		}

		[JsonEndpoint, RequireAuthorization]
		public int GetTaskTypesCount([NotEmpty] string project, [NotNull] ICollection<IModelFilterNode> filter)
		{
			var projectPredicate = _FilteringService.Filter<TaskTypeModel>().Where(m => m.ProjectCode == project);
			var predicate = filter.Concat(new[] { projectPredicate }).ToArray();

			return _FilteringDao.RowCount<TaskTypeModel>(predicate);
		}

		[JsonEndpoint, RequireAuthorization]
		public UpdateResult<TaskTypeDTO> EditTaskType([NotEmpty] string project, [NotNull] TaskTypeDTO model)
		{
			return Edit<TaskTypeModel, TaskTypeDTO>(model.Id, project, 
				(taskType, vr) => { taskType.Name = model.Name.TrimSafe(); },
				taskType => new TaskTypeAdapter().Fill(taskType));
		}

    	private void InternalDeleteTaskType(Guid id)
    	{
    		var taskType = _CrudDao.Get<TaskTypeModel>(id, true);

    		if (_CrudDao.Exists<TaskModel>(q => q.Where(m => m.TaskType == taskType)))
    			throw new CannotDeleteReferencedItemException();

    		_CrudDao.Delete(taskType);
    	}

		[JsonEndpoint, RequireAuthorization]
		public bool DeleteTaskType([NotEmpty] Guid id)
		{
			InternalDeleteTaskType(id);

			return true;
		}

		//TODO transaction management???
    	[JsonEndpoint, RequireAuthorization]
		public bool DeleteTaskTypes([NotEmpty] string project, [NotNull] ICollection<Guid> ids, Guid? replacementTypeId)
    	{
			if (replacementTypeId.HasValue && ids.Contains(replacementTypeId.Value))
				throw new CanNotReplaceWithItemThatWillBeDeletedTo();

    		var s = _SessionProvider.CurrentSession;
    		var trn = s.BeginTransaction();
    		try
    		{
    			const string hqlUpdate =
    				"update versioned TaskModel set TaskTypeId = :newTypeId where ProjectCode = :project and TaskTypeId = :oldTypeId";
    			var updateQuery = s.CreateQuery(hqlUpdate);

    			foreach (var id in ids)
    			{
    				if (replacementTypeId.HasValue)
    				{
    					updateQuery
    						.SetGuid("newTypeId", replacementTypeId.Value)
    						.SetString("project", project)
    						.SetGuid("oldTypeId", id)
    						.ExecuteUpdate();
    				}

					InternalDeleteTaskType(id);
    			}

    			trn.Commit();
    		}
    		catch (Exception)
    		{
				trn.Rollback();
    			throw;
    		}
			return true;
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<IModelMetadata> TaskTypeMetadata()
		{
			return MetadataForModelAndRelations<TaskTypeModel>();
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<TaskTagLookupEntry> LookupTags(
			[NotEmpty] string project, 
			string term,
			[InRange(0, null)] int page)
		{
			//доступны только проектные теги либо персональные текущего пользователя
			var query = _SessionProvider.CurrentSession.QueryOver<TaskTagModel>()
				.Where(m => m.ProjectCode == project && (m.Owner == null || m.Owner.Id == _AuthController.CurrentUser().Id));
			if (!term.IsNullOrWhiteSpace())
			{
				foreach (var termPart in term.Split(new []{',', ' ', '\\'}, StringSplitOptions.RemoveEmptyEntries))
				{
					query = query.WhereRestrictionOn(m => m.FullName).IsLike(termPart, MatchMode.Anywhere);
				}
			}
			query = query.OrderBy(m => m.FullName).Asc;

			return _CrudDao.PagedQuery(query, page)
				.List<TaskTagModel>()
				.Select(m => new TaskTagLookupEntry {Id = m.Id.ToString(), Text = m.FullName, Personal = m.Owner != null})
				.ToArray();
		}

		[JsonEndpoint, RequireAuthorization]
		public IEnumerable<TaskTagDTO> GetTags(
			[NotEmpty] string project,
			[NotNull] ICollection<IModelFilterNode> filter,
			[NotNull] ICollection<SortInfo> sorters, 
			[InRange(0, null)] int page)
		{
			var projectPredicate = _FilteringService.Filter<TaskTagModel>().Where(m => m.ProjectCode == project);
			var predicate = filter.Concat(new[] { projectPredicate }).ToArray();
			var adapter = new TaskTagAdapter();

			return _FilteringDao.List<TaskTagModel>(predicate, page, sorters)
				.Select(adapter.Fill)
				.ToArray();
		}

		private TaskTagModel FindOrCreate(string project, string[] path, bool personal, ref List<TaskTagModel> created)
		{
			var queryTpl = Session.QueryOver<TaskTagModel>()
				.Where(m => m.ProjectCode == project);
			queryTpl = personal
				? queryTpl.Where(m => m.Owner.Id == _AuthController.CurrentUser().Id)
				: queryTpl.WhereRestrictionOn(m => m.Owner).IsNull();
			var parent = queryTpl.Where(m => m.FullName == string.Join("\\", path)).SingleOrDefault();
			if (parent != null) return parent; //already exists

			string currentPath = null;
			TaskTagModel currentParent = null;
			for(var i = 0; i < path.Length; i++)
			{
				currentPath = i == 0 ? path[i].TrimSafe() : currentPath + "\\" + path[i].TrimSafe();
				var cpath = currentPath;
				parent = queryTpl.Where(m => m.FullName == cpath).SingleOrDefault();
				
				if (parent == null)
				{
					parent = new TaskTagModel
					                	{
					                		ProjectCode = project,
					                		Creator = _AuthController.CurrentUser(),
					                		Owner = personal ? _AuthController.CurrentUser() : null,
											Parent = currentParent,
											Name = path[i].TrimSafe(),
					                		FullName = currentPath
					                	};
					_CrudDao.Store(parent);
					created.Add(parent);
				}
				currentParent = parent;
			}
			return currentParent;
		}

		private List<TaskTagModel> UpdateTagNameAndParent(string project, TaskTagDTO model, bool personal, TaskTagModel tag)
		{
			var createdParents = new List<TaskTagModel>();
			var parts = model.Name.TrimSafe().Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 1)
			{
				tag.FullName = tag.Name = parts[0];
			}
			else if (parts.Length > 1)
			{
				tag.Parent = FindOrCreate(project, parts.Take(parts.Length - 1).ToArray(), personal, ref createdParents);
				tag.Name = parts[parts.Length - 1];
				tag.FullName = tag.Parent.FullName + "\\" + tag.Name;
			}
			return createdParents;
		}

		[JsonEndpoint, RequireAuthorization]
		public UpdateResult<TaskTagDTO[]> CreateTag([NotEmpty] string project, [NotNull] TaskTagDTO model, bool personal = true)
		{
			var adapter = new TaskTagAdapter();
			var result = new UpdateResult<TaskTagDTO[]> {Model = new TaskTagDTO[0]};
			List<TaskTagModel> createdParents = null;
			var res = Edit<TaskTagModel, TaskTagDTO>(model.Id, project,
				(tag, vr) =>
					{
						tag.Owner = personal ? _AuthController.CurrentUser() : null;
						createdParents = UpdateTagNameAndParent(project, model, personal, tag);
					},
				adapter.Fill);
			result.Validation = res.Validation;
			if (!result.Validation.Success) return result;

			result.Model = new[] {res.Model}.Concat(
				createdParents.Select(adapter.Fill)).ToArray();
			return result;
		}

		[JsonEndpoint, RequireAuthorization]
		public UpdateResult<TaskTagDTO[]> EditTag([NotEmpty] string project, [NotNull] TaskTagDTO model)
		{
			var adapter = new TaskTagAdapter();
			TaskTagModel updatedTag = null;
			List<TaskTagModel> createdParents = null;
			var result = new UpdateResult<TaskTagDTO[]> {Model = new TaskTagDTO[0]};
			var mainTagRes = Edit<TaskTagModel, TaskTagDTO>(model.Id, project,
				(tag, vr) =>
					{
						createdParents = UpdateTagNameAndParent(project, model, tag.Owner != null, tag);
						updatedTag = tag;
					},
				adapter.Fill,
				() => { throw new TagCreationNotSupportedException(); });

			result.Validation = mainTagRes.Validation;
			if (!result.Validation.Success)
			{
				//error when updating, return now
				return result;
			}

			var defenceCounter = 0;
			var forUpdateOnClient = new List<TagModel>();
			if (updatedTag.Children.Any())
			{
				//same algorithm to obtain the hierarchy, which is widely used in sql
				var forUpdate = updatedTag.Children.ToList();
				while (forUpdate.Any())
				{
					//recalc full name for current hierarchy level
					foreach (var tag in forUpdate)
					{
						tag.FullName = tag.Parent.FullName + "\\" + tag.Name;
						_CrudDao.Store(tag);
						forUpdateOnClient.Add(tag);
					}
					//grab next hierarchy level
					forUpdate = forUpdate.SelectMany(tag => tag.Children).ToList();

					defenceCounter++;
					if (defenceCounter > 100) //unreachable level of nesting for tags
						throw new InvalidOperationException("Too more nesting levels in tag hierarchy. Possible cycle in graph.");
				}
			}

			result.Model = new[] {mainTagRes.Model}
				.Concat(createdParents.Select(adapter.Fill))
				.Concat(forUpdateOnClient.Cast<TaskTagModel>().Select(adapter.Fill)).ToArray();

			return result;
		}
    }
}