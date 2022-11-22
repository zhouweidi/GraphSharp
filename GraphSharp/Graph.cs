using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace GraphSharp
{
	public sealed class Graph
	{
		List<Node> m_nodes = new List<Node>();

		public IReadOnlyList<Node> Nodes => m_nodes;
		public IEnumerable<Node> ResultNodes => from n in m_nodes
												where n.NoneOfOutPortsConnected() && n.CheckInPortsConnected(false)
												select n;

		#region Node operations

		public void AddNode(Node node)
		{
			if (node.Graph != null)
				throw new ArgumentException($"The node '{node}' already belongs to another graph", nameof(node));

			m_nodes.Add(node);
			node.Graph = this;
		}

		public void RemoveNode(Node node)
		{
			if (node.Graph != this)
				throw new ArgumentException($"The node '{node}' does not belong to this graph", nameof(node));

			if (!m_nodes.Remove(node))
				throw new InvalidOperationException($"The node '{node}' has not been added in this graph before");

			node.RemoveLinks();
			node.Graph = null;
		}

		public void Clear()
		{
			if (m_nodes.Count == 0)
				return;

			foreach (var node in m_nodes)
			{
				node.RemoveLinks();
				node.Graph = null;
			}

			m_nodes.Clear();
		}

		#endregion

		#region Process

		class NodeEvaluation
		{
			public Node Node;
			public int InPortIndex;

			public NodeEvaluation(Node node)
			{
				Node = node;
			}
		}

		public void Evaluate()
		{
			var resultNodes = ResultNodes;

			// Check connectivity and circular dependency
			EvaluateHelper(resultNodes, false);

			// Reset all out values
			foreach (var n in m_nodes)
				n.ResetOutValues();

			// Evaluate nodes
			EvaluateHelper(resultNodes, true);
		}

		void EvaluateHelper(IEnumerable<Node> resultNodes, bool evaluateNode)
		{
			var stack = new Stack<NodeEvaluation>(from n in resultNodes
												  select new NodeEvaluation(n));
			var processedNodes = new HashSet<Node>();

			while (stack.Count > 0)
			{
				var frame = stack.Peek();
				var node = frame.Node;

				if (frame.InPortIndex >= node.InPorts.Count)
				{
					if (evaluateNode)
						node.EvaluateHelper();

					stack.Pop();
				}
				else
				{
					if (!evaluateNode && frame.InPortIndex == 0)
						node.CheckInPortsConnected(true);

					while (frame.InPortIndex < node.InPorts.Count)
					{
						var dependencyNode = node.InPorts[frame.InPortIndex].EndPort.Owner;

						if (!evaluateNode && stack.Any(f => f.Node == dependencyNode))
							throw new Exception($"Circular dependency found at the in port '{node.InPorts[frame.InPortIndex]}'");

						frame.InPortIndex++;

						if (processedNodes.Add(dependencyNode))
						{
							stack.Push(new NodeEvaluation(dependencyNode));
							break;
						}
					}
				}
			}
		}

		#endregion

		#region Serialization

		public string SaveString()
		{
			using var stream = new MemoryStream();

			Save(stream);

			return Encoding.UTF8.GetString(stream.ToArray());
		}

		public void Save(string fileName)
		{
			using var stream = new FileStream(fileName, FileMode.Create);

			Save(stream);
		}

		public void Save(Stream stream)
		{
			var options = new JsonWriterOptions
			{
				Indented = true
			};

			using var writer = new Utf8JsonWriter(stream, options);

			Save(writer, true);
		}

		public void Save(Utf8JsonWriter writer) => Save(writer, false);

		void Save(Utf8JsonWriter writer, bool isJsonRoot)
		{
			if (isJsonRoot)
				writer.WriteStartArray();

			foreach (var node in m_nodes)
				node.Save(writer);

			if (isJsonRoot)
				writer.WriteEndArray();

			writer.Flush();
		}

		public void LoadString(string text) => LoadString(text, DefaultLoader.Instance);

		public void LoadString(string text, ILoader loader)
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text), false);

			Load(stream, loader);
		}

		public void Load(string fileName) => Load(fileName, DefaultLoader.Instance);

		public void Load(string fileName, ILoader loader)
		{
			using var stream = new FileStream(fileName, FileMode.Open);

			Load(stream, loader);
		}

		public void Load(Stream stream) => Load(stream, DefaultLoader.Instance);

		public void Load(Stream stream, ILoader loader)
		{
			using var document = JsonDocument.Parse(stream);

			Load(document.RootElement, loader);
		}

		public void Load(JsonElement element) => Load(element, DefaultLoader.Instance);

		public void Load(JsonElement element, ILoader loader)
		{
			Clear();

			// Create nodes
			var nodesCount = element.GetArrayLength();
			if (nodesCount == 0)
				return;

			var nodesLinks = new List<IReadOnlyList<Node.OutPortLinks>>(nodesCount);
			m_nodes.Capacity = nodesCount;

			foreach (var e in element.EnumerateArray())
			{
				var (node, links) = Node.Load(e, loader);

				m_nodes.Add(node);
				nodesLinks.Add(links);
			}

			// Set links
			for (int i = 0; i < m_nodes.Count; i++)
			{
				var links = nodesLinks[i];
				if (links != null)
				{
					var node = m_nodes[i];
					node.LoadLinks(links, m_nodes);
				}
			}
		}

		#endregion
	}
}
