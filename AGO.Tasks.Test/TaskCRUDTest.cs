﻿using System;
using System.Linq;
using AGO.Core;
using AGO.Core.Controllers;
using AGO.Core.Filters;
using AGO.Tasks.Controllers;
using AGO.Tasks.Controllers.DTO;
using AGO.Tasks.Model.Task;
using NUnit.Framework;

namespace AGO.Tasks.Test
{
	/// <summary>
	/// Тесты CRUD реестра задач
	/// </summary>
	public class TaskCRUDTest: AbstractTest
	{
		private TasksController controller;

		public override void FixtureSetUp()
		{
			base.FixtureSetUp();
			controller = IocContainer.GetInstance<TasksController>();
		}

		[Test]
		public void GetTasksReturnAllRecords()
		{
			var t1 = M.Task(1);
			var t2 = M.Task(2);

			var result = controller.GetTasks(
				TestProject,
				Enumerable.Empty<IModelFilterNode>().ToArray(),
				new[] {new SortInfo {Property = "InternalSeqNumber"}},
				0, TaskPredefinedFilter.All).ToArray();

			Assert.AreEqual(2, result.Length);
			Assert.AreEqual(t1.Id, result[0].Id);
			Assert.AreEqual(t2.Id, result[1].Id);
		}

		[Test]
		public void GetTasksCountReturnCount()
		{
			M.Task(1);
			M.Task(2);

			var result = controller.GetTasksCount(
				TestProject,
				Enumerable.Empty<IModelFilterNode>().ToArray(), TaskPredefinedFilter.All);

			Assert.AreEqual(2, result);
		}

		[Test]
		public void LookupTasksWithoutTermReturnAllRecords()
		{
			var t1 = M.Task(1);
			var t2 = M.Task(2);

			var result = controller.LookupTasks(
				TestProject,
				null,
				0).ToArray();

			//assume order by InternalSeqNumber as default sort
			Assert.AreEqual(2, result.Length);
			StringAssert.AreEqualIgnoringCase(t1.Id.ToString(), result[0].Id);
			StringAssert.AreEqualIgnoringCase(t1.SeqNumber, result[0].Text);
			StringAssert.AreEqualIgnoringCase(t2.Id.ToString(), result[1].Id);
			StringAssert.AreEqualIgnoringCase(t2.SeqNumber, result[1].Text);
		}

		[Test]
		public void GetTaskByNumberReturnModel()
		{
			var task = M.Task(1);

			var result = controller.GetTask(TestProject, "t0-1");

			Assert.IsNotNull(result);
			Assert.AreEqual(task.Id, result.Id);
			Assert.AreEqual(task.SeqNumber, result.SeqNumber);
		}

		[Test, ExpectedException(typeof(NoSuchEntityException))]
		public void GetTaskByInvalidNumberThrow()
		{
			controller.GetTask(TestProject, "not existing number");
		}

		[Test, ExpectedException(typeof(NoSuchProjectException))]
		public void GetTaskByInvalidProjectThrow()
		{
			M.Task(1);

			controller.GetTask("not existing project", "t0-1");
		}

		[Test]
		public void GetTaskDetailsByNumberReturnModel()
		{
			var task = M.Task(1, content: "some content");

			var result = controller.GetTaskDetails(TestProject, "t0-1");

			Assert.IsNotNull(result);
			Assert.AreEqual(task.Content, result.Content);
		}

		[Test, ExpectedException(typeof(NoSuchEntityException))]
		public void GetTaskDetailsByInvalidNumberThrow()
		{
			controller.GetTaskDetails(TestProject, "not existing number");
		}

		[Test, ExpectedException(typeof(NoSuchProjectException))]
		public void GetTaskDetailsByInvalidProjectThrow()
		{
			M.Task(1);

			controller.GetTaskDetails("not existing project", "t0-1");
		}

		[Test]
		public void CreateTaskWithoutProjectThrow()
		{
			var model = new CreateTaskDTO();

			Assert.That(controller.CreateTask("not existed project", model).Validation.Success, Is.False);
		}

		[Test]
		public void CreateTaskWithoutTypeReturnError()
		{
			var model = new CreateTaskDTO();

			var vr = controller.CreateTask(TestProject, model).Validation;
			Session.Flush();

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "TaskType").Value.Any());
		}

		[Test]
		public void CreateTaskWithWrongTypeReturnError()
		{
			var model = new CreateTaskDTO { TaskType = Guid.NewGuid(), Executors = new [] { Guid.NewGuid()}};

			var vr = controller.CreateTask(TestProject, model).Validation;
			Session.Flush();

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "TaskType").Value.Any());
		}

		[Test]
		public void CreateTaskWithoutExecutorsReturnError()
		{
			var tt = M.TaskType();
			var model = new CreateTaskDTO {TaskType = tt.Id};

			var vr = controller.CreateTask(TestProject, model).Validation;
			Session.Flush();

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());

			model.Executors = new Guid[0];

			vr = controller.CreateTask(TestProject, model).Validation;
			Session.Flush();

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());
		}

		[Test]
		public void CreateTaskWithWrongExecutorsReturnError()
		{
			var tt = M.TaskType();
			var model = new CreateTaskDTO { TaskType = tt.Id, Executors = new [] { Guid.NewGuid() } };

			var vr = controller.CreateTask(TestProject, model).Validation;

			Assert.IsFalse(vr.Success);
			Assert.IsTrue(vr.FieldErrors.First(e => e.Key == "Executors").Value.Any());
		}

		[Test]
		public void CreateTaskWithValidParamsReturnSuccess()
		{
			var tt = M.TaskType();
			var member = M.ProjectMembers(TestProject).First();

			var model = new CreateTaskDTO
			            	{
			            		TaskType = tt.Id,
			            		Executors = new[] {member.Id},
			            		DueDate = new DateTime(2013, 01, 01),
			            		Content = "test task",
			            		Priority = TaskPriority.Low
			            	};
			var vr = controller.CreateTask(TestProject, model).Validation;
			Session.Flush();

			Assert.IsTrue(vr.Success);
			var task = Session.QueryOver<TaskModel>().Where(m => m.ProjectCode == TestProject).Take(1).SingleOrDefault();
			Assert.IsNotNull(task);
			M.Track(task);
			Assert.AreEqual(TestProject, task.ProjectCode);
			Assert.AreEqual("t0-1", task.SeqNumber);
			Assert.AreEqual(1, task.InternalSeqNumber);
			Assert.AreEqual(tt.Id, task.TaskType.Id);
			Assert.IsTrue(task.Executors.Any(e => e.Executor.Id == member.Id));
			Assert.AreEqual(new DateTime(2013, 01, 01), task.DueDate);
			Assert.AreEqual("test task", task.Content);
			Assert.AreEqual(TaskPriority.Low, task.Priority);
		}

		[Test]
		public void DeleteTaskReturnSuccess()
		{
			var t = M.Task(1);

			var res = controller.DeleteTask(TestProject, t.Id);
			Session.Flush();

			Assert.IsTrue(res);
			t = Session.Get<TaskModel>(t.Id);
			Assert.IsNull(t);
		}

		[Test]
		public void DeleteSeveralTaskReturnSuccess()
		{
			var t1 = M.Task(1);
			var t2 = M.Task(2);
			var t3 = M.Task(3);

			var res = controller.DeleteTasks(TestProject, new [] { t1.Id, t3.Id});
			Session.Flush();

			Assert.IsTrue(res);
			t1 = Session.Get<TaskModel>(t1.Id);
			t2 = Session.Get<TaskModel>(t2.Id);
			t3 = Session.Get<TaskModel>(t3.Id);
			Assert.IsNull(t1);
			Assert.IsNotNull(t2);
			Assert.IsNull(t3);
		}

		//TODO Test editing
		[Test]
		public void UpdateWithInvalidProjectReturnError()
		{
			var t = M.Task(1);

			var inf = new PropChangeDTO(t.Id, t.ModelVersion, "Content", "bla bla");
			Assert.That(controller.UpdateTask("not existing proj", inf).Validation.Success, Is.False);
		}

		[Test]
		public void UpdateWithInvalidIdReturnError()
		{
			var t = M.Task(1);

			var inf = new PropChangeDTO(Guid.NewGuid(), t.ModelVersion, "Content", "bla bla");
			var ur = controller.UpdateTask(TestProject, inf);
			Session.Flush();

			Assert.IsFalse(ur.Validation.Success);
			Assert.IsTrue(ur.Validation.Errors.Any());
		}

		[Test]
		public void UpdateContentWithInvalidTypeReturnError()
		{
			var t = M.Task(1);

			var inf = new PropChangeDTO(t.Id, t.ModelVersion, "Content", new {a = 1});
			var ur = controller.UpdateTask(TestProject, inf);
			Session.Flush();

			Assert.IsFalse(ur.Validation.Success);
			Assert.IsTrue(ur.Validation.FieldErrors.First(e => e.Key == "Content").Value.Any());
		}

		[Test]
		public void UpdateContentReturnSuccess()
		{
			var t = M.Task(1);

			var inf = new PropChangeDTO(t.Id, t.ModelVersion, "Content", "some test string");
			var ur = controller.UpdateTask(TestProject, inf);
			Session.Flush();

			Assert.IsTrue(ur.Validation.Success);
			t = Session.Get<TaskModel>(t.Id);
			Assert.AreEqual("some test string", t.Content);
		}
	}
}