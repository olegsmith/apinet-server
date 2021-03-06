﻿using System;
using System.Collections.Generic;
using AGO.Core.Controllers;
using Newtonsoft.Json;

namespace AGO.Tasks.Controllers.DTO
{
	public class BaseTaskDTO : ModelDTO
	{
		public string ProjectCode { get; set; }

		public string SeqNumber { get; set; }

		public string TaskType { get; set; }

		public string Content { get; set; }

		public IEnumerable<LookupEntry> Executors { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public DateTime? DueDate { get; set; }

		public string Status { get; set; }
	}

	public class TaskListItemDTO: BaseTaskDTO
	{
		public IEnumerable<LookupEntry> Tags { get; set; }
	}

	public class TaskListItemDetailsDTO
	{
		public string Priority { get; set; }

		public string Content { get; set; }

		public decimal? EstimatedTime { get; set; }

		public decimal? SpentTime { get; set; }

		public string Note { get; set; }

		public IEnumerable<AgreementView> Agreements { get; set; }

		public IEnumerable<LookupEntry> Files { get; set; }
	}

	public class TaskViewDTO: BaseTaskDTO
	{
		public new LookupEntry TaskType { get; set; }

		public new LookupEntry Status { get; set; }

		public LookupEntry Priority { get; set; }

		public string Note { get; set; }

		public IEnumerable<Agreement> Agreements { get; set; }

		public StatusHistoryDTO StatusHistory { get; set; }

		public IEnumerable<CustomParameterDTO> Parameters { get; set; }

		public string Author { get; set; }

		public DateTime? CreationTime { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public decimal? EstimatedTime { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public decimal? SpentTime { get; set; }

		public IEnumerable<TimelogDTO> Timelog { get; set; }
	}

	public class AgreementView
	{
		public string Agreemer { get; set; }

		public bool Done { get; set; }
	}

	public class Agreement: ModelDTO
	{
		public string Agreemer { get; set; }

		public DateTime? DueDate { get; set; }

		public bool Done { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public DateTime? AgreedAt { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Include)]
		public string Comment;
	}

	public class TimelogDTO: ModelDTO
	{
		public string Member { get; set; }

		public decimal Time { get; set; }

		public string Comment { get; set; }

		public string Editor { get; set; }

		public DateTime? CreationTime { get; set; }
	}
}
