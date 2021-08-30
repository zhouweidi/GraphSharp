using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace GraphSharp
{
	public class Node
	{
		public const string DefaultProcessMethod = "Process";

		[Browsable(false)]
		public Graph Graph { get; internal set; }
		[Category("Node")]
		public string Name { get; private set; }
		[Browsable(false)]
		public object UserData { get; set; }

		MethodInfo m_processMethod;
		object m_processInstance;
		InPort[] m_inPorts;
		OutPort[] m_outPorts;
		object[] m_parameters;
		bool[] m_outParameters;
		internal object ReturnValue { get; set; }

		internal object[] Parameters => m_parameters;

		public delegate void OnLinkDelegate(OutPort selfOut, InPort otherIn);
		public delegate void OnUnlinkDelegate(OutPort selfOut, InPort otherIn);
		public delegate void OnOutValuesChangedDelegate(Node node);

		public event OnLinkDelegate OnLink;
		public event OnUnlinkDelegate OnUnlink;
		public event OnOutValuesChangedDelegate OnOutValuesChanged;

		public override string ToString() => Name;

		#region Initialization

		public static Node FromStaticMethod(Type type, string processMethodName = DefaultProcessMethod, string name = null)
		{
			var node = new Node();
			node.StaticMethod(name, type, processMethodName);

			return node;
		}

		public static Node FromInstanceMethod(object processInstance, string processMethodName = DefaultProcessMethod, string name = null)
		{
			if (processInstance is Node)
				throw new ArgumentException($"Unnecessary to create a node for an existing instance of a Node class '{processInstance.GetType().FullName}'", nameof(processInstance));

			var type = processInstance.GetType();
			if (!ExistsParameterlessConstructor(type))
				throw new ArgumentException($"No parameterless constructor defined in class '{type.FullName}'", nameof(processInstance));

			var node = new Node();
			node.InstanceMethod(name, processInstance, processMethodName);

			return node;
		}

		static bool ExistsParameterlessConstructor(Type type) => type.GetConstructor(Type.EmptyTypes) != null;

		Node()
		{ }

		protected Node(string processMethodName = DefaultProcessMethod, string name = null)
		{
			if (!ExistsParameterlessConstructor(GetType()))
				throw new Exception($"No parameterless constructor defined in class '{GetType().FullName}'");

			InstanceMethod(name, this, processMethodName);
		}

		void InstanceMethod(string name, object processInstance, string processMethodName)
		{
			if (processInstance == null)
				throw new ArgumentNullException(nameof(processInstance));

			if (string.IsNullOrEmpty(processMethodName))
				throw new ArgumentException("Empty process method name", nameof(processMethodName));

			Initialize(name, processInstance.GetType(), processMethodName, BindingFlags.Instance);
			m_processInstance = processInstance;
		}

		void StaticMethod(string name, Type type, string processMethodName)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (string.IsNullOrEmpty(processMethodName))
				throw new ArgumentException("Empty process method name", nameof(processMethodName));

			Initialize(name, type, processMethodName, BindingFlags.Static);
		}

		void Initialize(string name, Type type, string processMethodName, BindingFlags bindingFlags)
		{
			// Node name
			Name = name ?? $"{type.Name}.{processMethodName}";

			// Process method
			m_processMethod = type.GetMethod(processMethodName, bindingFlags | BindingFlags.Public | BindingFlags.NonPublic);
			if (m_processMethod == null)
				throw new ArgumentException($"Invalid process method name '{processMethodName}'", nameof(processMethodName));

			if (m_processMethod.DeclaringType == typeof(Node))
				throw new ArgumentException($"The process method '{processMethodName}' belongs to '{typeof(Node)}'", nameof(processMethodName));

			// In ports
			var parameters = m_processMethod.GetParameters();

			m_inPorts = (from p in parameters
						 where !p.IsOut
						 select new InPort(p, this)).ToArray();

			// Out ports
			var outParameters = from z in parameters.Zip(Enumerable.Range(0, parameters.Length))
								where z.First.IsOut
								select new OutPort(z.First, this, z.Second);

			bool returnValueAsOutPort = m_processMethod.ReturnType != typeof(void);
			if (returnValueAsOutPort)
			{
				m_outPorts = outParameters
					.Concat(new[] { new OutPort(m_processMethod.ReturnParameter, this, -1) })
					.ToArray();
			}
			else
				m_outPorts = outParameters.ToArray();

			// Neither in nor out ports
			if (m_inPorts.Length == 0 && m_outPorts.Length == 0)
				throw new Exception($"Neither in ports nor out ports in node '{this}'");

			// Parameters for the process method
			m_parameters = new object[parameters.Length];
			m_outParameters = parameters.Select(p => p.IsOut).ToArray();
		}

		#endregion

		#region Port getters

		[Browsable(false)]
		public IReadOnlyList<InPort> InPorts => m_inPorts;
		[Browsable(false)]
		public IReadOnlyList<OutPort> OutPorts => m_outPorts;

		public InPort GetInPort(string name = null)
		{
			if (m_inPorts.Length == 0)
				throw new Exception($"No in port exists in '{this}'");

			// Return the default in port only if these is one in port
			if (string.IsNullOrEmpty(name))
			{
				if (m_inPorts.Length != 1)
					throw new AmbiguousMatchException($"No default in port found due to multiple ones exist in '{this}'");

				return m_inPorts[0];
			}
			else
				return m_inPorts.FirstOrDefault(p => p.Name == name);
		}

		public OutPort GetOutPort(string name = null)
		{
			if (m_outPorts.Length == 0)
				throw new Exception($"No out port exists in '{this}'");

			// Return the default out port only if these is one out port or an out port with a 'null' name (return value)
			if (string.IsNullOrEmpty(name) && m_outPorts.Length == 1)
				return m_outPorts[0];
			else
			{
				var r = m_outPorts.FirstOrDefault(p => p.Name == name);

				if (r == null && string.IsNullOrEmpty(name))
					throw new AmbiguousMatchException($"No default out port found due to multiple ones exist in '{this}'");

				return r;
			}
		}

		#endregion

		#region Link/Unlink

		public bool IsLinkable(OutPort selfOut, InPort otherIn)
		{
			try
			{
				CheckLinkable(selfOut, otherIn);
				return true;
			}
			catch
			{
				return false;
			}
		}

		void CheckLinkable(OutPort selfOut, InPort otherIn)
		{
			if (selfOut == null)
				throw new ArgumentNullException(nameof(selfOut));

			if (otherIn == null)
				throw new ArgumentNullException(nameof(otherIn));

			// My out port?
			if (selfOut.Owner != this)
				throw new ArgumentException($"The out port '{selfOut}' doesn't belong to node '{this}' to link", nameof(selfOut));

			// I have already linked to the target
			if (selfOut.EndPorts.Contains(otherIn))
			{
				if (otherIn.EndPort != selfOut)
					throw new Exception($"A bad existing link '{selfOut} -> {otherIn}' found (the target port doesn't link back)");

				throw new Exception($"The link '{selfOut} -> {otherIn}' already exists");
			}

			// The other has already linked?
			if (otherIn.EndPort != null)
			{
				if (otherIn.EndPort == selfOut)
					throw new Exception($"A bad existing link '{selfOut} -> {otherIn}' found (the in port considers the link exists)");

				throw new InvalidOperationException($"The target in port '{otherIn}' already has a link to another '{otherIn.EndPort}'");
			}

			// Circular dependency?
			if (DetectCycle(otherIn.Owner))
				throw new InvalidOperationException($"Circular dependency detected from '{selfOut} -> {otherIn}'");

			// Type linkable?
			if (!IsLinkableType(selfOut, otherIn))
				throw new InvalidOperationException($"Unmatched port types of '{selfOut} -> {otherIn}' ({selfOut.ValueType} -> {otherIn.ValueType})");
		}

		public void Link(InPort otherIn) => Link(GetOutPort(null), otherIn);

		public void Link(OutPort selfOut, InPort otherIn)
		{
			CheckLinkable(selfOut, otherIn);

			selfOut.AddEndPort(otherIn);
			otherIn.EndPort = selfOut;

			OnLink?.Invoke(selfOut, otherIn);
		}

		bool DetectCycle(Node linkToNode)
		{
			if (linkToNode == this)
				return true;

			var q = new Queue<Node>();
			q.Enqueue(linkToNode);

			do
			{
				var node = q.Dequeue();

				foreach (var selfOut in node.m_outPorts)
				{
					foreach (var otherIn in selfOut.EndPorts)
					{
						var otherNode = otherIn.Owner;
						if (otherNode == this)
							return true;

						q.Enqueue(otherNode);
					}
				}

			} while (q.Count > 0);

			return false;
		}

		bool IsLinkableType(OutPort selfOut, InPort otherIn)
		{
			var src = selfOut.ValueType;
			if (!src.IsByRef)
				src = src.MakeByRefType();

			var dest = otherIn.ValueType;
			if (!dest.IsByRef)
				dest = dest.MakeByRefType();

			if (dest.IsAssignableFrom(src))
				return true;

			return false;
		}

		public void Unlink(InPort otherIn) => Unlink(GetOutPort(null), otherIn);

		public void Unlink(OutPort selfOut, InPort otherIn)
		{
			if (selfOut == null)
				throw new ArgumentNullException(nameof(selfOut));

			if (otherIn == null)
				throw new ArgumentNullException(nameof(otherIn));

			// My out port?
			if (selfOut.Owner != this)
				throw new ArgumentException($"The out port '{selfOut}' doesn't belong to node '{this}' to unlink", nameof(selfOut));

			// No such link?
			if (!selfOut.EndPorts.Contains(otherIn))
				return;

			// The other doesn't link to me?
			if (otherIn.EndPort != selfOut)
				throw new InvalidOperationException($"A bad existing link '{selfOut} -> {otherIn}' found (the target in port doesn't link back)");

			otherIn.EndPort = null;
			selfOut.RemoveEndPort(otherIn);

			OnUnlink?.Invoke(selfOut, otherIn);
		}

		internal void RemoveLinks()
		{
			foreach (var selfIn in m_inPorts)
			{
				var otherOut = selfIn.EndPort;
				if (otherOut != null)
					otherOut.Owner.Unlink(otherOut, selfIn);
			}

			foreach (var selfOut in m_outPorts)
			{
				if (selfOut.EndPorts.Count > 0)
				{
					foreach (var otherIn in selfOut.EndPorts.ToArray())
						Unlink(selfOut, otherIn);
				}
			}
		}

		#endregion

		#region Evaluation

		public void Evaluate()
		{
			ResetOutValues();

			CheckInPortsConnected(true);

			EvaluateHelper();
		}

		internal void EvaluateHelper()
		{
			// Setup parameters
			{
				int inPortIndex = 0;

				for (int i = 0; i < m_parameters.Length; i++)
				{
					if (m_outParameters[i])
						m_parameters[i] = null;
					else
					{
						m_parameters[i] = m_inPorts[inPortIndex].LoadValue();
						inPortIndex++;
					}
				}

				ReturnValue = null;
			}

			// Call the process method
			ReturnValue = m_processMethod.Invoke(m_processInstance, m_parameters);

			OnOutValuesChanged?.Invoke(this);
		}

		internal bool CheckInPortsConnected(bool throwException)
		{
			// Check all in ports connected
			if (m_inPorts.Length > 0)
			{
				var unlinked = m_inPorts.FirstOrDefault(p => p.EndPort == null);
				if (unlinked != null)
				{
					if(throwException)
						throw new Exception($"The in port '{unlinked}' is not linked");

					return false;
				}
			}

			return true;
		}

		internal bool NoneOfOutPortsConnected() => m_outPorts.Length == 0 || m_outPorts.All(p => p.EndPorts.Count == 0);

		internal void ResetOutValues()
		{
			foreach (var p in m_outPorts)
				p.ResetValue();

			OnOutValuesChanged?.Invoke(this);
		}

		#endregion

		#region Serialization

		protected virtual void OnSave(Utf8JsonWriter writer)
		{ }

		internal void Save(Utf8JsonWriter writer, IReadOnlyList<Node> nodes)
		{
			writer.WriteStartObject();

			// Node block
			writer.WriteStartObject("Node");
			{
				// Basic
				writer.WriteString("NodeName", Name);

				writer.WriteString("MethodContainerType", m_processMethod.DeclaringType.AssemblyQualifiedName);

				var instanceType = m_processInstance != null ?
					m_processInstance.GetType().AssemblyQualifiedName :
					"";
				writer.WriteString("InstanceType", instanceType);

				writer.WriteString("MethodName", m_processMethod.Name);

				// Out ports
				if (m_outPorts.Length > 0)
				{
					writer.WriteStartArray("OutPorts");

					foreach (var p in m_outPorts)
						SaveOutPort(writer, nodes, p);

					writer.WriteEndArray();
				}
			}
			writer.WriteEndObject();

			// Custom data block (call back for 'this' instance only)
			if (m_processInstance == this)
			{
				writer.WriteStartObject("CustomData");
				OnSave(writer);
				writer.WriteEndObject();
			}

			writer.WriteEndObject();
		}

		static void SaveOutPort(Utf8JsonWriter writer, IReadOnlyList<Node> nodes, OutPort outPort)
		{
			writer.WriteStartObject();

			// Type & name
			var outPortName = string.IsNullOrWhiteSpace(outPort.Name) ? "" : outPort.Name;
			writer.WriteString("Name", outPortName);

			writer.WriteString("Type", outPort.ValueType.AssemblyQualifiedName);

			// End ports
			if (outPort.EndPorts.Count > 0)
			{
				writer.WriteStartArray("EndPorts");

				foreach (var ep in outPort.EndPorts)
				{
					var nodeIndex = -1;
					for (int i = 0; i < nodes.Count; i++)
					{
						if (nodes[i] == ep.Owner)
						{
							nodeIndex = i;
							break;
						}
					}

					if (nodeIndex < 0)
						throw new Exception($"No end point node found for out port '{outPort}'");

					writer.WriteStartObject();
					writer.WriteNumber("NodeIndex", nodeIndex);
					writer.WriteString("InPort", ep.Name);
					writer.WriteEndObject();
				}

				writer.WriteEndArray();
			}

			writer.WriteEndObject();
		}

		protected virtual void OnLoad(JsonElement element)
		{ }

		internal class OutPortLinks
		{
			public class EndPort
			{
				public int NodeIndex { get; private set; }
				public string InPortName { get; private set; }

				public EndPort(int nodeIndex, string inPortName)
				{
					NodeIndex = nodeIndex;
					InPortName = inPortName;
				}
			}

			public string Name { get; private set; }
			List<EndPort> m_endPorts;
			public IReadOnlyList<EndPort> EndPorts => m_endPorts;

			public OutPortLinks(string name, int endPortsCount = 0)
			{
				Name = name;
				m_endPorts = new List<EndPort>(endPortsCount);
			}

			public void AddEndPort(int nodeIndex, string inPortName)
			{
				m_endPorts.Add(new EndPort(nodeIndex, inPortName));
			}
		}

		internal static (Node, IReadOnlyList<OutPortLinks>) Load(JsonElement element, ILoader loader)
		{
			// Node block
			var nodeElement = element.GetProperty("Node");

			// Basic
			var nodeName = nodeElement.GetProperty("NodeName").GetString();

			var methodContainerTypeName = nodeElement.GetProperty("MethodContainerType").GetString();
			var methodContainerType = loader.FindType(methodContainerTypeName);

			var instanceTypeName = nodeElement.GetProperty("InstanceType").GetString();
			object instance = string.IsNullOrEmpty(instanceTypeName) ? null : loader.CreateInstance(instanceTypeName);

			var methodName = nodeElement.GetProperty("MethodName").GetString();

			var bindingFlags = instance != null ? BindingFlags.Instance : BindingFlags.Static;
			var methodInfo = methodContainerType.GetMethod(methodName, bindingFlags | BindingFlags.Public | BindingFlags.NonPublic);
			if (methodInfo == null)
				throw new Exception($"The process method '{methodName}' is not found in '{methodContainerType}'");

			// Out ports
			List<OutPortLinks> nodeLinks = null;

			JsonElement outPorts;
			if (nodeElement.TryGetProperty("OutPorts", out outPorts))
			{
				var outPortsCount = outPorts.GetArrayLength();
				if (outPortsCount > 0)
				{
					nodeLinks = new List<OutPortLinks>(outPortsCount);

					foreach (var op in outPorts.EnumerateArray())
						LoadOutPort(op, loader, methodInfo, nodeLinks);
				}
			}

			// Initialize node
			var node = instance as Node;
			if (node != null)
			{
				node.InstanceMethod(nodeName, node, methodName);

				// Custom data block (call back for 'this' instance only)
				var customData = element.GetProperty("CustomData");
				node.OnLoad(customData);
			}
			else
			{
				// Create naked node
				node = instance != null ?
					FromInstanceMethod(instance, methodName, nodeName) :
					FromStaticMethod(methodContainerType, methodName, nodeName);
			}

			return (node, nodeLinks);
		}

		static void LoadOutPort(JsonElement op, ILoader loader, MethodInfo methodInfo, List<OutPortLinks> nodeLinks)
		{
			// Type & name
			var outPortName = op.GetProperty("Name").GetString();
			if (outPortName == "")
				outPortName = null;

			var outPortValueTypeName = op.GetProperty("Type").GetString();
			var outPortValueType = loader.FindType(outPortValueTypeName);

			ValidateOutParameter(methodInfo, outPortName, outPortValueType);

			// End ports
			JsonElement endPorts;
			if (op.TryGetProperty("EndPorts", out endPorts))
			{
				var endPortsCount = endPorts.GetArrayLength();
				if (endPortsCount > 0)
				{
					var opLinks = new OutPortLinks(outPortName, endPortsCount);
					nodeLinks.Add(opLinks);

					foreach (var ep in endPorts.EnumerateArray())
					{
						int nodeIndex = ep.GetProperty("NodeIndex").GetInt32();
						var inPortName = ep.GetProperty("InPort").GetString();

						opLinks.AddEndPort(nodeIndex, inPortName);
					}
				}
			}
		}

		static void ValidateOutParameter(MethodInfo methodInfo, string parameterName, Type parameterType)
		{
			bool isReturnType = string.IsNullOrEmpty(parameterName);
			if (isReturnType)
			{
				if (methodInfo.ReturnType != parameterType)
					throw new Exception($"The return type '{methodInfo.ReturnType}' of method '{methodInfo}' doesn't match the expected type '{parameterType}'");
			}
			else
			{
				var parameter = (from p in methodInfo.GetParameters()
								 where p.IsOut && p.Name == parameterName
								 select p).FirstOrDefault();

				if (parameter == null)
					throw new Exception($"No out parameter '{parameterName}' found in method '{methodInfo.Name}'");

				if (parameter.ParameterType != parameterType)
					throw new Exception($"The type '{parameter.ParameterType}' of parameter '{parameterName}' of method '{methodInfo}' doesn't match the expected type '{parameterType}'");
			}
		}

		internal void LoadLinks(IEnumerable<OutPortLinks> nodeLinks, IReadOnlyList<Node> nodes)
		{
			foreach (var link in nodeLinks)
			{
				var outPort = GetOutPort(link.Name);
				if (outPort == null)
					throw new Exception($"No out port '{outPort}' found for loading links");

				foreach (var ep in link.EndPorts)
				{
					var node = nodes[ep.NodeIndex];

					var inPort = node.GetInPort(ep.InPortName);
					if (inPort == null)
						throw new Exception($"No in port '{inPort}' found to link for loading links");

					Link(outPort, inPort);
				}
			}
		}

		#endregion
	}
}
