﻿using System;

namespace AGO.Core.Filters.Metadata
{
	internal class PropertyMetadata : IPropertyMetadata
	{
		public string Name { get; set; }

		public Type PropertyType { get; set; }
	}
}
