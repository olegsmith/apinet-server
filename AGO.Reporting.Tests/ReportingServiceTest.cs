﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using AGO.Core;
using AGO.Core.Config;
using AGO.Core.Filters;
using AGO.Core.Model.Dictionary.Projects;
using AGO.Core.Model.Security;
using AGO.Reporting.Common;
using AGO.Reporting.Common.Model;
using AGO.Reporting.Service;
using Newtonsoft.Json;
using NHibernate.Criterion;
using NUnit.Framework;

namespace AGO.Reporting.Tests
{
	[TestFixture]
	public class ReportingServiceTest: AbstractReportingTest
	{
		[TestFixtureSetUp]
		public new void Init()
		{
			base.Init();
		}

		[TestFixtureTearDown]
		public new void Cleanup()
		{
			base.Cleanup();
		}

		private ReportingService realsvc;
		private IReportingService svc;
		private DictionaryKeyValueProvider provider;
		private IDictionary<string, string> config;
		[SetUp]
		public void SetUp()
		{
			realsvc = new ReportingService();
			config = new Dictionary<string, string>
			         	{
							{"Reporting_ServiceName", "NUnit Fast reports"}, 
							{"Reporting_TemplatesCacheDirectory", "templates_cache"}
						};
			provider = new DictionaryKeyValueProvider(config);
			realsvc.KeyValueProvider = new OverridableAppSettingsKeyValueProvider(provider);
			svc = realsvc;
		}

		[TearDown]
		public new void TearDown()
		{
			svc.Dispose();
			svc = null;
			base.TearDown();
		}

		[Test]
		public void PingAlwaysReturnTrue()
		{
			realsvc.Initialize();

			Assert.IsTrue(svc.Ping());
		}

		[Test]
		public void GenerateCsvReport()
		{
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator, 
				typeof (FakeGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);

			var safeCounter = 0;
			while (safeCounter < 10)
			{
				Thread.Sleep(500);
				safeCounter++;
				Session.Refresh(task);
				if (task.State == ReportTaskState.Completed) break;
			}

			Assert.AreEqual(ReportTaskState.Completed, task.State);
			Assert.IsNotNull(task.StartedAt);
			Assert.IsNotNull(task.CompletedAt);
			Assert.Greater(task.CompletedAt.Value, task.StartedAt.Value);
			StringAssert.AreEqualIgnoringCase("\"1\",\"zxc\"", Encoding.UTF8.GetString(task.Result));
		}

		[Test]
		public void GenerateProjectTagsReport()
		{
			const string search = "en";
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$id$}\",\"{$author$}\",\"{$name$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(ModelDataGenerator).AssemblyQualifiedName,
				typeof(ModelParameters).AssemblyQualifiedName);
			var param = new ModelParameters();
			param.UserId = CurrentUser.Id;
			param.CriteriaJson = FilteringService.GenerateJsonFromFilter(
				FilteringService.Filter<ProjectTagModel>().WhereString(m => m.Name).Like(search, true, true));
			var writer = new StringWriter();
			JsonService.CreateSerializer().Serialize(writer, param);
			var task = M.Task("controlled", "Fast reports", stg.Id, writer.ToString());
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);

			var safeCounter = 0;
			while (safeCounter < 10)
			{
				Thread.Sleep(500);
				safeCounter++;
				Session.Refresh(task);
				if (task.State == ReportTaskState.Completed) break;
			}

			Assert.AreEqual(ReportTaskState.Completed, task.State);
			var tags = Session.QueryOver<ProjectTagModel>().WhereRestrictionOn(m => m.Name).IsLike(search, MatchMode.Anywhere)
				.List<ProjectTagModel>();
			var report = Encoding.UTF8.GetString(task.Result);
			foreach (var tag in tags)
			{
				StringAssert.Contains(tag.Id.ToString(), report);
				StringAssert.Contains(tag.Creator.Name, report);
				StringAssert.Contains(tag.Name, report);
			}
			StringAssert.Contains(CurrentUser.Name, report);
		}

		[Test]
		public void CancelLongReport()
		{
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(FakeCancelableGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);

			var safeCounter = 0;
			while (safeCounter < 10)
			{
				Thread.Sleep(100);
				safeCounter++;
				Session.Refresh(task);
				if (task.State == ReportTaskState.Running) break;
			}

			Thread.Sleep(100);
			svc.CancelReport(task.Id);

			safeCounter = 0;
			while (safeCounter < 10)
			{
				Thread.Sleep(100);
				safeCounter++;
				Session.Refresh(task);
				if (task.State != ReportTaskState.Running) break;
			}

			Assert.AreEqual(ReportTaskState.Canceled, task.State);
		}

		[Test]
		public void TimeoutCancelableLongReport()
		{
			config.Add("Reporting_ConcurrentWorkersTimeout", "2");//timeout 2 sec
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(FakeCancelableGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);
			Thread.Sleep(200);//get time to run

			//wait slightly greater than timeout
			Thread.Sleep(2500);

			Session.Refresh(task);
			Assert.AreEqual(ReportTaskState.Canceled, task.State);
			StringAssert.Contains("timeout", task.ErrorMsg);
		}

		[Test]
		public void TimeoutNonCancelableLongReport()
		{
			config.Add("Reporting_ConcurrentWorkersTimeout", "2");//timeout 2 sec
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(FakeFreezengGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);
			Thread.Sleep(200);//get time to run

			//wait slightly greater than timeout
			Thread.Sleep(2500);

			Session.Refresh(task);
			Assert.AreEqual(ReportTaskState.Canceled, task.State);
			StringAssert.Contains("timeout", task.ErrorMsg);
		}

		[Test]
		public void AbortLongReport()
		{
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(FakeFreezengGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);

			var safeCounter = 0;
			while (safeCounter < 10)
			{
				Thread.Sleep(100);
				safeCounter++;
				Session.Refresh(task);
				if (task.State == ReportTaskState.Running) break;
			}

			Thread.Sleep(100);
			svc.CancelReport(task.Id);

			//Must be aborted in reasonable time
			Thread.Sleep(2000);
			Session.Refresh(task);

			Assert.AreEqual(ReportTaskState.Canceled, task.State);
		}

		[Test]
		public void TrackReportProgress()
		{
			config.Add("Reporting_TrackProgressInterval", "50");//each 50ms track state
			realsvc.Initialize();

			var tpl = M.Template("fake", Encoding.UTF8.GetBytes("<range_data>\"{$num$}\",\"{$txt$}\"</range_data>"));
			var stg = M.Setting("fake", tpl.Id, GeneratorType.CvsGenerator,
				typeof(FakeTenIterationGenerator).AssemblyQualifiedName,
				typeof(FakeParameters).AssemblyQualifiedName);
			var task = M.Task("controlled", "Fast reports", stg.Id, "{a:1, b:'zxc'}");
			_SessionProvider.FlushCurrentSession();

			svc.RunReport(task.Id);
			Thread.Sleep(50);//time to run

			var safeCounter = 0;
			Session.Refresh(task);
			while (safeCounter < 20 && task.State == ReportTaskState.NotStarted)
			{
				Thread.Sleep(50);
				Session.Refresh(task);
			}

			safeCounter = 0;
			var prev = -1;
			Session.Refresh(task);
			while (safeCounter < 200 && task.State == ReportTaskState.Running && task.DataGenerationProgress < 100)
			{
				Session.Refresh(task);
				Assert.IsTrue(task.DataGenerationProgress >= prev);
				prev = task.DataGenerationProgress;
				Thread.Sleep(100);
				safeCounter++;
			}
			Session.Refresh(task);
			Assert.AreEqual(100, task.DataGenerationProgress);
		}
	}

	public class FakeParameters
	{
		[JsonProperty("a")]
		public int A { get; set; }

		[JsonProperty("b")]
		public string B { get; set; }
	}

	public class FakeFreezengGenerator: BaseReportDataGenerator
	{
		protected override void FillReportData(object parameters)
		{
			Thread.Sleep(1000 * 60);//wait one minute
		}
	}

	public class FakeCancelableGenerator: BaseReportDataGenerator
	{
		protected override void FillReportData(object parameters)
		{
			InitTicker(120);
			for (int i = 0; i < 120; i++)
			{
				Thread.Sleep(1000);
				Ticker.AddTick(); //cancelation will be processed there
			}
		}
	}

	public class FakeTenIterationGenerator: BaseReportDataGenerator
	{
		protected override void FillReportData(object parameters)
		{
			InitTicker(10);
			var range = MakeRange("data", string.Empty);
			for (int i = 0; i < 10; i++)
			{
				var item = MakeItem(range);
				MakeValue(item, "num", i.ToString(CultureInfo.InvariantCulture));
				MakeValue(item, "txt", i.ToString(CultureInfo.InvariantCulture));
				Ticker.AddTick();
				Thread.Sleep(100);
			}
		}
	}

	public class FakeGenerator: BaseReportDataGenerator
	{
		protected override void FillReportData(object parameters)
		{
			var p = (FakeParameters)parameters;
			var range = MakeRange("data", string.Empty);
			var item = MakeItem(range);
			MakeValue(item, "num", p.A.ToString(CultureInfo.CurrentUICulture));
			MakeValue(item, "txt", p.B, false);
		}
	}

	public class ModelParameters
	{
		[JsonProperty("user")]
		public Guid UserId { get; set; }

		[JsonProperty("filter")]
		public string CriteriaJson { get; set; }
	}

	public class ModelDataGenerator: BaseReportDataGenerator
	{
		private readonly ISessionProvider sp;
		private readonly IFilteringService fs;

		public ModelDataGenerator(ISessionProvider provider, IFilteringService service)
		{
			sp = provider;
			fs = service;
		}

		protected override void FillReportData(object parameters)
		{
			var param = parameters as ModelParameters;
			if (param == null) 
				throw new ArgumentException("parameters is not ModelParameters", "parameters");

			var filter = fs.ParseFilterFromJson(param.CriteriaJson, typeof(ProjectTagModel));
			var predicate = fs.CompileFilter(filter, typeof (ProjectTagModel));
			var tags = predicate.GetExecutableCriteria(sp.CurrentSession).List<ProjectTagModel>();
			var executor = sp.CurrentSession.Get<UserModel>(param.UserId);
			
			InitTicker(tags.Count + 1);
			var range = MakeRange("data", string.Empty);
			foreach (var tag in tags)
			{
				var item = MakeItem(range);
				MakeValue(item, "id", tag.Id.ToString());
				MakeValue(item, "author", tag.Creator != null ? tag.Creator.FullName : "<none>");
				MakeValue(item, "name", tag.FullName);
				Ticker.AddTick();
			}
			var userItem = MakeItem(range);
			MakeValue(userItem, "id", string.Empty);
			MakeValue(userItem, "author", "Executor:");
			MakeValue(userItem, "name", executor.FullName);
			Ticker.AddTick();
		}
	}
}