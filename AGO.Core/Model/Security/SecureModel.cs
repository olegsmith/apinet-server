﻿using System;
using AGO.Core.Attributes.Constraints;
using AGO.Core.Attributes.Mapping;
using AGO.Core.Attributes.Model;
using Newtonsoft.Json;

namespace AGO.Core.Model.Security
{
	public abstract class SecureModel<TIdType> : CoreModel<TIdType>, ISecureModel
	{
		#region Persistent

		[NotNull]
		public virtual UserModel Creator { get; set; }
		[ReadOnlyProperty, MetadataExclude]
		public virtual Guid? CreatorId { get; set; }

		[JsonProperty]
		public virtual DateTime? LastChangeTime { get; set; }

		public virtual UserModel LastChanger { get; set; }
		[ReadOnlyProperty, MetadataExclude]
		public virtual Guid? LastChangerId { get; set; }
		
		#endregion
	}
}
