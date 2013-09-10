﻿using System.Collections.Generic;

namespace AGO.Core.Modules
{
	public interface IModuleDescriptor
	{
		string Name { get; }

		string Alias { get; }

		int Priority { get; }

		IEnumerable<IServiceDescriptor> Services { get; }
	}
}