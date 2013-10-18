﻿using System;
using AGO.Core.Model.Dictionary;
using AGO.Core.Model.Security;
using AGO.Core.Attributes.Constraints;
using Newtonsoft.Json;

namespace AGO.Home.Model.Dictionary.Projects
{
	public class ProjectStatusModel : SecureProjectBoundModel<Guid>, IDictionaryItemModel, IHomeModel
	{
		#region Persistent

		[UniqueProperty, NotEmpty, NotLonger(64), JsonProperty]
		public virtual string Name { get; set; }

		[NotLonger(512), JsonProperty]
		public virtual new string Description { get; set; }

		[JsonProperty]
		public virtual bool IsInitial { get; set; }

		[JsonProperty]
		public virtual bool IsFinal { get; set; }

		#endregion

		#region Non-persistent

		public override string ToString()
		{
			return Name;
		}

		#endregion
	}
}
