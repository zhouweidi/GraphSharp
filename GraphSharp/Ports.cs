using System;
using System.Collections.Generic;
using System.Reflection;

namespace GraphSharp
{
	public abstract class Port
	{
		internal ParameterInfo TypeInfo { get; private set; }
		public Node Owner { get; private set; }
		public string Name => TypeInfo.Name;
		internal Type ValueType => TypeInfo.ParameterType;

		protected Port(ParameterInfo typeInfo, Node owner)
		{
			TypeInfo = typeInfo;
			Owner = owner;
		}

		public override string ToString() => $"{Owner}.{Name}";
	}


	public sealed class InPort : Port
	{
		public OutPort EndPort { get; internal set; }

		internal InPort(ParameterInfo typeInfo, Node owner)
			: base(typeInfo, owner)
		{ }

		internal object LoadValue()
		{
			if (EndPort == null)
				throw new Exception($"The in port '{this}' is not linked");

			if (!EndPort.HasValue)
				throw new Exception($"The in port '{this}' has not fetched a value from out port '{EndPort}'");

			return EndPort.Value;
		}
	}


	public sealed class OutPort : Port
	{
		static readonly object NoOutValue = new object();

		List<InPort> m_endPorts = new List<InPort>();
		int m_parameterIndex;

		public IReadOnlyList<InPort> EndPorts => m_endPorts;
		public object Value => IsReturnValue ? Owner.ReturnValue : Owner.Parameters[m_parameterIndex];
		public override string ToString() => $"{Owner}.{(IsReturnValue ? "<Ret>" : Name)}";
		bool IsReturnValue => m_parameterIndex < 0;

		internal OutPort(ParameterInfo typeInfo, Node owner, int parameterIndex)
			: base(typeInfo, owner)
		{
			m_parameterIndex = parameterIndex;
		}

		internal void AddEndPort(InPort otherIn)
		{
			m_endPorts.Add(otherIn);
		}

		internal void RemoveEndPort(InPort otherIn)
		{
			m_endPorts.Remove(otherIn);
		}

		public bool HasValue => !ReferenceEquals(Value, NoOutValue);

		internal void ResetValue()
		{
			if (Value != NoOutValue)
			{
				if (m_parameterIndex < 0)
					Owner.ReturnValue = NoOutValue;
				else
					Owner.Parameters[m_parameterIndex] = NoOutValue;
			}
		}
	}
}
