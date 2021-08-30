using System;

namespace GraphSharp
{
	public interface ILoader
	{
		Type FindType(string typeName);
		object CreateInstance(string typeName);
	}


	public class DefaultLoader : ILoader
	{
		public static readonly DefaultLoader Instance = new DefaultLoader();

		public Type FindType(string typeName)
		{
			var type = Type.GetType(typeName, false);
			if (type == null)
				throw new Exception($"The type '{typeName}' is not found");

			return type;
		}

		public object CreateInstance(string typeName)
		{
			var type = FindType(typeName);

			return Activator.CreateInstance(type);
		}
	}
}
