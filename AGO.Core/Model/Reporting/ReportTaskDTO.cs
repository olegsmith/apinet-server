﻿using System;
using AGO.Core.Localization;
using AGO.Reporting.Common.Model;

namespace AGO.Core.Model.Reporting
{
	public class ReportTaskDTO
	{
		public static ReportTaskDTO FromTask(ReportTaskModel m, ILocalizationService ls, string project = null, bool hideErrorDetails = false)
		{
			if (m == null)
				throw new ArgumentNullException("m");

			return new ReportTaskDTO
			{
				Id = m.Id,
				Project = project ?? m.ProjectCode,
				ProjectCode = m.ProjectCode,
				Name = m.Name,
				State = m.State.ToString(),
				StateName = ls.MessageForType(typeof(ReportTaskState), m.State) ?? m.State.ToString(),
				Author = m.Creator != null ? m.Creator.FullName : null,
				CreationTime = m.CreationTime,
				StartedAt = m.StartedAt,
				CompletedAt = m.CompletedAt,
				DataGenerationProgress = m.DataGenerationProgress,
				ReportGenerationProgress = m.ReportGenerationProgress,
				ErrorMsg = m.ErrorMsg,
				ErrorDetails = !hideErrorDetails ? m.ErrorDetails : null,
				ResultUnread = m.ResultUnread
			};
		}

		public Guid Id { get; set; }

		public string Project { get; set; }

		public string ProjectCode { get; set; }

		public string Name { get; set; }

		public string State { get; set; }

		public string StateName { get; set; }

		public string Author { get; set; }

		public DateTime? CreationTime { get; set; }

		public DateTime? StartedAt { get; set; }

		public DateTime? CompletedAt { get; set; }

		public byte DataGenerationProgress { get; set; }

		public byte ReportGenerationProgress { get; set; }

		public string ErrorMsg { get; set; }

		public string ErrorDetails { get; set; }

		public bool ResultUnread { get; set; }
	}
}
