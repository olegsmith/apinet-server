﻿using System;
using System.Linq;
using AGO.Core.Filters;
using AGO.Home;
using AGO.Home.Model.Projects;
using AGO.Tasks.Controllers;
using AGO.Tasks.Controllers.DTO;
using AGO.Tasks.Model.Task;
using NUnit.Framework;

namespace AGO.Tasks.Test
{
	/// <summary>
	/// Тесты CRUD реестра задач
	/// </summary>
	[TestFixture]
	public class TaskCRUDTest: AbstractTest
	{
		private TasksController controller;

		[TestFixtureSetUp]
		public new void Init()
		{
			base.Init();
			controller = IocContainer.GetInstance<TasksController>();
		}

		[TestFixtureTearDown]
		public new void Cleanup()
		{
			base.Cleanup();
		}

		[TearDown]
		public new void TearDown()
		{
			base.TearDown();
		}


		[Test]
		public void GetTasksReturnAllRecords()
		{
			var t1 = M.Task(1);
			var t2 = M.Task(2);
			_SessionProvider.CloseCurrentSession();

			var result = controller.GetTasks(
				TestProject,
				Enumerable.Empty<IModelFilterNode>().ToArray(),
				new[] {new SortInfo {Property = "InternalSeqNumber"}},
				0, 10).ToArray();

			Assert.AreEqual(2, result.Length);
			Assert.AreEqual(t1.Id, result[0].Id);
			Assert.AreEqual(t2.Id, result[1].Id);
		}

		[Test]
		public void LookupTasksWithoutTermReturnAllRecords()
		{
			var t1 = M.Task(1);
			var t2 = M.Task(2);
			_SessionProvider.CloseCurrentSession();

			var result = controller.LookupTasks(
				TestProject,
				null,
				0, 10).ToArray();

			//assume order by InternalSeqNumber as default sort
			Assert.AreEqual(2, result.Length);
			StringAssert.AreEqualIgnoringCase(t1.Id.ToString(), result[0].Id);
			StringAssert.AreEqualIgnoringCase(t1.SeqNumber, result[0].Text);
			StringAssert.AreEqualIgnoringCase(t2.Id.ToString(), result[1].Id);
			StringAssert.AreEqualIgnoringCase(t2.SeqNumber, result[1].Text);
		}

		[Test, ExpectedException(typeof(NoSuchProjectException))]
		public void CreateTaskWithoutProjectThrow()
		{
			var model = new CreateTaskDTO();

			controller.CreateTask("not existed project", model);
		}

		[Test]
		public void CreateTaskWithoutTypeReturnError()
		{
			var model = new CreateTaskDTO();

			var vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "TaskType").Value.Any());
		}

		[Test]
		public void CreateTaskWithWrongTypeReturnError()
		{
			var model = new CreateTaskDTO { TaskType = Guid.NewGuid(), Executors = new [] { Guid.NewGuid()}};

			var vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "TaskType").Value.Any());
		}

		[Test]
		public void CreateTaskWithoutExecutorsReturnError()
		{
			var tt = M.TaskType();
			_SessionProvider.FlushCurrentSession();
			var model = new CreateTaskDTO {TaskType = tt.Id};

			var vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());

			model.Executors = new Guid[0];

			vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());
		}

		[Test]
		public void CreateTaskWithWrongExecutorsReturnError()
		{
			var tt = M.TaskType();
			_SessionProvider.FlushCurrentSession();
			var model = new CreateTaskDTO { TaskType = tt.Id, Executors = new [] { Guid.NewGuid() } };

			var vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());
		}

		[Test]
		public void CreateTaskWithValidParamsReturnSuccess()
		{
			var tt = M.TaskType();
			var status = M.CustomStatus();
			var project = Session.QueryOver<ProjectModel>().Where(m => m.ProjectCode == TestProject).SingleOrDefault();
			var participant = project.Participants.First();
			_SessionProvider.FlushCurrentSession();

			var model = new CreateTaskDTO
			            	{
			            		TaskType = tt.Id,
			            		Executors = new[] {participant.Id},
			            		DueDate = new DateTime(2013, 01, 01),
			            		Content = "test task",
			            		CustomStatus = status.Id,
			            		Priority = TaskPriority.Low
			            	};
			var vr = controller.CreateTask(TestProject, model);
			_SessionProvider.FlushCurrentSession(!vr.Success);

			Assert.IsTrue(vr.Success);
			var task = Session.QueryOver<TaskModel>().Where(m => m.ProjectCode == TestProject).Take(1).SingleOrDefault();
			Assert.IsNotNull(task);
			Assert.AreEqual(TestProject, task.ProjectCode);
			Assert.AreEqual("t0-1", task.SeqNumber);
			Assert.AreEqual(1, task.InternalSeqNumber);
			Assert.AreEqual(tt.Id, task.TaskType.Id);
			Assert.IsTrue(task.Executors.Any(e => e.Executor.Id == participant.Id));
			Assert.AreEqual(new DateTime(2013, 01, 01).ToUniversalTime(), task.DueDate);
			Assert.AreEqual("test task", task.Content);
			Assert.AreEqual(status.Id, task.CustomStatus.Id);
			Assert.IsTrue(task.CustomStatusHistory.Any(h => h.Task.Id == task.Id && h.Status.Id == status.Id));
			Assert.AreEqual(TaskPriority.Low, task.Priority);
		}
	}
}