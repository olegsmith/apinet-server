﻿using AGO.Core.Controllers.Activity;
using AGO.Core.Localization;
using AGO.Core.Model.Activity;

namespace AGO.Tasks.Controllers.Activity
{
	public class TaskFilesRelatedActivityViewProcessor : RelatedChangeActivityViewProcessor
	{
		#region Properties, fields, constructors
		public TaskFilesRelatedActivityViewProcessor(ILocalizationService localizationService)
			: base(localizationService)
		{		
		}

		#endregion

		#region Template methods

		protected override bool DoProcess(ActivityView view, RelatedChangeActivityRecordModel model)
		{
			return "TaskFileModel".Equals(model.RelatedItemType) && base.DoProcess(view, model);
		}

		protected override bool DoProcessItem(ActivityItemView view, RelatedChangeActivityRecordModel model)
		{
			return "TaskFileModel".Equals(model.RelatedItemType) && base.DoProcessItem(view, model);
		}

		protected override void DoPostProcess(ActivityView view)
		{
			base.DoPostProcess(view);

			LocalizeActivityItem<TaskFilesRelatedActivityViewProcessor>(view);
		}

		protected override void DoPostProcessItem(ActivityItemView view)
		{
			base.DoPostProcessItem(view);

			LocalizeRelatedActivityItem<TaskFilesRelatedActivityViewProcessor>(view);
		}

		#endregion
	}
}