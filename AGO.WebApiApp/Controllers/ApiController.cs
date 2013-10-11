﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages.Scope;
using AGO.Core;
using AGO.Core.Attributes.Controllers;
using AGO.Core.Controllers;
using AGO.Core.Execution;
using AGO.Core.Json;
using AGO.Core.Localization;

namespace AGO.WebApiApp.Controllers
{
	public class ApiController : Controller
	{
		public ActionResult Dispatch()
		{
			using (ScopeStorage.CreateTransientScope(new Dictionary<object, object>()))
			{
				try
				{
					var serviceType = RouteData.Values["serviceType"] as Type;
					if (serviceType == null)
						throw new Exception("serviceType is null");

					var method = RouteData.Values["method"] as MethodInfo;
					if (method == null)
						throw new Exception("method is null");

					var service = DependencyResolver.Current.GetService(serviceType);
					var initializable = service as IInitializable;
					if (initializable != null)
						initializable.Initialize();

					var requireAuthorizationAttribute = method.FirstAttribute<RequireAuthorizationAttribute>(false);
					if (requireAuthorizationAttribute != null)
					{
						var authController = DependencyResolver.Current.GetService<AuthController>();
						if (authController == null)
							throw new NotAuthenticatedException();

						if (!authController.IsAuthenticated())
							throw new NotAuthenticatedException();

						if (requireAuthorizationAttribute.RequireAdmin && !authController.IsAdmin())
							throw new AccessForbiddenException();
					}

					var executor = DependencyResolver.Current.GetService<IActionExecutor>();
					var result = executor.Execute(service, method);

					var jsonService = DependencyResolver.Current.GetService<IJsonService>();
					var stringBuilder = new StringBuilder();
					var outputWriter = jsonService.CreateWriter(new StringWriter(stringBuilder), true);
					jsonService.CreateSerializer().Serialize(outputWriter, result);

					return Content(stringBuilder.ToString(), "application/json", Encoding.UTF8);
				}
				catch (Exception e)
				{
					HttpContext.Response.StatusCode = 500;
					if (e is NotAuthenticatedException)
						HttpContext.Response.StatusCode = 401;
					if (e is AccessForbiddenException)
						HttpContext.Response.StatusCode = 403;
					
					var httpException = e as HttpException;
					if (httpException != null)
						HttpContext.Response.StatusCode = httpException.GetHttpCode();

					var message = new StringBuilder();
					var localizationService = DependencyResolver.Current.GetService<ILocalizationService>();

					message.Append(localizationService.MessageForException(e));
					if (message.Length == 0)
						message.Append(localizationService.MessageForException(new ExceptionDetailsHidden()));
					else
					{
						var subMessage = e.InnerException != null
							? localizationService.MessageForException(e.InnerException)
							: null;
						if (!subMessage.IsNullOrEmpty())
							message.Append(string.Format(" ({0})", subMessage.FirstCharToLower()));
					}

					return Json(new {message });
				}
			}
		}
	}
}