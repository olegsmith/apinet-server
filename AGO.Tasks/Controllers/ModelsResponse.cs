﻿using System.Collections.Generic;

namespace AGO.Tasks.Controllers
{
	/// <summary>
	/// Результат запроса на список моделей
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ModelsResponse<T>
	{
		public int totalRowsCount;

		public IEnumerable<T> rows;
	}
}