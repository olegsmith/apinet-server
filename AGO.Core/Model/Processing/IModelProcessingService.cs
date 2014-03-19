﻿using System.Collections.Generic;
using AGO.Core.Model.Projects;
using NHibernate;

namespace AGO.Core.Model.Processing
{
	public interface IModelProcessingService
	{
		void ValidateModelSaving(IIdentifiedModel model, ValidationResult validation, ISession session, object capability = null);

		void ValidateModelDeletion(IIdentifiedModel model, ValidationResult validation, ISession session, object capability = null);

		void RegisterModelValidators(IEnumerable<IModelValidator> validators);

		bool CopyModelProperties(IIdentifiedModel target, IIdentifiedModel source, object capability = null);

		void AfterModelCreated(IIdentifiedModel model, ProjectMemberModel creator = null);

		void AfterModelUpdated(IIdentifiedModel model, IIdentifiedModel original, ProjectMemberModel changer = null);

		void RegisterModelPostProcessors(IEnumerable<IModelPostProcessor> postProcessors);
	}
}