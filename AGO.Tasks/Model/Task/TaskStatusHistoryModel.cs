﻿using System;
using System.ComponentModel;
using AGO.Core.Attributes.Constraints;
using AGO.Core.Attributes.Mapping;
using AGO.Core.Attributes.Model;
using AGO.Core.Model.Security;
using Newtonsoft.Json;

namespace AGO.Tasks.Model.Task
{
    /// <summary>
    /// Запись в истории изменения статусов задачи
    /// </summary>
	[Table(SchemaName = "Tasks")]
    public class TaskStatusHistoryModel: SecureModel<Guid>
    {
        #region Persistent

        /// <summary>
        /// Дата изменения статуса на заданный
        /// </summary>
        [DisplayName("Дата начала"), JsonProperty/*, InRange(new DateTime(2000, 01, 01), new DateTime(2200, 01, 01))*/]
        public virtual DateTime Start { get; set; }

        /// <summary>
        /// Дата изменения статуса на следующий, фиксирует период нахождения
        /// задачи в заданном статусе. Может быть null, если задача все еще находится
        /// в заданном статусе
        /// </summary>
        [DisplayName("Дата окончания"), JsonProperty, NotNull]
        public virtual DateTime? Finish { get; set; }

        /// <summary>
        /// Задачи, изменение статуса которой регистрируется
        /// </summary>
        [DisplayName("Задача"), JsonProperty, NotNull]
        public virtual TaskModel Task { get; set; }
        [ReadOnlyProperty, MetadataExclude]
        public virtual Guid? TaskId { get; set; }

        /// <summary>
        /// Установленный задаче статус
        /// </summary>
        [DisplayName("Статус"), JsonProperty, NotNull]
        public virtual TaskStatus Status { get; set; }

        #endregion
    }
}