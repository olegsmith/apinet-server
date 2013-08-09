﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NHibernate.Criterion;
using NHibernate.SqlCommand;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using AGO.Hibernate.Attributes.Model;
using AGO.Hibernate.Json;
using AGO.Hibernate.Model;

namespace AGO.Hibernate.Filters
{
	public class FilteringService : AbstractService, IFilteringService
	{
		#region Configuration properties, fields and methods

		protected FilteringServiceOptions _Options = new FilteringServiceOptions();
		public FilteringServiceOptions Options
		{
			get { return _Options; }
			set { _Options = value ?? _Options; }
		}

		protected override void DoSetConfigProperty(string key, string value)
		{
			if ("FormattingCulture".Equals(key))
			{
				_Options.FormattingCulture = CultureInfo.CreateSpecificCulture(value.TrimSafe());
				return;
			}

			_Options.SetMemberValue(key, value);
		}

		protected override string DoGetConfigProperty(string key)
		{
			if ("FormattingCulture".Equals(key))
				return _Options.FormattingCulture != null ? _Options.FormattingCulture.Name : null;

			return _Options.GetMemberValue(key).ToStringSafe();
		}

		#endregion

		#region Properties, fields, constructors

		protected readonly JsonSchema _ModelNodeSchema;

		protected readonly IJsonService _JsonService;

		public FilteringService(IJsonService jsonService)
		{
			if (jsonService == null)
				throw new ArgumentNullException("jsonService");
			_JsonService = jsonService;

			var modelNodeSchemaStream = typeof(AbstractFilterNode).Assembly.GetManifestResourceStream(
				"AGO.Hibernate.Filters.ModelFilterNodeSchema.json");
			if (modelNodeSchemaStream == null)
				throw new InvalidOperationException();
			var valueNodeSchemaStream = typeof(AbstractFilterNode).Assembly.GetManifestResourceStream(
				"AGO.Hibernate.Filters.ValueFilterNodeSchema.json");
			if (valueNodeSchemaStream == null)
				throw new InvalidOperationException();

			var schemaResolver = new JsonSchemaResolver();
			JsonSchema.Read(_JsonService.CreateReader(
				new StreamReader(valueNodeSchemaStream), true), schemaResolver);
			_ModelNodeSchema = JsonSchema.Read(_JsonService.CreateReader(
				new StreamReader(modelNodeSchemaStream), true), schemaResolver);
		}

		#endregion

		#region Interfaces implementation

		public void ValidateFilter(IModelFilterNode node, Type modelType)
		{
			if (node == null)
				throw new ArgumentNullException("node");
			if (modelType == null)
				throw new ArgumentNullException("modelType");

			try
			{
				ValidateModelFilterNode(node, modelType);
			}
			catch (Exception e)
			{
				throw new FilterValidationException(e);
			}
		}

		public IModelFilterNode ParseFilterFromJson(TextReader reader, Type validateForModelType = null)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");
			IModelFilterNode result;

			try
			{				
				var jsonReader =_JsonService.CreateValidatingReader(reader, _ModelNodeSchema);
				using (jsonReader)
				{
					var node = new ModelFilterNode { Operator = ModelFilterOperators.And };
					result = node;
					var tokens = JToken.ReadFrom(jsonReader) as IEnumerable<JToken>;
					if (tokens == null)
						throw new EmptyFilterDeserializationResultException();
					ProcessModelFilterTokens(tokens, node);
				}
			}
			catch (Exception e)
			{
				throw new FilterJsonException(e);
			}

			if (validateForModelType == null)
				return result;

			ValidateFilter(result, validateForModelType);
			return result;
		}

		public IModelFilterNode ParseFilterFromJson(string str, Type validateForModelType = null)
		{
			if (str.IsNullOrWhiteSpace())
				throw new ArgumentNullException("str");

			return ParseFilterFromJson(new StringReader(str), validateForModelType);
		}

		public IModelFilterNode ParseFilterFromJson(Stream stream, Type validateForModelType = null)
		{
			if (stream == null)
				throw new ArgumentNullException("stream");

			return ParseFilterFromJson(new StreamReader(stream, true), validateForModelType);			
		}

		public string GenerateJsonFromFilter(IModelFilterNode node)
		{
			if (node == null)
				throw new ArgumentNullException("node");

			try
			{
				var sb = new StringBuilder();
				var writer = new StringWriter(sb);
				_JsonService.CreateSerializer().Serialize(writer, node);
				writer.Flush();

				return sb.ToString();
			}
			catch (Exception e)
			{
				throw new FilterJsonException(e);
			}		
		}

		public DetachedCriteria CompileFilter(IModelFilterNode node, Type modelType)
		{
			if (node == null)
				throw new ArgumentNullException("node");
			if (modelType == null)
				throw new ArgumentNullException("modelType");

			try
			{
				ValidateModelFilterNode(node, modelType);

				var criteria = DetachedCriteria.For(modelType);
				criteria.Add(CompileModelFilterNode(node, criteria, modelType, null, new HashSet<string>(), false));
				return criteria;
			}
			catch (Exception e)
			{
				throw new FilterCompilationException(e);
			}	
		}

		public IModelFilterNode ConcatFilters(
			IEnumerable<IModelFilterNode> nodes,
			ModelFilterOperators op = ModelFilterOperators.And)
		{
			if (nodes == null)
				throw new ArgumentNullException("nodes");

			try
			{
				var result = new ModelFilterNode { Operator = op };

				foreach (var node in nodes)
				{
					if (node == null)
						continue;
					result.AddItem((IFilterNode) node.Clone());
				}

				return result;			
			}
			catch (Exception e)
			{
				throw new FilterConcatenationException(e);
			}	
		}

		public IModelFilterBuilder<TModel, TModel> Filter<TModel>() 
			where TModel : class, IIdentifiedModel
		{
			return new ModelFilterBuilder<TModel, TModel>(null, null) { Operator = ModelFilterOperators.And };
		}

		#endregion

		#region Template methods

		protected override void DoFinalizeConfig()
		{
			base.DoFinalizeConfig();

			_Options.FormattingCulture = _Options.FormattingCulture ?? CultureInfo.InvariantCulture;
		}

		protected override void DoInitialize()
		{
			base.DoInitialize();

			var initializable = _JsonService as IInitializable;
			if (initializable != null)
				initializable.Initialize();
		}

		#endregion

		#region Helper methods

		protected void ValidateModelFilterNode(
			IModelFilterNode node, 
			Type modelType)
		{
			if (!typeof(IIdentifiedModel).IsAssignableFrom(modelType))
				throw new UnexpectedTypeException(modelType);
			
			foreach (var item in node.Items)
			{
				PropertyInfo propertyInfo = null;
				if (!item.Path.IsNullOrWhiteSpace())
				{
					propertyInfo = modelType.GetProperty(item.Path.Trim(), BindingFlags.Public | BindingFlags.Instance);
					if (propertyInfo == null)
						throw new MissingModelPropertyException(item.Path, modelType);

					var notMappedAttribute = propertyInfo.FirstAttribute<NotMappedAttribute>(true);
					if (notMappedAttribute != null)
						throw new NotMappedModelPropertyException(propertyInfo);
				}

				var modelFilterItem = item as IModelFilterNode;
				var valueFilterItem = item as IValueFilterNode;

				if (modelFilterItem != null)
				{
					var innerModelType = modelType;

					if (propertyInfo != null)
					{
						innerModelType = propertyInfo.PropertyType;
						if (typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.PropertyType.IsGenericType)
							innerModelType = propertyInfo.PropertyType.GetGenericArguments()[0];
					}

					ValidateModelFilterNode(modelFilterItem, innerModelType);
				}

				if (valueFilterItem == null)
					continue;

				if (item.Path.IsNullOrEmpty())
					throw new EmptyNodePathException();
				if (propertyInfo == null)
					throw new MissingModelPropertyException(item.Path, modelType);

				ValidateValueFilterNode(valueFilterItem, propertyInfo);		
			}
		}

		protected void ValidateValueFilterNode(IValueFilterNode node, PropertyInfo propertyInfo)
		{
			var propertyType = propertyInfo.PropertyType;
			if (typeof (IEnumerable<IIdentifiedModel>).IsAssignableFrom(propertyType))
				propertyType = propertyType.GetGenericArguments()[0];

			if (!propertyType.IsValue() && !typeof(string).IsAssignableFrom(propertyType) &&
					!typeof(IIdentifiedModel).IsAssignableFrom(propertyType))
				throw new UnexpectedTypeException(propertyType);

			var operandEmpty = node.Operand.IsNullOrWhiteSpace();

			if (node.IsBinary && operandEmpty)
				throw new InvalidFilterOperandException(propertyInfo);

			if (typeof(string).IsAssignableFrom(propertyType))
				ValidateStringValueFilterNode(node, propertyInfo);
			else if (propertyType.IsValueType)
				ValidatePrimitiveValueFilterNode(node, propertyInfo);
			else if (typeof (IIdentifiedModel).IsAssignableFrom(propertyType))
			{
				ValidateModelValueFilterNode(node, propertyInfo);

				var idProperty = propertyType.IsClass 
					? propertyType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance) 
					: null;
				if (idProperty != null)
					propertyType = idProperty.PropertyType;
			}

			if (!operandEmpty && node.Operand.ConvertSafe(propertyType, _Options.FormattingCulture) == null)
				throw new InvalidFilterOperandException(propertyInfo);
		}

		protected void ValidateStringValueFilterNode(IValueFilterNode node, PropertyInfo propertyInfo)
		{
			if (node.IsUnary)
				return;

			var op = node.Operator ?? ValueFilterOperators.Eq;

			if (!node.IsBinary)
				throw new InvalidValueFilterOperatorException(op, propertyInfo);

			if (op == ValueFilterOperators.Gt || op == ValueFilterOperators.Lt ||
					op == ValueFilterOperators.Ge || op == ValueFilterOperators.Le)
				throw new InvalidValueFilterOperatorException(op, propertyInfo);
		}

		protected void ValidatePrimitiveValueFilterNode(IValueFilterNode node, PropertyInfo propertyInfo)
		{
			var op = node.Operator ?? ValueFilterOperators.Eq;

			if (op == ValueFilterOperators.Like)
				throw new InvalidValueFilterOperatorException(op, propertyInfo);
		}

		protected void ValidateModelValueFilterNode(IValueFilterNode node, PropertyInfo propertyInfo)
		{
			var op = node.Operator ?? ValueFilterOperators.Eq;

			if (op != ValueFilterOperators.Eq && op != ValueFilterOperators.Exists)
				throw new InvalidValueFilterOperatorException(op, propertyInfo);
		}

		protected Junction CompileModelFilterNode(
			IModelFilterNode node,
			DetachedCriteria criteria,
			Type modelType, 
			string alias,
			ISet<string> registeredAliases,
			bool negative)
		{
			var op = node.Operator ?? ModelFilterOperators.And;

			var result = op == ModelFilterOperators.And 
				? (Junction) Restrictions.Conjunction() 
				: Restrictions.Disjunction();

			negative = node.Negative ? !negative : negative;

			var itemsCount = 0;
			foreach (var item in node.Items)
			{
				itemsCount++;

				PropertyInfo propertyInfo = null;
				if (!item.Path.IsNullOrEmpty())
				{
					propertyInfo = modelType.GetProperty(item.Path, BindingFlags.Public | BindingFlags.Instance);
					if (propertyInfo == null)
						throw new MissingModelPropertyException(item.Path, modelType);
				}
				
				var modelFilterItem = item as IModelFilterNode;
				var valueFilterItem = item as IValueFilterNode;

				if (modelFilterItem != null)
				{
					var newModelType = modelType;
					var newAlias = alias;

					if (propertyInfo != null)
					{
						newModelType = propertyInfo.PropertyType;
						if (typeof (IEnumerable).IsAssignableFrom(propertyInfo.PropertyType) && propertyInfo.PropertyType.IsGenericType)
							newModelType = propertyInfo.PropertyType.GetGenericArguments()[0];

						var newPath = alias;
						if (!newPath.IsNullOrEmpty())
							newPath += '.';
						newPath += item.Path;

						newAlias = newPath.Replace('.', '_');

						if (!registeredAliases.Contains(newAlias))
						{
							criteria.CreateAlias(newPath, newAlias, JoinType.LeftOuterJoin);
							registeredAliases.Add(newAlias);
						}
					}

					result.Add(CompileModelFilterNode(modelFilterItem, criteria, newModelType, newAlias, registeredAliases, negative));
				}

				if (valueFilterItem == null)
					continue;

				if (propertyInfo == null)
					throw new MissingModelPropertyException(item.Path, modelType);

				result.Add(CompileValueFilterNode(valueFilterItem, criteria,
					propertyInfo, valueFilterItem.Path, alias, registeredAliases, negative));		
			}

			if (itemsCount == 0)
				result.Add(Restrictions.IsNotNull("Id"));

			return result;
		}

		protected ICriterion CompileValueFilterNode(
			IValueFilterNode node,
			DetachedCriteria criteria,
			PropertyInfo propertyInfo,
			string path,
			string alias,
			ISet<string> registeredAliases,
			bool negative)
		{
			ICriterion result = null;

			var propertyType = propertyInfo.PropertyType;
			var isTimestamp = propertyInfo.GetCustomAttributes(typeof (TimestampAttribute), false).Length > 0;
			var isModelsCollection = typeof (IEnumerable<IIdentifiedModel>).IsAssignableFrom(propertyType);		
			var realPath = path;
			if (!alias.IsNullOrEmpty())
				realPath = alias + '.' + realPath;
			var realType = propertyType;

			if (isModelsCollection)
			{
				propertyType = propertyType.GetGenericArguments()[0];

				var newAlias = realPath.Replace('.', '_');
				if (!registeredAliases.Contains(realPath))
				{
					criteria.CreateAlias(realPath, newAlias, JoinType.LeftOuterJoin);
					registeredAliases.Add(realPath);
				}
				realPath = newAlias;
			}

			if(typeof(IIdentifiedModel).IsAssignableFrom(propertyType))
			{
				realPath = realPath + ".Id";
				var idProperty = propertyType.IsClass ? propertyType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance) : null;
				if (idProperty != null)
					realType = idProperty.PropertyType;
			}

			var value = node.Operand.ConvertSafe(realType, _Options.FormattingCulture);
			DateTime? dayStart = null;
			DateTime? nextDay = null;
			if (value is DateTime? && !isTimestamp)
			{
				var dateValue = ((DateTime) value).ToLocalTime();
				dayStart = DateTime.SpecifyKind(new DateTime(dateValue.Year, dateValue.Month, dateValue.Day), DateTimeKind.Local);
				nextDay = dayStart.Value.AddDays(1).ToUniversalTime();
				dayStart = dayStart.ToUniversalTime();
			}

			if (node.Operator == ValueFilterOperators.Eq)
			{
				result = dayStart != null
					? Restrictions.Ge(realPath, dayStart.Value) && Restrictions.Lt(realPath, nextDay.Value)
					: Restrictions.Eq(realPath, value);
			}
			else if (node.Operator == ValueFilterOperators.Exists)
				result = Restrictions.Not(Restrictions.IsNull(realPath));
			else if (node.Operator == ValueFilterOperators.Like)
			{
				var str = node.Operand.TrimSafe();
				var mode = MatchMode.Exact;
				var prefixed = str.StartsWith("%");
				var suffixed = str.EndsWith("%");
				if (prefixed && suffixed)
					mode = MatchMode.Anywhere;
				else if (prefixed)
					mode = MatchMode.End;
				else if (suffixed)
					mode = MatchMode.Start;
				result = Restrictions.Like(realPath, str.Replace("%", ""), mode);
			}
			else if (node.Operator == ValueFilterOperators.Lt)
			{
				result = dayStart != null 
					? Restrictions.Lt(realPath, dayStart.Value)
					: Restrictions.Lt(realPath, value);
			}
			else if (node.Operator == ValueFilterOperators.Gt)
			{
				result = dayStart != null
					? Restrictions.Ge(realPath, nextDay.Value)
					: Restrictions.Gt(realPath, value);
			}
			else if (node.Operator == ValueFilterOperators.Le)
			{
				result = dayStart != null
					? Restrictions.Lt(realPath, nextDay.Value)
					: Restrictions.Le(realPath, value);
			}
			else if (node.Operator == ValueFilterOperators.Ge)
			{
				result = dayStart != null
					? Restrictions.Ge(realPath, dayStart.Value)
					: Restrictions.Ge(realPath, value);
			}

			if (result == null)
				throw new InvalidOperationException("Unexpected operator type");
			
			negative = node.Negative ? !negative : negative;
			return negative ? Restrictions.Not(result) : result;
		}

		internal void ProcessModelFilterTokens(IEnumerable<JToken> tokens, ModelFilterNode current)
		{
			foreach (var property in tokens.OfType<JProperty>())
			{
				if ("path".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Path = property.Value.TokenValue().TrimSafe();
				else if ("op".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Operator = ModelFilterOperatorFromToken(property.Value);
				else if ("not".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Negative = property.Value.TokenValue().ConvertSafe<bool>(_Options.FormattingCulture);
				else if (!"items".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					continue;

				var array = property.Value as JArray;
				if (array == null)
					continue;

				foreach (var obj in array.OfType<JObject>())
				{
					var opProperty = obj.OfType<JProperty>().FirstOrDefault(
					p => "op".Equals(p.Name, StringComparison.InvariantCultureIgnoreCase));
					if (opProperty == null)
						continue;

					var modelOp = ModelFilterOperatorFromToken(opProperty.Value);
					var valueOp = ValueFilterOperatorFromToken(opProperty.Value);

					if (modelOp != null)
					{
						var newModelNode = new ModelFilterNode();
						current.AddItem(newModelNode);
						ProcessModelFilterTokens(obj, newModelNode);
					}
					if (valueOp == null)
						continue;

					var newValueNode = new ValueFilterNode();
					current.AddItem(newValueNode);
					ProcessValueFilterTokens(obj, newValueNode);
				}
			}
		}

		internal void ProcessValueFilterTokens(IEnumerable<JToken> tokens, ValueFilterNode current)
		{
			foreach (var property in tokens.OfType<JProperty>())
			{
				if ("path".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Path = property.Value.TokenValue().TrimSafe();
				else if ("op".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Operator = ValueFilterOperatorFromToken(property.Value);
				else if ("not".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Negative = property.Value.TokenValue().ConvertSafe<bool>(_Options.FormattingCulture);
				else if ("value".Equals(property.Name, StringComparison.InvariantCultureIgnoreCase))
					current.Operand = property.Value.TokenValue();
		}
		}

		protected ValueFilterOperators? ValueFilterOperatorFromToken(JToken token)
		{
			var value = token.TokenValue().TrimSafe();

			var exact = value.ParseEnumSafe<ValueFilterOperators>();
			if (exact != null)
				return exact;

			foreach (var pair in ValueFilterNode.OperatorConversionTable.Where(
					pair => pair.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase)))
				return pair.Key;
			return null;
		}

		protected ModelFilterOperators? ModelFilterOperatorFromToken(JToken token)
		{
			var value = token.TokenValue().TrimSafe();

			var exact = value.ParseEnumSafe<ModelFilterOperators>();
			if (exact != null)
				return exact;

			foreach (var pair in ModelFilterNode.OperatorConversionTable.Where(
					pair => pair.Value.Equals(value, StringComparison.InvariantCultureIgnoreCase)))
				return pair.Key;
			return null;
		}

		#endregion
	}
}