﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Routing;
using AGO.Core;
using AGO.Core.Application;
using AGO.Core.Config;
using AGO.Core.Controllers;
using AGO.Core.Controllers.Security.OAuth;
using AGO.Core.Modules;
using AGO.WebApiApp.Controllers;
using AGO.WebApiApp.Execution;
using SimpleInjector.Integration.Web.Mvc;

namespace AGO.WebApiApp.Application
{
	public class WebApplication : AbstractControllersApplication, IWebApplication
	{
		#region Properties, fields, constructors

		public override IKeyValueProvider KeyValueProvider
		{
			get
			{
				_KeyValueProvider = _KeyValueProvider ?? new AppSettingsKeyValueProvider(
					WebConfigurationManager.OpenWebConfiguration("~/Web.config"));
				return _KeyValueProvider;
			}
			set { base.KeyValueProvider = value; }
		}

		public bool WebEnabled { get; set; }

		#endregion

		#region Interfaces implementation

		public void RegisterJsonEndpoint(IServiceDescriptor descriptor, MethodInfo method)
		{
			if (method == null)
				throw new ArgumentNullException("method");

			var moduleName = !descriptor.Module.Alias.IsNullOrWhiteSpace()
				? descriptor.Module.Alias.TrimSafe() : descriptor.Module.Name.TrimSafe();
			if (moduleName.IsNullOrEmpty())
				throw new Exception(string.Format("Empty module name in \"{0}\"", descriptor.Module.GetType().AssemblyQualifiedName));

			var serviceName = descriptor.Name.TrimSafe();
			if (serviceName.IsNullOrEmpty())
				throw new Exception(string.Format("Empty service name in \"{0}\"", descriptor.GetType().AssemblyQualifiedName));

			var routeName = string.Format("{0}_{1}_{2}",
				descriptor.Module.Name, descriptor.Name, method).Replace('.', '_');
			var routePath = string.Format("api/{0}/{1}/{2}",
				moduleName.FirstCharToLower(),
				serviceName.FirstCharToLower(),
				method.Name.FirstCharToLower());

			RouteTable.Routes.MapRoute(
				routeName,
				routePath,
				new { controller = "Api", action = "Dispatch", serviceType = descriptor.ServiceType, method });
		}

		#endregion

		#region Template methods

		protected override void DoRegisterCoreServices()
		{
			base.DoRegisterCoreServices();

			if (!WebEnabled)
				return;

			DoRegisterWebServices();
		}

		protected override void DoRegisterActionExecution()
		{
			base.DoRegisterActionExecution();

			IocContainer.RegisterSingle<IStateStorage<object>, HttpSessionStateStorage>();
			IocContainer.RegisterSingle<IStateStorage<string>, HttpCookiesStateStorage>();
		}

		protected virtual void DoRegisterWebServices()
		{
			IocContainer.RegisterSingle<IEnvironmentService, HostingEnvironmentService>();
			IocContainer.RegisterInitializer<HostingEnvironmentService>(service =>
				new KeyValueConfigProvider(new RegexKeyValueProvider("^Environment_(.*)", KeyValueProvider)).ApplyTo(service));
		}

		protected override void DoRegisterModules(IList<IModuleDescriptor> moduleDescriptors)
		{
			base.DoRegisterModules(moduleDescriptors);
			if (!WebEnabled)
				return;

			DoRegisterWebModules(moduleDescriptors);
		}

		protected virtual void DoRegisterWebModules(IList<IModuleDescriptor> moduleDescriptors)
		{
			foreach (var moduleDescriptor in moduleDescriptors)
			{
				foreach (var webServiceDescriptor in moduleDescriptor.Services
						.OfType<IWebServiceDescriptor>().OrderBy(s => s.Priority))
					webServiceDescriptor.RegisterWeb(this);
			}
		}

		protected override IEnumerable<Type> AllActionParameterResolvers
		{
			get
			{
				return base.AllActionParameterResolvers.Concat(new[]
				{
					typeof(HttpRuntimeParameterResolver),
					typeof(FormOrQueryParameterResolver),
					typeof(JsonBodyParameterResolver)
				});
			}
		}

		protected override void DoInitializeCoreServices()
		{
			base.DoInitializeCoreServices();
			if (!WebEnabled)
				return;

			DoInitializeWebServices();
		}

		protected virtual void DoInitializeWebServices()
		{
			RegisterCoreRoutes(RouteTable.Routes);

			DependencyResolver.SetResolver(new SimpleInjectorDependencyResolver(IocContainer));
		}

		protected override void DoInitializeModules(IList<IModuleDescriptor> moduleDescriptors)
		{
			base.DoInitializeModules(moduleDescriptors);
			if (!WebEnabled)
				return;

			DoInitializeWebModules(moduleDescriptors);
		}

		protected virtual void DoInitializeWebModules(IList<IModuleDescriptor> moduleDescriptors)
		{
			foreach (var moduleDescriptor in moduleDescriptors)
			{
				foreach (var webServiceDescriptor in moduleDescriptor.Services
						.OfType<IWebServiceDescriptor>().OrderBy(s => s.Priority))
					webServiceDescriptor.InitializeWeb(this);
			}
		}

		protected void RegisterCoreRoutes(RouteCollection routes)
		{
			routes.RouteExistingFiles = false;

			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			RegisterDownloadRoutes(routes);
			RegisterOAuthRoutes(routes);
		}

		protected void RegisterDownloadRoutes(RouteCollection routes)
		{
			routes.MapRoute(Downloader.REPORT_TEMPLATE_TYPE,
							"download/" + Downloader.REPORT_TEMPLATE_TYPE + "/{project}/{id}",
			                new {controller = "Download", action = "DownloadReportTemplate"});
			routes.MapRoute(Downloader.REPORT_TYPE,
							"download/" + Downloader.REPORT_TYPE + "/{project}/{id}",
							new { controller = "Download", action = "DownloadReport" });
			routes.MapRoute(Downloader.FILE_TYPE,
							"download/" + Downloader.FILE_TYPE + "/{project}/{id}",
							new { controller = "Download", action = "DownloadFile" });
		}

		protected void RegisterOAuthRoutes(RouteCollection routes)
		{
			Func<string, OAuthProvider, object> mkdef = (action, providerType) => new {controller = "OAuth", action, providerType};

			routes.MapRoute("BeginFb", "oauth/begin/fb", mkdef("BeginLoginFlow", OAuthProvider.Facebook));
			routes.MapRoute("EndFb", "oauth/fb", mkdef("EndLoginFlow", OAuthProvider.Facebook));
			routes.MapRoute("BeginTwi", "oauth/begin/twi", mkdef("BeginLoginFlow", OAuthProvider.Twitter));
			routes.MapRoute("EndTwi", "oauth/twi", mkdef("EndLoginFlow", OAuthProvider.Twitter));
		}
		
		#endregion
	}
}
