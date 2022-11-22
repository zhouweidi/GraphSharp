using System;

namespace GraphSharp
{
	public interface ILoader
	{
		Type FindType(string typeName);
		INodeHandler CreateHandler(Type type);
		object CreateMethodContainer(Type type, INodeHandler handler);
	}


	public class DefaultLoader : ILoader
	{
		public static readonly DefaultLoader Instance = new DefaultLoader();

		public virtual Type FindType(string typeName)
		{
			var type = Type.GetType(typeName, false);
			if (type == null)
				throw new Exception($"The type '{typeName}' is not found");

			return type;
		}

		public virtual INodeHandler CreateHandler(Type type)
		{
			return (INodeHandler)Activator.CreateInstance(type);
		}

		public virtual object CreateMethodContainer(Type type, INodeHandler handler)
		{
			try
			{
				return Activator.CreateInstance(type, new[] { handler });
			}
			catch (MissingMethodException)
			{
				return Activator.CreateInstance(type);
			}
		}
	}
}
