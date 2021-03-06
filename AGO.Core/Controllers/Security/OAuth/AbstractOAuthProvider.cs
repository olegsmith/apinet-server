﻿using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using AGO.Core.DataAccess;
using AGO.Core.Model.Configuration;
using AGO.Core.Model.Security;
using NHibernate;

namespace AGO.Core.Controllers.Security.OAuth
{
	public abstract class AbstractOAuthProvider: AbstractService, IOAuthProvider
	{
		private readonly ISessionProviderRegistry rsp;
		protected string RedirectUrl;
		private const string RedirectUrlConfigKey = "RedirectUrl";

		protected AbstractOAuthProvider(ISessionProviderRegistry providerRegistry)
		{
			if (providerRegistry == null)
				throw new ArgumentNullException("providerRegistry");

			rsp = providerRegistry;
		}

		protected void DoInSession(Action<ISession> action)
		{
			using (var s = rsp.GetMainDbProvider().SessionFactory.OpenSession())
			{
				action(s);
				s.Flush();
				s.Close();
			}
		}

		protected virtual UserModel FindUserById(string userId)
		{
			if (userId.IsNullOrWhiteSpace())
				throw new ArgumentNullException("userId");

			UserModel u = null;
			DoInSession(s =>
			{
				u = s.QueryOver<UserModel>().Where(m => m.OAuthProvider == Type && m.OAuthUserId == userId).SingleOrDefault();
			});

			return u;
		}

		protected UserModel RegisterUser(string userId, string fname, string lname)
		{
			UserModel user = null;
			DoInSession(s =>
			{
				user = new UserModel
				{
					CreationTime = DateTime.UtcNow,
					Active = true,
					FirstName = fname,
					LastName = lname,
					SystemRole = SystemRole.Member,
					OAuthProvider = Type,
					OAuthUserId = userId
				};
				s.Save(user);
				//every new user take one ticket for creating one project
				var ticket = new ProjectTicketModel { User = user, CreationTime = DateTime.UtcNow };
				s.Save(ticket);
			});
			return user;
		}

		protected virtual void UpdateAvatar(UserModel user, string url)
		{
			DoInSession(s =>
			{
				user.AvatarUrl = url;
				s.Update(user);
			});
		}

		protected override string DoGetConfigProperty(string key)
		{
			if (RedirectUrlConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				return RedirectUrl;
			}
			return base.DoGetConfigProperty(key);
		}

		protected override void DoSetConfigProperty(string key, string value)
		{
			if (RedirectUrlConfigKey.Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				RedirectUrl = value;
			}
			else
				base.DoSetConfigProperty(key, value);
		}

		public virtual OAuthDataModel CreateData()
		{
			return new OAuthDataModel();
		}

		public abstract OAuthProvider Type { get; }
		public abstract Task<string> PrepareForLogin(OAuthDataModel data);
		public abstract Task<UserModel> QueryUserId(OAuthDataModel data, NameValueCollection parameters);
		public abstract bool IsCancel(NameValueCollection parameters);
	}
}
