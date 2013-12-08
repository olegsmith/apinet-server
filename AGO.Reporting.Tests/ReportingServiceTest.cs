﻿using System.Globalization;
using System.Text;
using System.Threading;
using AGO.Core.Config;
using AGO.Reporting.Common;
using AGO.Reporting.Common.Model;
using AGO.Reporting.Service;
using NUnit.Framework;
using Newtonsoft.Json;

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

		[SetUp]
		public void SetUp()
		{
			realsvc = new ReportingService();
			realsvc.KeyValueProvider = new AppSettingsKeyValueProvider();
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
}