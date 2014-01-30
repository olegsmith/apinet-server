﻿using System;
using AGO.Core.Attributes.Constraints;
using AGO.Core.Attributes.Mapping;
using AGO.Core.Attributes.Model;
using AGO.Core.Model.Projects;
using AGO.Core.Model.Security;
using AGO.Reporting.Common.Model;
using Newtonsoft.Json;

namespace AGO.Core.Model.Reporting
{
	/// <summary>
	/// Модель задачи на создание отчета
	/// </summary>
	[LazyLoad]
	public class ReportTaskModel: SecureModel<Guid>, IReportTask
	{
		#region Persistent

		[NotEmpty, NotLonger(ProjectModel.PROJECT_CODE_SIZE), JsonProperty]
		public virtual string Project { get; set; }

		[NotEmpty, NotLonger(250), JsonProperty]
		public virtual string Name { get; set; }

		[NotNull]
		public virtual ReportSettingModel ReportSetting { get; set; }
		[ReadOnlyProperty]
		public virtual Guid? ReportSettingId { get; set; }

		public virtual string Parameters { get; set; }
		
		[MetadataExclude]
		public virtual string Culture { get; set; }

		[JsonProperty]
		public virtual ReportTaskState State { get; set; }

		[InRange(0, 100), JsonProperty]
		public virtual byte DataGenerationProgress { get; set; }

		[InRange(0, 100), JsonProperty]
		public virtual byte ReportGenerationProgress { get; set; }

		[JsonProperty]
		public virtual DateTime? StartedAt { get; set; }

		[JsonProperty]
		public virtual DateTime? CompletedAt { get; set; }

		[JsonProperty]
		public virtual string ErrorMsg { get; set; }

		[JsonProperty]
		public virtual string ErrorDetails { get; set; }

		[MetadataExclude, LazyLoad]
		public virtual byte[] ResultContent { get; set; }

		[JsonProperty]
		public virtual string ResultName { get; set; }

		[NotLonger(128), MetadataExclude]
		public virtual string ResultContentType { get; set; }

		[JsonProperty]
		public virtual bool ResultUnread { get; set; }

		#endregion

		[NotMapped]
		public virtual IReportSetting Setting
		{
			get { return ReportSetting; }
		}

		[NotMapped, MetadataExclude]
		public virtual string AuthorLogin { get { return Creator != null ? Creator.Login : null; } }
	}
}