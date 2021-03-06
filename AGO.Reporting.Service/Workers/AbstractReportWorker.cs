﻿using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AGO.Core;
using AGO.Core.DataAccess;
using AGO.Core.Notification;
using AGO.Reporting.Common;
using AGO.Reporting.Common.Model;

namespace AGO.Reporting.Service.Workers
{
	/// <summary>
	/// Базовый класс для worker-ов - генераторов отчетов.
	/// </summary>
	public abstract class AbstractReportWorker
	{
		protected AbstractReportWorker(string project, Guid taskId, SimpleInjector.Container di, TemplateResolver resolver)
		{
			if (project.IsNullOrWhiteSpace())
				throw new ArgumentNullException("project");
			if (taskId == default(Guid))
				throw new ArgumentNullException("taskId");
			if (di == null)
				throw new ArgumentNullException("di");
			if (resolver == null)
				throw new ArgumentNullException("resolver");

			Project = project;
			TaskId = taskId;
			Container = di;
			TemplateResolver = resolver;

			Repository = Container.GetInstance<IReportingRepository>();
			SessionProviderRegistry = Container.GetInstance<ISessionProviderRegistry>();
			Bus = Container.GetInstance<INotificationService>();
		}

		public string Project { get; private set; }

		public Guid TaskId { get; private set; }

		protected SimpleInjector.Container Container { get; private set; }

		protected TemplateResolver TemplateResolver { get; private set; }

		protected IReportingRepository Repository { get; private set; }

		protected ISessionProviderRegistry SessionProviderRegistry { get; private set; }

		protected INotificationService Bus { get; private set; }

		public object Parameters { get; set; }

		public CultureInfo UserCulture { get; set; }

		public bool Finished { get; protected set; }

		public int Timeout { get; set; }

		public int TrackProgressInterval { get; set; }

		public abstract void Prepare(IReportTask task);

		public event EventHandler End;

		private Task<IReportGeneratorResult> task;
		private bool wasTimedOut;
		protected CancellationTokenSource TokenSource;
		private SequentialTimer trackProgressTimer;
		private readonly object tracklc = new object();

		public void Start()
		{
			RegisterStart();
			
			TokenSource = new CancellationTokenSource();
			trackProgressTimer = new SequentialTimer(TrackProgress, TrackProgressInterval);
			task = new Task<IReportGeneratorResult> (IntrenalWrappedStart, TokenSource.Token);
			var whenSuccess = task.ContinueWith(t => RegisterSuccessAndSaveResult(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
			var whenCancel = task.ContinueWith(genTask => RegisterCancel(wasTimedOut), TaskContinuationOptions.OnlyOnCanceled);
			var whenError = task.ContinueWith(t => 
				{
				    var ex = t.Exception != null && 
						t.Exception.InnerExceptions != null &&
				        t.Exception.InnerExceptions.Count > 0
				            ? t.Exception.InnerExceptions[0]
				            : null;
					RegisterError(ex);
				}, TaskContinuationOptions.OnlyOnFaulted);
			
			Task.Factory.ContinueWhenAny(new[] {whenSuccess, whenError, whenCancel}, t => Finish());
			task.Start();
			trackProgressTimer.Run();
		}

		public void Stop()
		{
			if (Finished) return;
			
			//May be stop called early than token will be created in start method
			if (TokenSource != null)
				TokenSource.Cancel();
			else
				Finished = true;
		}

		private void RegisterStart()
		{
			ChangeState(rt =>
			{
				rt.State = ReportTaskState.Running;
				rt.StartedAt = DateTime.Now;
			}, ReportEvents.RUNNED);
		}

		private void RegisterSuccessAndSaveResult(IReportGeneratorResult result)
		{
			StopProgressTracking();
			ChangeState(rt =>
			{
				//Main task in project db
			    rt.State = ReportTaskState.Completed;
			    rt.CompletedAt = DateTime.Now;
			    rt.DataGenerationProgress = 100; //fix generator 
			    rt.ReportGenerationProgress = 100; //ticker errors
			    var buffer = new byte[result.Result.Length];
			    result.Result.Position = 0;
			    result.Result.Read(buffer, 0, buffer.Length);
				rt.ResultContent = buffer;
			    rt.ResultName = result.GetFileName(rt.ResultName);
			    rt.ResultContentType = result.ContentType;

				//Archive record in master db
				var mainDbSession = SessionProviderRegistry.GetMainDbProvider().CurrentSession;
				Repository.ArchiveReport(mainDbSession, rt);
				mainDbSession.Flush();
			}, ReportEvents.COMPLETED);
		}

		private void RegisterError(Exception ex)
		{
			StopProgressTracking();
			ChangeState(rt =>
			{
			    rt.State = ReportTaskState.Error;
			    rt.CompletedAt = DateTime.Now;
			    rt.ErrorMsg = ex != null ? ex.Message : "Unknown error";
			    rt.ErrorDetails = ex != null ? ex.ToString() : string.Empty;
			}, ReportEvents.ERROR);
		}

		private void RegisterCancel(bool abortedByTimeout)
		{
			StopProgressTracking();
			//if not timeouted - reportingcontroller write about cancel himself, we only register interrupt, that we trigger in our code
			if (abortedByTimeout)
			{
				ChangeState(rt =>
				{
					rt.State = ReportTaskState.Canceled;
					rt.CompletedAt = DateTime.Now;
					rt.ErrorMsg = "Report execution was interrupted by timeout";
				}, ReportEvents.ABORTED);
			}
		}

		private void Finish()
		{
			Finished = true;
			if (End != null)
				End(this, EventArgs.Empty);
		}

		/// <summary>
		/// Provide mechanism for soft aborting non-responsible tasks.
		/// Soft means that we register cancelation after timeout and don't wait innerTask, that may be running
		/// some time. This can be quite load for service, but Thread.Abort is more danger approach. We try to use
		/// cooperative multithreading as mush as possible in TPL.
		/// </summary>
		private IReportGeneratorResult IntrenalWrappedStart()
		{
			//Stopped early than really started
			if (Finished)
			{
				TokenSource.Cancel();
				TokenSource.Token.ThrowIfCancellationRequested();
			}

			var innerTask = Task<IReportGeneratorResult>.Factory.StartNew(
				() =>
				{
					var backupCulture = Thread.CurrentThread.CurrentUICulture;
					Thread.CurrentThread.CurrentUICulture = UserCulture;
					try
					{
						return DALHelper.Do(SessionProviderRegistry, Project, (ms, ps) => InternalStart());
					}
					finally
					{
						Thread.CurrentThread.CurrentUICulture = backupCulture;
					}
				}, TokenSource.Token);
			try
			{
				if (innerTask.Wait(Timeout, TokenSource.Token))
				{
					return innerTask.Result;
				}
				//if wait ended by cancelation via token, we go to the catch block and wasTimedOut = false
				//and no additional check for TokenSource.IsCancellationRequested needed.
				wasTimedOut = true;
				if (!TokenSource.IsCancellationRequested)
				{
					TokenSource.Cancel();
					TokenSource.Token.ThrowIfCancellationRequested();
				}
			}
			catch (AggregateException aex)
			{
				if (aex.InnerExceptions[0].GetType() != typeof(TaskCanceledException))
					throw;
			}
			return null;
		}

		protected abstract IReportGeneratorResult InternalStart();

		protected abstract void InternalTrackProgress(IReportTask task);

		private void TrackProgress()
		{
			Func<bool> notStopped = () => !Finished && !TokenSource.IsCancellationRequested;
			Func<IReportTask, bool> notCompleted = rt => rt.DataGenerationProgress < 100 || rt.ReportGenerationProgress < 100;
			ChangeState(rt =>
			{
				InternalTrackProgress(rt);
				if (notStopped() && notCompleted(rt))
				{
					trackProgressTimer.Run();
				}
			}, ReportEvents.PROGRESS);
		}

		private void StopProgressTracking()
		{
			lock (tracklc)
			{
				if (trackProgressTimer == null) return;
				trackProgressTimer.Stop();
				trackProgressTimer = null;
			}
		}

		private readonly object stateChangeSync = new object();
		//т.к. изменение состояния задачи вызывается двумя потоками - основным, делающим работу,
		//и таймером, обновляющим процент выполнения, используем синхронизацию, что бы не получить
		//LockingException на пустом месте
		protected void ChangeState(Action<IReportTask> action, string type)
		{
			lock (stateChangeSync)
			{
				DALHelper.Do(SessionProviderRegistry, Project, (mainSess, projSess)=>
				{
					var backupCulture = Thread.CurrentThread.CurrentUICulture;
					Thread.CurrentThread.CurrentUICulture = UserCulture;
					try
					{
						var reportTask = Repository.GetTask(projSess, TaskId);
						action(reportTask);
						projSess.SaveOrUpdate(reportTask);
						var userId = reportTask.AuthorId.ToString();//cache user id, because after flush user proxy can't be loaded
						projSess.Flush();
						//Not wait for the task complete - no care if some of messages was lost
						Bus.EmitReportChanged(type, userId, Repository.GetTaskAsDTO(mainSess, projSess, reportTask.Id));
					}
					finally
					{
						Thread.CurrentThread.CurrentUICulture = backupCulture;
					}
				});

			}
		}
	}
}