﻿using System.Collections.Generic;
using AGO.Core.Modules;
using AGO.Home.Controllers;

namespace AGO.Home
{
	public class ModuleDescriptor : IModuleDescriptor
	{
		public string Name { get { return "AGO.Home"; } }

		public string Alias { get { return "Home"; } }

		public int Priority { get { return 0; } }

		public IEnumerable<IServiceDescriptor> Services { get; private set; }

		public ModuleDescriptor()
		{
			Services = new List<IServiceDescriptor>
			{
				new AttributedServiceDescriptor<DictionaryController>(this),
				new AttributedServiceDescriptor<ProjectsController>(this),
				new AttributedServiceDescriptor<ConfigController>(this),
				new AttributedServiceDescriptor<UsersController>(this)
			};
		}
	}
}
