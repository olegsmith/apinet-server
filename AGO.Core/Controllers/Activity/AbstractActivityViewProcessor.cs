﻿using System;
using System.Globalization;
using System.Linq;
using AGO.Core.Localization;
using AGO.Core.Model.Activity;

namespace AGO.Core.Controllers.Activity
{
	public abstract class AbstractActivityViewProcessor<TModel> : IActivityViewProcessor
		where TModel : ActivityRecordModel
	{
		#region Properties, fields, constructors

		protected readonly ILocalizationService LocalizationService;

		protected AbstractActivityViewProcessor(ILocalizationService localizationService)
		{
			if (localizationService == null)
				throw new ArgumentNullException("localizationService");
			LocalizationService = localizationService;
		}

		#endregion

		#region Interfaces implementation

		public void Process(ActivityView view, ActivityRecordModel model)
		{
			if (view == null || !(model is TModel))
				return;
			if (DoProcess(view, (TModel) model))
				view.ApplicableProcessors.Add(this);
		}

		public void PostProcess(ActivityView view)
		{
			if (view == null || !view.ApplicableProcessors.Contains(this))
				return;
			DoPostProcess(view);
		}

		public void ProcessItem(ActivityItemView view, ActivityRecordModel model)
		{
			if (view == null || !(model is TModel))
				return;
			if (DoProcessItem(view, (TModel) model))
				view.ApplicableProcessors.Add(this);
		}

		public void PostProcessItem(ActivityItemView view)
		{
			if (view == null || !view.ApplicableProcessors.Contains(this))
				return;
			DoPostProcessItem(view);
		}

		#endregion

		#region Template methods

		protected virtual bool DoProcess(ActivityView view, TModel model)
		{
			view.ActivityTime = (model.CreationTime ?? DateTime.Now).ToLocalTime().ToString("O");
			view.ActivityItem = model.ItemName;

			return true;
		}

		protected virtual bool DoProcessItem(ActivityItemView view, TModel model)
		{
			var groupedView = view as GroupedActivityItemView;
			if (groupedView != null)
			{
				groupedView.ChangeCount++;
				groupedView.Users.Add(model.Creator.ToStringSafe()); 
				return true;
			}

			view.ActivityTime = (model.CreationTime ?? DateTime.Now).ToLocalTime().ToString("O");
			view.User = model.Creator.ToStringSafe();

			return true;
		}

		protected virtual void DoPostProcess(ActivityView view)
		{
		}

		protected virtual void DoPostProcessItem(ActivityItemView view)
		{
			LocalizeUser(view);
			LocalizeAction(view);
		}

		#endregion
		
		#region Helper methods

		protected virtual void LocalizeAction(ActivityItemView view)
		{
			if (view.Action.IsNullOrWhiteSpace() || !(view is GroupedActivityItemView))
				return;

			view.Action = LocalizationService.MessageForType(typeof(IActivityViewProcessor), 
				"CommitedChanges", CultureInfo.CurrentUICulture, view.Action);
		}
		
		protected virtual void LocalizeActivityItem<TType>(ActivityView view)
		{
			if (view.ActivityItem.IsNullOrWhiteSpace())
				return;

			view.ActivityItem = LocalizationService.MessageForType(typeof(TType), "ActivityItem",
				CultureInfo.CurrentUICulture, view.ActivityItem);
		}
		
		protected virtual void LocalizeUser(ActivityItemView view)
		{
			var groupedView = view as GroupedActivityItemView;
			if (groupedView == null)
			{
				if (!view.User.IsNullOrWhiteSpace())
						view.User = LocalizationService.MessageForType(typeof(IActivityViewProcessor), "User",
					CultureInfo.CurrentUICulture, view.User);
				return;
			}				

			groupedView.User = groupedView.Users.Count > 1 
				? LocalizationService.MessageForType(typeof(IActivityViewProcessor), "Users", CultureInfo.CurrentUICulture,
					groupedView.Users.Aggregate(string.Empty, (current, user) => current.IsNullOrWhiteSpace() ? user : current + ", " + user))
				: LocalizationService.MessageForType(typeof(IActivityViewProcessor), "User", CultureInfo.CurrentUICulture, 
					groupedView.Users.FirstOrDefault());
		}

		#endregion
	}
}