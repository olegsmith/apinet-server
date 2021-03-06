﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AGO.Core.DataAccess;
using FluentNHibernate.Automapping;
using FluentNHibernate.Cfg;
using FluentNHibernate.Conventions;

namespace AGO.Core.AutoMapping
{
	public class AutoMappedSessionFactoryBuilder : SessionFactoryBuilder
	{
		#region Configuration properties, fields and methods

		public string AutoMappingsDumpPath { get; set; }

		public static bool DisableSchemas { get; set; }

		protected readonly ISet<Type> _AutoMappingConventions = new HashSet<Type>();
		public ISet<Type> AutoMappingConventions
		{
			get { return _AutoMappingConventions; }
		}

		protected readonly ISet<Assembly> _AutoMappingAssemblies = new HashSet<Assembly>();
		public ISet<Assembly> AutoMappingAssemblies
		{
			get { return _AutoMappingAssemblies; }
		}

		protected override void DoSetConfigProperty(string key, string value)
		{
			if ("AutoMappingsDumpPath".Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				AutoMappingsDumpPath = value;
				return;
			}

			if ("AutoMappingAssemblies".Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				var assemblyNames = value.TrimSafe().Split(';');
				_AutoMappingAssemblies.UnionWith(assemblyNames.Where(name => !name.IsNullOrWhiteSpace()).Select(Assembly.Load));
				return;
			}

			if ("AutoMappingConventions".Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				var typeNames = value.TrimSafe().Split(';');
				_AutoMappingConventions.UnionWith(
					typeNames.Where(name => !name.IsNullOrWhiteSpace()).Select(name => Type.GetType(name, true)));
				return;
			}

			if ("DisableSchemas".Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				DisableSchemas = value.ConvertSafe<bool>();
				return;
			}

			base.DoSetConfigProperty(key, value);
		}

		protected override string DoGetConfigProperty(string key)
		{
			if ("AutoMappingsDumpPath".Equals(key, StringComparison.InvariantCulture))
				return AutoMappingsDumpPath;

			if ("AutoMappingAssemblies".Equals(key, StringComparison.InvariantCulture))
			{
				var result = new StringBuilder();
				foreach (var assembly in _AutoMappingAssemblies)
					result.AppendLine(assembly.FullName);
				return result.ToString();
			}

			if ("AutoMappingConventions".Equals(key, StringComparison.InvariantCulture))
			{
				var result = new StringBuilder();
				foreach (var type in _AutoMappingConventions)
					result.AppendLine(type.AssemblyQualifiedName);
				return result.ToString();
			}

			if ("DisableSchemas".Equals(key, StringComparison.InvariantCulture))
				return DisableSchemas.ToString();

			return base.DoGetConfigProperty(key);
		}

		#endregion

		#region Properties, fields, constructors

		protected readonly IEnvironmentService _EnvironmentService;

		public AutoMappedSessionFactoryBuilder(IEnvironmentService environmentService)
		{
			if (environmentService == null)
				throw new ArgumentNullException("environmentService");
			_EnvironmentService = environmentService;
		}

		#endregion
		
		#region Template methods

		protected override void DoFinalizeConfig()
		{
			base.DoFinalizeConfig();
			_HibernateConfiguration.SetNamingStrategy(new QuotedNamesNamingStrategy());

			if (!AutoMappingsDumpPath.IsNullOrWhiteSpace())
			{
				var dumpPath = _EnvironmentService.PhysicalPath(AutoMappingsDumpPath);

				var dirInfo = new DirectoryInfo(dumpPath);
				if (!dirInfo.Exists)
					dirInfo.Create();

				var existingMappings = dirInfo.GetFiles("*.hbm.xml");
				foreach (var fileInfo in existingMappings)
					fileInfo.Delete();

				AutoMappingsDumpPath = dirInfo.FullName;
			}

			if (_AutoMappingConventions.Count == 0)
			{
				_AutoMappingConventions.Add(typeof (ClassConvention));
				_AutoMappingConventions.Add(typeof (PersistentCollectionConvention));
				_AutoMappingConventions.Add(typeof (PropertyConvention));
				_AutoMappingConventions.Add(typeof (ReferenceConvention));
				_AutoMappingConventions.Add(typeof (SubclassConvention));
				_AutoMappingConventions.Add(typeof (UserTypeConvention));
			}

			var autoPersistenceModel = AutoMap.Assemblies(new DefaultAutoMappingConfiguration(), _AutoMappingAssemblies).Conventions.Setup(c =>
			{
				foreach (var type in _AutoMappingConventions.Where(t => typeof(IConvention).IsAssignableFrom(t)))
					c.Add(type);
			});

			foreach (var assembly in _AutoMappingAssemblies)
				autoPersistenceModel.UseOverridesFromAssembly(assembly);

			if (!AutoMappingsDumpPath.IsNullOrWhiteSpace())
				autoPersistenceModel.WriteMappingsTo(AutoMappingsDumpPath);

			
			_HibernateConfiguration = Fluently.Configure(_HibernateConfiguration).Mappings(mappingConfig =>
			{
				foreach (var assembly in _AutoMappingAssemblies)
					mappingConfig.FluentMappings.AddFromAssembly(assembly);

				mappingConfig.AutoMappings.Add(autoPersistenceModel);

			}).BuildConfiguration();
		}

		#endregion
	}
}