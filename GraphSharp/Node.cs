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
		[Browsable(false)]
		public Graph Graph { get; internal set; }
		[Browsable(false)]
		public string Name { get; private set; }
		[Browsable(false)]
		public INodeHandler Handler { get; private set; }

		MethodInfo m_processMethod;
		object m_processInstance;
		InPort[] m_inPorts;
		OutPort[] m_outPorts;
		object[] m_parameters;
		bool[] m_outParameters;
		internal object ReturnValue { get; set; }

		Guid? m_serializationId;

		internal object[] Parameters => m_parameters;

		public override string ToString() => Name;

		#region Initialization

		Node() // For static-method-binding dummies and Node.FromXxx methods
		{ }

		protected Node(INodeHandler handler, string processMethodName, string name = null) // For sub-classes
		{
			InstanceMethod(handler, name, this, processMethodName);
		}

		public static Node FromInstanceMethod(INodeHandler handler, object processInstance, string processMethodName, string name = null)
		{
			if (processInstance is Node)
				throw new ArgumentException($"Unnecessary to create a node for an existing instance of a Node class '{processInstance.GetType().FullName}'", nameof(processInstance));

			var node = new Node();
			node.InstanceMethod(handler, name, processInstance, processMethodName);

			return node;
		}

		void InstanceMethod(INodeHandler handler, string name, object processInstance, string processMethodName)
		{
			if (processInstance == null)
				throw new ArgumentNullException(nameof(processInstance));

			if (string.IsNullOrEmpty(processMethodName))
				throw new ArgumentException("Empty process method name", nameof(processMethodName));

			Initialize(name, handler, processInstance.GetType(), processMethodName, BindingFlags.Instance);
			m_processInstance = processInstance;
		}

		public static Node FromStaticMethod(INodeHandler handler, Type type, string processMethodName, string name = null)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));

			if (string.IsNullOrEmpty(processMethodName))
				throw new ArgumentException("Empty process method name", nameof(processMethodName));

			var node = new Node();
			node.Initialize(name, handler, type, processMethodName, BindingFlags.Static);

			return node;
		}

		void Initialize(string name, INodeHandler handler, Type type, string processMethodName, BindingFlags bindingFlags)
		{
			// Node name
			Name = name ?? $"{type.Name}.{processMethodName}";

			// Handler
			Handler = handler;

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

			OnLink(selfOut, otherIn);
			Handler?.OnLink(selfOut, otherIn);
		}

		protected virtual void OnLink(OutPort selfOut, InPort otherIn)
		{ }

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

			OnUnlink(selfOut, otherIn);
			Handler?.OnUnlink(selfOut, otherIn);
		}

		protected void OnUnlink(OutPort selfOut, InPort otherIn)
		{ }

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

			OnOutValuesChanged(this);
			Handler?.OnOutValuesChanged(this);
		}

		protected virtual void OnOutValuesChanged(Node node)
		{ }

		internal bool CheckInPortsConnected(bool throwException)
		{
			// Check all in ports connected
			if (m_inPorts.Length > 0)
			{
				var unlinked = m_inPorts.FirstOrDefault(p => p.EndPort == null);
				if (unlinked != null)
				{
					if (throwException)
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

			OnOutValuesChanged(this);
			Handler?.OnOutValuesChanged(this);
		}

		#endregion

		#region Serialization

		Guid GetSerializationId()
		{
			if (m_serializationId == null)
				m_serializationId = Guid.NewGuid();

			return m_serializationId.Value;
		}

		protected virtual void OnSave(Utf8JsonWriter writer)
		{ }

		internal void Save(Utf8JsonWriter writer)
		{
			writer.WriteStartObject();

			// Basic
			writer.WriteString("Name", Name);
			writer.WriteString("Id", GetSerializationId());
			writer.WriteString("MethodContainer", m_processMethod.DeclaringType.AssemblyQualifiedName);
			writer.WriteString("MethodName", m_processMethod.Name);
			writer.WriteBoolean("StaticMethod", m_processInstance == null);

			var handlerType = Handler != null ?
				Handler.GetType().AssemblyQualifiedName :
				"";
			writer.WriteString("Handler", handlerType);

			// Out ports
			if (m_outPorts.Length > 0)
			{
				writer.WriteStartArray("OutPorts");

				foreach (var outPort in m_outPorts)
					SaveOutPort(writer, outPort);

				writer.WriteEndArray();
			}

			// Custom data block (call back for 'this' instance only)
			if (m_processInstance == this)
			{
				writer.WriteStartObject("CustomData");
				OnSave(writer);
				writer.WriteEndObject();
			}

			// Handler block
			if (Handler != null)
			{
				writer.WriteStartObject("HandlerData");
				Handler.OnSave(this, writer);
				writer.WriteEndObject();
			}

			writer.WriteEndObject();
		}

		static void SaveOutPort(Utf8JsonWriter writer, OutPort outPort)
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
					writer.WriteStartObject();

					writer.WriteString("NodeId", ep.Owner.GetSerializationId());
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
				public Guid NodeId { get; private set; }
				public string InPortName { get; private set; }

				public EndPort(Guid nodeId, string inPortName)
				{
					NodeId = nodeId;
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

			public void AddEndPort(Guid nodeId, string inPortName)
			{
				m_endPorts.Add(new EndPort(nodeId, inPortName));
			}
		}

		internal static (Node, IReadOnlyList<OutPortLinks>) Load(JsonElement element, ILoader loader)
		{
			// Basic
			var nodeName = element.GetProperty("Name").GetString();
			var nodeId = element.GetProperty("Id").GetGuid();

			var methodContainerTypeName = element.GetProperty("MethodContainer").GetString();
			var methodContainerType = loader.FindType(methodContainerTypeName);

			var methodName = element.GetProperty("MethodName").GetString();
			var isStaticMethod = element.GetProperty("StaticMethod").GetBoolean();

			var handlerTypeName = element.GetProperty("Handler").GetString();

			// Out ports
			List<OutPortLinks> nodeLinks = null;
			{
				var bindingFlags = isStaticMethod ? BindingFlags.Static : BindingFlags.Instance;
				var methodInfo = methodContainerType.GetMethod(methodName, bindingFlags | BindingFlags.Public | BindingFlags.NonPublic);
				if (methodInfo == null)
					throw new Exception($"The process method '{methodName}' is not found in '{methodContainerType}'");

				JsonElement outPorts;
				if (element.TryGetProperty("OutPorts", out outPorts))
				{
					var outPortsCount = outPorts.GetArrayLength();
					if (outPortsCount > 0)
					{
						nodeLinks = new List<OutPortLinks>(outPortsCount);

						foreach (var op in outPorts.EnumerateArray())
							LoadOutPort(op, loader, methodInfo, nodeLinks);
					}
				}
			}

			// Create handler
			INodeHandler handler;
			if (string.IsNullOrEmpty(handlerTypeName))
				handler = null;
			else
			{
				var handlerType = loader.FindType(handlerTypeName);
				handler = loader.CreateHandler(handlerType);

				if (handler == null)
					throw new Exception($"Failed to create a node handler of type '{handlerType}'");
			}

			// Create node
			var node = handler as Node;
			if (node != null)
			{
				var customData = element.GetProperty("CustomData");
				node.OnLoad(customData);
			}
			else
			{
				if (isStaticMethod)
				{
					// Create a dummy node
					node = FromStaticMethod(handler, methodContainerType, methodName, nodeName);
				}
				else
				{
					// Create method container
					object methodContainer;
					if (methodContainerTypeName == handlerTypeName)
						methodContainer = handler;
					else
					{
						methodContainer = loader.CreateMethodContainer(methodContainerType, handler);
						if (methodContainer == null)
							throw new Exception($"Failed to create a method container of type '{methodContainerType}'");
					}

					// Load node
					node = methodContainer as Node;
					if (node != null)
					{
						// Custom data block (call back on the instance of Node and its sub-classes only)
						var customData = element.GetProperty("CustomData");
						node.OnLoad(customData);
					}
					else
					{
						// Create a dummy node
						node = FromInstanceMethod(handler, methodContainer, methodName, nodeName);
					}
				}
			}

			// Load handler
			if (handler != null)
			{
				var handlerData = element.GetProperty("HandlerData");
				handler.OnLoad(node, handlerData);
			}

			node.m_serializationId = nodeId;

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
						var nodeId = ep.GetProperty("NodeId").GetGuid();
						var inPortName = ep.GetProperty("InPort").GetString();

						opLinks.AddEndPort(nodeId, inPortName);
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
					var node = nodes.FirstOrDefault(n => n.GetSerializationId() == ep.NodeId);
					if (node == null)
						throw new Exception($"No node with ID '{ep.NodeId}' found for loading links");

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
