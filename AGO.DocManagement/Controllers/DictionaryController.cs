﻿using System;
using System.Linq;
using AGO.Core.Attributes.Controllers;
using AGO.Core.Controllers;
using AGO.DocManagement.Model.Dictionary.Documents;
using AGO.Core;
using AGO.Core.Filters;
using AGO.Core.Json;
using AGO.Core.Modules.Attributes;
using Newtonsoft.Json;

namespace AGO.DocManagement.Controllers
{
	public class DictionaryController : AbstractController
	{
		#region Properties, fields, constructors

		public DictionaryController(
			IJsonService jsonService,
			IFilteringService filteringService,
			IJsonRequestService jsonRequestService,
			ICrudDao crudDao,
			IFilteringDao filteringDao,
			ISessionProvider sessionProvider,
			AuthController authController)
			: base(jsonService, filteringService, jsonRequestService, crudDao, filteringDao, sessionProvider, authController)
		{
		}

		#endregion

		#region Json endpoints

		[JsonEndpoint, RequireAuthorization]
		public object GetDocumentStatuses(JsonReader input)
		{
			var request = _JsonRequestService.ParseModelsRequest(input, DefaultPageSize, MaxPageSize);

			return new
			{
				totalRowsCount = _FilteringDao.RowCount<DocumentStatusModel>(request.Filters),
				rows = _FilteringDao.List<DocumentStatusModel>(request.Filters, OptionsFromRequest(request))
			};
		}

		[JsonEndpoint, RequireAuthorization]
		public DocumentStatusModel GetDocumentStatus(JsonReader input)
		{
			var request = _JsonRequestService.ParseModelRequest<Guid>(input);

			var filter = new ModelFilterNode { Operator = ModelFilterOperators.And };
			filter.AddItem(new ValueFilterNode
			{
				Path = "Id",
				Operator = ValueFilterOperators.Eq,
				Operand = request.Id.ToStringSafe()
			});

			return _FilteringDao.List<DocumentStatusModel>(
				new[] { filter }, OptionsFromRequest(request)).FirstOrDefault();
		}

		[JsonEndpoint, RequireAuthorization]
		public object GetDocumentCategories(JsonReader input)
		{
			var request = _JsonRequestService.ParseModelsRequest(input, DefaultPageSize, MaxPageSize);

			return new
			{
				totalRowsCount = _FilteringDao.RowCount<DocumentCategoryModel>(request.Filters),
				rows = _FilteringDao.List<DocumentCategoryModel>(request.Filters, OptionsFromRequest(request))
			};
		}

		[JsonEndpoint, RequireAuthorization]
		public DocumentCategoryModel GetDocumentCategory(JsonReader input)
		{
			var request = _JsonRequestService.ParseModelRequest<Guid>(input);

			var filter = new ModelFilterNode { Operator = ModelFilterOperators.And };
			filter.AddItem(new ValueFilterNode
			{
				Path = "Id",
				Operator = ValueFilterOperators.Eq,
				Operand = request.Id.ToStringSafe()
			});

			return _FilteringDao.List<DocumentCategoryModel>(
				new[] { filter }, OptionsFromRequest(request)).FirstOrDefault();
		}

		#endregion
	}
}