﻿using AGO.Core;
using AGO.Core.Controllers;
using AGO.Core.Filters;
using AGO.Core.Json;
using AGO.Core.Localization;
using AGO.Core.Model.Processing;

namespace AGO.Tasks.Controllers
{
    /// <summary>
    /// Контроллер работы с задачами модуля задач
    /// </summary>
    public class TasksController: AbstractController
    {
        public TasksController(
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
    }
}