﻿using AGO.Core.Application;

namespace AGO.WebApiApp.Application
{
	public class TestDataApplication : AbstractControllersApplication, ITestDataApplication
	{
		public void CreateDatabase()
		{
			DoCreateDatabase();
		}

		public void PopulateDatabase()
		{
			Initialize();
			DoPopulateDatabase();
		}

		public void CreateAndPopulateDatabase()
		{
			DoCreateDatabase();

			Initialize();
			DoPopulateDatabase();
		}
	}
}
