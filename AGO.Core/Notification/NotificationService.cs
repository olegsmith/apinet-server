﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AGO.Core.Model.Reporting;
using BookSleeve;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AGO.Core.Notification
{
	public class NotificationService: AbstractService, INotificationService
	{
		#region Configuration

		private string redisHost = "127.0.0.1";
		private int redisPort = 6379;
		private int maxAttemptCount = 10;
		private TimeSpan retryInterval = TimeSpan.FromSeconds(1);

		private const string HostConfigKey = "redis_host";
		/// <summary>
		/// Имя или ip-адрес хоста с размещенным redis сервером
		/// </summary>
		public string RedisHost
		{
			get { return GetConfigProperty(HostConfigKey); }
			set { SetConfigProperty(HostConfigKey, value); }
		}

		private const string PortConfigKey = "redis_port";
		/// <summary>
		/// Порт, прослушиваемый redis
		/// </summary>
		public int RedisPort
		{
			get { return GetConfigProperty(PortConfigKey).ConvertSafe<int>(); }
			set { SetConfigProperty(PortConfigKey, value.ToString(CultureInfo.InvariantCulture)); }
		}

		private const string MaxAttemptCountConfigKey = "max_attempts";
		/// <summary>
		/// Максимальное кол-во попыток повторной отправки данных в случае ошибки
		/// </summary>
		public int MaxAttemtpCount 
		{ 
			get { return GetConfigProperty(MaxAttemptCountConfigKey).ConvertSafe<int>(); } 
			set { SetConfigProperty(MaxAttemptCountConfigKey, value.ToString(CultureInfo.InvariantCulture)); } 
		}

		private const string RetryIntervalConfigKey = "retry_interval";
		public TimeSpan RetryInterval 
		{ 
			get { return TimeSpan.FromMilliseconds(GetConfigProperty(RetryIntervalConfigKey).ConvertSafe<int>()); }
			set { SetConfigProperty(RetryIntervalConfigKey, value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)); }
		}

		protected override string DoGetConfigProperty(string key)
		{
			if (HostConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
				return redisHost;
			if (PortConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
				return redisPort.ToString(CultureInfo.InvariantCulture);
			if (MaxAttemptCountConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
				return maxAttemptCount.ToString(CultureInfo.InvariantCulture);
			if (RetryIntervalConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
				return retryInterval.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
			
			return base.DoGetConfigProperty(key);
		}

		protected override void DoSetConfigProperty(string key, string value)
		{
			if (HostConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				redisHost = value;
			}
			else if (PortConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				redisPort = value.ConvertSafe<int>();
			}
			else if (MaxAttemptCountConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase)) 
			{
				maxAttemptCount = value.ConvertSafe<int>();
			}
			else if (RetryIntervalConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase)) 
			{
				retryInterval = TimeSpan.FromMilliseconds(value.ConvertSafe<int>());
			}
			else
				base.DoSetConfigProperty(key, value);
		}

		#endregion

		#region Connection management

		protected override void DoInitialize()
		{
			base.DoInitialize();

			Interlocked.Exchange(ref state, State.Connecting);
// ReSharper disable CSharpWarnings::CS4014
			//don't wait for connect - there will be infinity loop if redis not started
			Start();
// ReSharper restore CSharpWarnings::CS4014
		}

		private static class State
		{
			public const int Disconnected = 0;
			public const int Connecting = 1;
			public const int Connected = 2;
			public const int Closed = 3;
		}

		private const string EVENT_REPORT_RUN = "reports_run";
		private const string EVENT_REPORT_CANCEL = "reports_cancel";
		private const string EVENT_REPORT_CHANGED = "reports_changed";
		private const string EVENT_WORK_QUEUE_CHANGED = "workqueue_changed";
		private RedisConnection pubConnection;
		private RedisSubscriberConnection subConnection;
		private readonly List<Action<Guid>> runReportSubscribers = new List<Action<Guid>>();
		private readonly List<Action<Guid>> cancelReportSubscribers = new List<Action<Guid>>();
		private readonly List<Action<string, string, object>> reportChandedSubscribers = new List<Action<string, string, object>>();
		private int state = State.Disconnected;

		private void LogInfo(string message)
		{
			Log.InfoFormat("{0}\tThread {1}\tState {2}: {3}", DateTime.Now.ToString("hh.mm.ss.ffff"), Thread.CurrentThread.ManagedThreadId, state, message);
		}

		private void LogDebug(string message)
		{
			Log.DebugFormat("{0}\tThread {1}\tState {2}: {3}", DateTime.Now.ToString("hh.mm.ss.ffff"), Thread.CurrentThread.ManagedThreadId, state, message);
		}

		public async Task<object> Start()
		{
			//LogDebug("Enter start");

			do
			{
				Task connectTask = ConnectToRedis();
				try
				{
					connectTask.Wait(TimeSpan.FromSeconds(10));
					//LogDebug("Connect task successfull");
					Interlocked.Exchange(ref state, State.Connected);
					LogInfo("Started");
				}
				catch (AggregateException ex)
				{
					ex.Handle(e => {
						Log.InfoFormat("Notification service: error on connecting.", e);
						return true;
					});

					//LogDebug("Connect task faulted");
					if (state == State.Closed)
					{
						return null;
					}
					LogDebug("Try connect once more");
				}
				await Task.Delay(RetryInterval);
			} while (state != State.Connected && state != State.Closed);
			return null;
		}

		private async Task ConnectToRedis()
		{
			if (pubConnection != null)
			{
				pubConnection.Closed -= Reconnect;
				pubConnection.Error -= Reconnect;
				pubConnection.Dispose();
				pubConnection = null;
			}

			var connection = new RedisConnection(redisHost, redisPort);
			//LogDebug("Opening connection");
			await connection.Open();
			//LogDebug("Connection open, next subscribe");
			var channel = connection.GetOpenSubscriberChannel();
			channel.CompletionMode = ResultCompletionMode.PreserveOrder;
			await Task.WhenAll(
				channel.Subscribe(EVENT_REPORT_RUN, (type, msg) => OnReportActionEvent(msg, runReportSubscribers)),
				channel.Subscribe(EVENT_REPORT_CANCEL, (type, msg) => OnReportActionEvent(msg, cancelReportSubscribers)),
				channel.Subscribe(EVENT_REPORT_CHANGED, (type, msg) => OnReportChangedEvent(msg)));
			//LogDebug("Subscribed successfully");
			subConnection = channel;
			pubConnection = connection;
			connection.Closed += Reconnect;
			connection.Error += Reconnect;
			//LogDebug("Redis connection ready to work");
		}

		private void Reconnect(object sender, EventArgs e)
		{
			//LogDebug("Try set disconnected on reconnecting");

			if (State.Connected != Interlocked.CompareExchange(ref state, State.Disconnected, State.Connected))
			{
				//LogDebug("Loose disconnect set, another thread first set state");
				return;
			}

			//LogDebug("Try set connecting on reconnecting");

			if (State.Disconnected == Interlocked.CompareExchange(ref state, State.Connecting, State.Disconnected))
			{
				LogInfo("Reconnecting: clean old connection");

				if (subConnection != null)
				{
					subConnection.Dispose();
					subConnection = null;
				}
				if (pubConnection != null)
				{
					pubConnection.Error -= Reconnect;
					pubConnection.Closed -= Reconnect;
					pubConnection.Dispose();
					pubConnection = null;
				}

				Start().Wait();
			}
		}

		private static void OnReportActionEvent(byte[] msg, IEnumerable<Action<Guid>> subscribers)
		{
			var taskId = new Guid(msg);
			foreach (var subscriber in subscribers)
			{
				subscriber(taskId);
			}
		}

		private void OnReportChangedEvent(byte[] msg)
		{
			var jobj = JObject.Parse(Encoding.UTF8.GetString(msg));
			var type = jobj.TokenValue("type");
			var login = jobj.TokenValue("login");
			var dto = jobj.Property("report").Value.ToObject<ReportTaskDTO>();
			foreach (var subscriber in reportChandedSubscribers)
			{
				subscriber(type, login, dto);
			}
		}

		public async Task<T> DoWithRedis<T>(Func<T> action)
		{
			int attempt = MaxAttemtpCount;
			do
			{
				try
				{
					if (pubConnection != null)
						return action();
				}
				catch (Exception ex)
				{
					if (ex is RedisException || 
						ex is TimeoutException || 
						ex is IOException || 
						ex is InvalidOperationException || 
						ex is NullReferenceException) //last is are result of pubConnection=null in reconnect process, that may occurs in the middle of action() execution
					{
						LogInfo("Error when send to redis, attemts " + attempt + ". Error: " + ex);
						if (attempt <= 0)
							throw;
						if (state == State.Connected)
							Reconnect(this, EventArgs.Empty);
					}
					else
					{
						throw;
					}
				}
				attempt--;
				await Task.Delay(RetryInterval);
			} while (attempt >= 0);

			//We can't go to this line
			throw new Exception("Max retry attempt exceed (Can't send data to redis, but we must not to go here, bug in logic)");
		}

		public void Stop()
		{
			Interlocked.Exchange(ref state, State.Closed);

			if (subConnection != null)
			{
				subConnection.Close(true);
				subConnection = null;
			}

			if (pubConnection != null)
			{
				pubConnection.Error -= Reconnect;
				pubConnection.Closed -= Reconnect;

				pubConnection.Close(true);
				pubConnection.Dispose();
				pubConnection = null;
			}
		}

		#endregion

		#region Service implementation

		public Task EmitRunReport(Guid reportId)
		{
			return DoWithRedis<object>(() =>
			{
				var emitTask = pubConnection.Publish(EVENT_REPORT_RUN, reportId.ToByteArray());
				pubConnection.Wait(emitTask);
				return null;
			});
		}

		public Task EmitCancelReport(Guid reportId)
		{
			return DoWithRedis<object>(() =>
			{
				var emitTask = pubConnection.Publish(EVENT_REPORT_CANCEL, reportId.ToByteArray());
				pubConnection.Wait(emitTask);
				return null;
			});
		}

		public Task EmitReportChanged(string type, string login, object dto)
		{
			return DoWithRedis<object>(() =>
			{
				var msg = new { type, login, report = dto };
				var msgjson = JsonConvert.SerializeObject(msg);
				var emitTask = pubConnection.Publish(EVENT_REPORT_CHANGED, msgjson);
				pubConnection.Wait(emitTask);
				return null;
			});
		}

		public void SubscribeToRunReport(Action<Guid> subscriber)
		{
			if (subscriber == null) return;

			runReportSubscribers.Add(subscriber);
		}

		public void SubscribeToCancelReport(Action<Guid> subscriber)
		{
			if (subscriber == null) return;

			cancelReportSubscribers.Add(subscriber);
		}

		public void SubscribeToReportChanged(Action<string, string, object> subscriber)
		{
			if (subscriber == null) return;

			reportChandedSubscribers.Add(subscriber);
		}

		public Task EmitWorkQueueChanged(string login, object dto)
		{
			return DoWithRedis<object>(() =>
			{
				var msg = new { login, data = dto };
				var msgjson = JsonConvert.SerializeObject(msg);
				var emitTask = pubConnection.Publish(EVENT_WORK_QUEUE_CHANGED, msgjson);
				pubConnection.Wait(emitTask);
				return null;
			});
		}

		#endregion

		public void Dispose()
		{
			Stop();
		}
	}
}
