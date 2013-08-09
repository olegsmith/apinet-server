﻿using System.Web.Mvc;
using System.Web.Routing;

namespace AGO.WebApiApp.App_Start
{
	public class RouteConfig
	{
		public static void RegisterRoutes(RouteCollection routes)
		{
			routes.RouteExistingFiles = true;

			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			if (WebApiApplication.DevMode == DevMode.Dev)
			{
				routes.MapRoute(
					"ng-app",
					"ng-app/{*path}",
					new {controller = "StaticFiles", action = "StaticFile", prefix = "ng-app"});
			}
			else if (WebApiApplication.DevMode == DevMode.Prod)
			{
				routes.MapRoute(
					"ng-app",
					"ng-app/{*path}",
					new { controller = "StaticFiles", action = "StaticFile", prefix = "ng-app-public" });
			}

			routes.MapRoute(
				"Content",
				"Content/{*path}",
				new { controller = "StaticFiles", action = "StaticFile", prefix = "Content" });

			routes.MapRoute(
				"Scripts",
				"Scripts/{*path}",
				new { controller = "StaticFiles", action = "StaticFile", prefix = "Scripts" });

			routes.MapRoute(
				"Images",
				"Images/{*path}",
				new { controller = "StaticFiles", action = "StaticFile", prefix = "Images" });

			routes.MapRoute(
				"Default", 
				"{controller}/{action}/{id}", 
				new { controller = "Home", action = "Index", id = UrlParameter.Optional });
		}
	}
}