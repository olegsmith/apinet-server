﻿using AGO.Core.Application;
using AGO.Core.Controllers.Security;
using AGO.Core.Model.Security;
using NHibernate;
using NUnit.Framework;

namespace AGO.Core.Tests
{
	/// <summary>
	/// Base class for test with persistance mechanics involved
	/// </summary>
	[TestFixture]
	public abstract class AbstractPersistenceTest<TModelHelper>: AbstractControllersApplication
		where TModelHelper: AbstractModelHelper
	{
		protected ISession Session
		{
			get { return _SessionProvider.CurrentSession; }
		}

		/// <summary>
		/// Fixture model helper (cleaned after all tests)
		/// </summary>
		protected TModelHelper FM { get; set; }

		/// <summary>
		/// Test model helper (cleaned after each test)
		/// </summary>
		protected TModelHelper M { get; set; }

		[TestFixtureSetUp]
		public virtual void FixtureSetUp()
		{
			Initialize();
			CreateModelHelpers();
		}

		[TestFixtureTearDown]
		public virtual void FixtureTearDown()
		{
			if (FM != null)
				FM.DropCreated();
		}

		[SetUp]
		public virtual void SetUp()
		{
			LoginAdmin();
		}

		[TearDown]
		public virtual void TearDown()
		{
			if (M != null)
				M.DropCreated();
			Logout();
			_SessionProvider.CloseCurrentSession();
		}

		protected abstract void CreateModelHelpers();

		public UserModel LoginToUser(string login)
		{
			return Session.QueryOver<UserModel>().Where(m => m.Login == login).SingleOrDefault();
		}

		protected UserModel Login(string email)
		{
			Logout();
			var user = LoginToUser(email);
			IocContainer.GetInstance<AuthController>().LoginInternal(user);
			return user;
		}

		protected UserModel LoginAdmin()
		{
			return Login("admin@apinet-test.com");
		}

		protected void Logout()
		{
			IocContainer.GetInstance<AuthController>().Logout();
		}

		protected UserModel CurrentUser
		{
			get { return IocContainer.GetInstance<AuthController>().CurrentUser(); }
		}
	}
}