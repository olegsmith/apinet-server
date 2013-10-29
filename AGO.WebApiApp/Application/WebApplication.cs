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
using AGO.Core.Modules;
using AGO.WebApiApp.App_Start;
using AGO.WebApiApp.Controllers;
using AGO.WebApiApp.Execution;
using SimpleInjector.Integration.Web.Mvc;
using WebActivator;

[assembly: PostApplicationStartMethod(typeof(Initializer), "Initialize")]

namespace AGO.WebApiApp.Application
{
	public enum DevMode
	{
		Dev,
		Prod
	}

	public class WebApplication : AbstractControllersApplication, IWebApplication
	{
		#region Properties, fields, constructors

		public static DevMode DevMode { get; private set; }

		public static bool DisableCaching { get; private set; }

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

			IocContainer.RegisterSingle<IStateStorage, WebSessionStateStorage>();
		}

		protected virtual void DoRegisterWebServices()
		{
			DevMode = KeyValueProvider.Value("DevMode").ParseEnumSafe(DevMode.Dev);
			DisableCaching = KeyValueProvider.Value("DisableCaching").ConvertSafe<bool>();

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

			RegisterDefaultRoute(RouteTable.Routes);
		}

		protected void RegisterCoreRoutes(RouteCollection routes)
		{
			routes.RouteExistingFiles = true;

			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			routes.MapRoute(
				"Home",
				"",
				new { controller = "Home", action = "Index" });

			routes.MapRoute(
				"ProjectPage",
				"projects/{project}",
				new { controller = "Home", action = "Project" });

			routes.MapRoute(
				"AllModelsMetadata",
				"metadata/AllModelsMetadata",
				new { controller = "Metadata", action = "AllModelsMetadata" });
		}

		protected void RegisterDefaultRoute(RouteCollection routes)
		{
			RouteTable.Routes.MapRoute(
				"ng-app",
				"{*path}",
				new { controller = "StaticFiles", action = "StaticFile" });
		}

		#endregion
	}

	public static class Initializer
	{
		public static void Initialize()
		{
			new WebApplication { WebEnabled = true }.Initialize();
		}
	}
}