﻿using System.Collections.Generic;
using SimpleInjector;
using AGO.Core.Config;
using AGO.Core.Json;
using AGO.Core.Localization;
using AGO.Core.Modules;

namespace AGO.Core.Application
{
	public interface IApplication
	{
		Container IocContainer { get; }

		IKeyValueProvider KeyValueProvider { get; set; }

		IEnvironmentService EnvironmentService { get; }

		IJsonService JsonService { get; }

		ILocalizationService LocalizationService { get; }

		IList<IModuleDescriptor> ModuleDescriptors { get; }

		void Initialize();
	}
}