﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AGO.Core.Controllers;
using AGO.Core.Filters;
using AGO.Core.Model.Dictionary.Projects;
using AGO.Core.Model.Security;
using NUnit.Framework;

namespace AGO.Core.Tests.Security
{
	public class DictionaryControllerSecurityTests: AbstractPersistenceTest<ModelHelper>
	{
		private UserModel admin;
		private UserModel member;
		private DictionaryController controller;

		public override void FixtureSetUp()
		{
			base.FixtureSetUp();

			admin = LoginAdmin();
			member = LoginToUser("user1@apinet-test.com");
			controller = IocContainer.GetInstance<DictionaryController>();
		}

		protected override void CreateModelHelpers()
		{
			FM = new ModelHelper(() => MainSession, () => CurrentUser);
			M = new ModelHelper(() => MainSession, () => CurrentUser);
		}

		private dynamic MakeTestTags()
		{
			var adminTag = M.ProjectTag("nunit", admin);
			var memberTag = M.ProjectTag("nunit", member);
			return new {adm = adminTag, mbr = memberTag};
		}

		//admin, member (as system roles)
		//lookup
		[Test]
		public void AdminLookupOwnTags()
		{
			var d = MakeTestTags();
			LoginAdmin();
			var response = controller.LookupProjectTags(0, "nunit");

			Assert.That(response.Count(), Is.EqualTo(1));
			Assert.That(response, Has.Exactly(1).Matches<LookupEntry>(e => e.Id == d.adm.Id.ToString()));
		}

		[Test]
		public void MemberLookupOwnTags()
		{
			var d = MakeTestTags();
			Login(member.Email);
			var response = controller.LookupProjectTags(0, "nunit");

			Assert.That(response.Count(), Is.EqualTo(1));
			Assert.That(response, Has.Exactly(1).Matches<LookupEntry>(e => e.Id == d.mbr.Id.ToString()));
		}
		//get
		[Test]
		public void AdminGetOwnTags()
		{
			var d = MakeTestTags();
			LoginAdmin();
			var termFilter = _FilteringService.Filter<ProjectTagModel>()
				.WhereString(m => m.FullName).Like("nunit", true, true);
			var response = controller.GetProjectTags(0, new[] {termFilter}, Enumerable.Empty<SortInfo>().ToList());

			Assert.That(response.Count(), Is.EqualTo(1));
			Assert.That(response, Has.Exactly(1).Matches<ProjectTagModel>(e => e.Id == d.adm.Id));
		}

		[Test]
		public void MemberGetOwnTags()
		{
			var d = MakeTestTags();
			Login(member.Email);
			var termFilter = _FilteringService.Filter<ProjectTagModel>()
				.WhereString(m => m.FullName).Like("nunit", true, true);
			var response = controller.GetProjectTags(0, new[] { termFilter }, Enumerable.Empty<SortInfo>().ToList());

			Assert.That(response.Count(), Is.EqualTo(1));
			Assert.That(response, Has.Exactly(1).Matches<ProjectTagModel>(e => e.Id == d.mbr.Id));
		}
		//get count
		[Test]
		public void AdminGetOwnTagsCount()
		{
			MakeTestTags();
			LoginAdmin();
			var termFilter = _FilteringService.Filter<ProjectTagModel>()
				.WhereString(m => m.FullName).Like("nunit", true, true);
			var response = controller.GetProjectTagsCount(new[] { termFilter });

			Assert.That(response, Is.EqualTo(1));
		}

		[Test]
		public void MemberGetOwnTagsCount()
		{
			MakeTestTags();
			Login(member.Email);
			var termFilter = _FilteringService.Filter<ProjectTagModel>()
				.WhereString(m => m.FullName).Like("nunit", true, true);
			var response = controller.GetProjectTagsCount(new[] { termFilter });

			Assert.That(response, Is.EqualTo(1));
		}
		//create

		private void DoCreateTagTest(UserModel user)
		{
			Login(user.Email);
			var response = controller.CreateProjectTag(Guid.Empty, "nunit");
			MainSession.Flush();

			Assert.That(response, Is.Not.Null);
			Assert.That(response, Is.TypeOf<ProjectTagModel>());
			var tag = (ProjectTagModel)response;
			M.Track(() => tag);
			Assert.That(tag.FullName, Is.EqualTo("nunit"));
			Assert.That(tag.OwnerId, Is.EqualTo(user.Id));
		}

		[Test]
		public void AdminCanCreateOwnTag()
		{
			DoCreateTagTest(admin);
		}

		[Test]
		public void MemberCanCreateOwnTag()
		{
			DoCreateTagTest(member);
		}

		private void DoCreateSubTagTest(UserModel tagOwner, UserModel current, bool expectSuccess)
		{
			var ptag = M.ProjectTag("nunit parent", tagOwner);
			
			Login(current.Email);
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
			var response = controller.CreateProjectTag(ptag.Id, "nunit child");

			if (!expectSuccess)
			{
				MainSession.Clear(); //rollback

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<ValidationResult>());
				var vr = (ValidationResult) response;
				Assert.That(vr.Errors, Has.Exactly(1).Matches("Not enough permissions for create model"));
			}
			else
			{
				MainSession.Flush();

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<ProjectTagModel>());
				var tag = (ProjectTagModel)response;
				M.Track(() => tag);
				Assert.That(tag.FullName, Is.EqualTo("nunit parent / nunit child"));
				Assert.That(tag.OwnerId, Is.EqualTo(current.Id));
			}
		}

		[Test]
		public void AdminCanCreateChildTag()
		{
			DoCreateSubTagTest(admin, admin, true);
		}

		[Test]
		public void AdminCanNotCreateChildTagForMemberTag()
		{
			DoCreateSubTagTest(member, admin, false);
		}

		[Test]
		public void MemberCanCreateChildTag()
		{
			DoCreateSubTagTest(member, member, true);
		}

		[Test]
		public void MemberCanNotCreateChildTagForAdminTag()
		{
			DoCreateSubTagTest(member, admin, false);
		}
		//update
		private void DoUpdateTagTest(UserModel creator, UserModel updater, bool expectSuccess)
		{
			var tag = M.ProjectTag("nunit", creator);

			Login(updater.Email);
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
			var response = controller.UpdateProjectTag(tag.Id, "new nunit");
			if (!expectSuccess)
			{
				MainSession.Clear();

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<ValidationResult>());
				var vr = (ValidationResult) response;
				Assert.That(vr.Errors, Has.Exactly(1).Matches("Not enough permissions for update model"));
			}
			else
			{
				MainSession.Flush();

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<HashSet<ProjectTagModel>>());
				var updatedTag = ((HashSet<ProjectTagModel>)response).Single();
				Assert.That(updatedTag.FullName, Is.EqualTo("new nunit"));
			}
		}

		[Test]
		public void AdminCanUpdateOwnTag()
		{
			DoUpdateTagTest(admin, admin, true);
		}

		[Test]
		public void MemberCanUpdateOwnTag()
		{
			DoUpdateTagTest(member, member, true);
		}

		[Test]
		public void AdminCanNotUpdateMemberTag()
		{
			DoUpdateTagTest(member, admin, false);
		}

		[Test]
		public void MemberCanNotUpdateAdminTag()
		{
			DoUpdateTagTest(admin, member, false);
		}

		//delete
		private void DoDeleteTagTest(UserModel creator, UserModel current, bool expectSuccess)
		{
			var tag = M.ProjectTag("nunit", creator);

			Login(current.Email);
			Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
			var response = controller.DeleteProjectTag(tag.Id);
			if (!expectSuccess)
			{
				MainSession.Clear();

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<ValidationResult>());
				var vr = (ValidationResult)response;
				Assert.That(vr.Errors, Has.Exactly(1).Matches("Not enough permissions for delete model"));
			}
			else
			{
				MainSession.Flush();

				Assert.That(response, Is.Not.Null);
				Assert.That(response, Is.TypeOf<HashSet<Guid>>());
				var id = ((HashSet<Guid>)response).Single();
				Assert.That(id, Is.EqualTo(tag.Id));
			}
		}

		[Test]
		public void AdminCanDeleteOwnTag()
		{
			DoDeleteTagTest(admin, admin, true);
		}

		[Test]
		public void MemberCanDeleteOwnTag()
		{
			DoDeleteTagTest(member, member, true);
		}

		[Test]
		public void AdminCanNotDeleteMemberTag()
		{
			DoDeleteTagTest(member, admin, false);
		}

		[Test]
		public void MemberCanNotDeleteAdminTag()
		{
			DoDeleteTagTest(admin, member, false);
		}
	}
}