using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace GraphSharp.Editor
{
	public partial class GraphView : UserControl
	{
		Graph m_graph = new Graph();
		List<NodeWidget> m_nodeWidgets = new List<NodeWidget>();

		public Selection Selection { get; private set; } = new Selection();
		SelectionRubberBand m_selectionRubberBand;
		Point m_lastRightClickLocation;
		Point? m_movingStartPoint;
		Dictionary<NodeWidget, Point> m_selectionStartPoints;
		LinkingOperation m_linking;

		public IReadOnlyList<NodeWidget> NodeWidgets => m_nodeWidgets;

		#region Editable properties

		// Node
		[Category("Graph View")]
		public bool AutoEvaluation { get; set; }
		[Category("Graph View")]
		public Size NodeMinSize { get; set; } = new Size(100, 100);
		[Category("Graph View")]
		public Color NodeBorderColor { get; set; } = Color.Black;
		[Category("Graph View")]
		public Color NodeSelectionColor { get; set; } = Color.Yellow;

		// Title
		[Category("Graph View")]
		public Font NodeTitleFont { get; set; }
		[Category("Graph View")]
		public Color NodeTitleColor { get; set; } = Color.DarkRed;
		[Category("Graph View")]
		public Color NodeTitleBackColor { get; set; } = Color.Cyan;

		// Ports
		[Category("Graph View")]
		public int SpacingBetweenInOutPorts { get; set; } = 20;
		[Category("Graph View")]
		public Color PortNameColor { get; set; } = Color.Blue;
		[Category("Graph View")]
		public Color PortBackColor { get; set; } = Color.LightCyan;
		[Category("Graph View")]
		public int PortMargin { get; set; } = 3;
		[Category("Graph View")]
		public int PortPadding { get; set; } = 3;

		// Port pins
		[Category("Graph View")]
		public int PortPinSize { get; set; } = 10;
		[Category("Graph View")]
		public int PortPinMargin { get; set; } = 3;
		[Category("Graph View")]
		public int PortPinPadding { get; set; } = 3;
		[Category("Graph View")]
		public Color PortPinColor { get; set; } = Color.Red;

		// Visual image
		[Category("Graph View")]
		public int NodeVisualSize { get; set; } = 128;
		[Category("Graph View")]
		public Color NodeVisualBackColor { get; set; } = Color.DarkGray;

		// Links
		[Category("Graph View")]
		public Color LinkColor { get; set; } = Color.Purple;
		[Category("Graph View")]
		public Color LinkSelectionColor { get; set; } = Color.Yellow;

		// Selection
		[Category("Graph View")]
		public Color SelectionRubberBandColor { get; set; } = Color.Blue;
		[Category("Graph View")]
		public int SelectionRubberBandBackAlpha { get; set; } = 127;

		#endregion

		#region Draw kit

		internal Pen NodeBorderPen { get; private set; }
		internal Brush NodeBorderBrush { get; private set; }
		internal Pen NodeSelectionPen { get; private set; }

		internal Brush NodeTitleBrush { get; private set; }
		internal Brush NodeTitleBackBrush { get; private set; }

		internal Brush PortNameBrush { get; private set; }
		internal Brush PortBackBrush { get; private set; }
		internal Brush PortPinBrush { get; private set; }
		internal Pen PortPinPen { get; private set; }

		internal Brush NodeVisualBackBrush { get; private set; }

		internal Pen LinkPen { get; private set; }
		internal Pen LinkPendingPen { get; private set; }
		internal Pen LinkSelectionPen { get; private set; }

		Pen m_selectionRubberBandPen;
		Brush m_selectionRubberBandBrush;

		#endregion

		#region Initialization

		public GraphView()
		{
			InitializeComponent();
		}

		void OnLoad(object sender, EventArgs e)
		{
			CreatePaintingTools();
		}

		void CreatePaintingTools()
		{
			// Node
			NodeBorderPen = new Pen(NodeBorderColor, 1);
			NodeBorderBrush = new SolidBrush(NodeBorderColor);
			NodeSelectionPen = new Pen(NodeSelectionColor, 3);

			// Title
			if (NodeTitleFont == null)
				NodeTitleFont = Font;

			NodeTitleBrush = new SolidBrush(NodeTitleColor);
			NodeTitleBackBrush = new SolidBrush(NodeTitleBackColor);

			// Ports
			PortNameBrush = new SolidBrush(PortNameColor);
			PortBackBrush = new SolidBrush(PortBackColor);
			PortPinBrush = new SolidBrush(PortPinColor);
			PortPinPen = new Pen(PortPinColor, 3);

			// Visual image
			NodeVisualBackBrush = new SolidBrush(NodeVisualBackColor);

			// Links
			LinkPen = new Pen(LinkColor, 2);
			LinkPendingPen = new Pen(LinkColor, 2) { DashStyle = DashStyle.Dot, DashPattern = new float[] { 3, 3 } };
			LinkSelectionPen = new Pen(LinkSelectionColor, 3);

			// Selection
			m_selectionRubberBandPen = new Pen(SelectionRubberBandColor, 1);
			m_selectionRubberBandBrush = new SolidBrush(Color.FromArgb(Math.Clamp(SelectionRubberBandBackAlpha, 0, 255), SelectionRubberBandColor));
		}

		public void SetNodeTypes(IEnumerable<Type> nodeTypes)
		{
			if (nodeTypes.Any(t => !t.IsSubclassOf(typeof(Node))))
				throw new Exception("Not a derived class of Node");

			var items = m_contextMenuCreateNode.DropDownItems;

			items.Clear();

			foreach (var nodeType in nodeTypes)
			{
				var name = nodeType.Name.FormatName("Node");
				items.Add(name, null, (sender, e) => OnCreateNode(nodeType));
			}
		}

		#endregion

		#region Mouse input

		bool ControlIsDown => (ModifierKeys & Keys.Control) != 0;

		void OnMouseDown(object sender, MouseEventArgs e)
		{
			switch (e.Button)
			{
				case MouseButtons.Left:
					if (!StartLinking(e.Location))
					{
						if (ControlIsDown || !StartMoving(e.Location))
							m_selectionRubberBand = new SelectionRubberBand(this, e.Location, ControlIsDown);
					}
					break;
			}
		}

		void OnMouseMove(object sender, MouseEventArgs e)
		{
			switch (e.Button)
			{
				case MouseButtons.Left:
					if (m_linking != null)
						Linking(e.Location);
					else
					{
						if (m_movingStartPoint != null)
							Moving(e.Location);
						else if (m_selectionRubberBand != null)
							m_selectionRubberBand.Update(e.Location);
					}

					Invalidate();
					break;
			}
		}

		void OnMouseUp(object sender, MouseEventArgs e)
		{
			switch (e.Button)
			{
				case MouseButtons.Left:
					if (m_linking != null)
						EndLinking(e.Location);
					else
					{
						if (m_movingStartPoint != null)
							EndMoving(e.Location);
						else if (m_selectionRubberBand != null)
						{
							m_selectionRubberBand.Update(e.Location);
							m_selectionRubberBand = null;
						}
					}

					Invalidate();
					break;

				case MouseButtons.Right:
					if (Unlink(e.Location))
						Invalidate();
					else
						ShowContextMenu(e.Location);
					break;
			}
		}

		#endregion

		#region Context menu

		void ShowContextMenu(Point location)
		{
			m_lastRightClickLocation = location;

			if (!SelectionHitTest(location))
			{
				if (!TrySelectNonSelected(location))
					Selection.Clear();

				Invalidate();
			}

			m_contextMenuDeleteNode.Visible = Selection.Count > 0;

			m_contextMenu.Show(this, location);
		}

		void OnCreateNode(Type nodeType)
		{
			var node = (Node)Activator.CreateInstance(nodeType);

			AddNode(node, m_lastRightClickLocation);
		}

		void OnDeleteNode(object sender, EventArgs e)
		{
			if (Selection.Count == 0)
				return;

			foreach (var nodeWidget in Selection)
				RemoveNode(nodeWidget);

			Invalidate();
		}

		void OnEvaluate(object sender, EventArgs e)
		{
			Evaluate();
		}

		#endregion

		#region Operations

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Graph Graph => m_graph;

		public NodeWidget AddNode(Node node, Point location)
		{
			m_graph.AddNode(node);

			var nodeWidget = new NodeWidget(this, node)
			{
				Location = location
			};

			m_nodeWidgets.Add(nodeWidget);

			Invalidate();

			return nodeWidget;
		}

		public void RemoveNode(NodeWidget nodeWidget)
		{
			m_graph.RemoveNode(nodeWidget.Node);
			m_nodeWidgets.Remove(nodeWidget);

			Selection.Remove(nodeWidget);

			Invalidate();
		}

		public void RemoveSelectedNodes()
		{
			if (Selection.Count == 0)
				return;

			foreach (var nodeWidget in Selection.ToArray())
				RemoveNode(nodeWidget);
		}

		public void Reset()
		{
			Reload(new Graph(), null);
		}

		public void SelectAllNodes()
		{
			Selection.Clear();

			foreach (var nodeWidget in m_nodeWidgets)
				Selection.Add(nodeWidget);

			Invalidate();
		}

		public void Evaluate()
		{
			try
			{
				m_graph.Evaluate();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Evaluation", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			Invalidate();
		}

		bool SelectionHitTest(Point location)
		{
			if (Selection.Count == 0)
				return false;

			return (from nodeWidget in Selection
					where nodeWidget.HitTest(location)
					select nodeWidget).Any();
		}

		bool TrySelectNonSelected(Point location)
		{
			var hitNode = (from nodeWidget in m_nodeWidgets
						   where !Selection.Contains(nodeWidget) && nodeWidget.HitTest(location)
						   select nodeWidget).FirstOrDefault();

			if (hitNode != null)
			{
				Selection.Set(hitNode);
				Invalidate();

				return true;
			}
			else
				return false;
		}

		#endregion

		#region Paint

		void OnPaint(object sender, PaintEventArgs e)
		{
			foreach (var nodeWidget in m_nodeWidgets)
				nodeWidget.PrepareDrawing(e.Graphics);

			foreach (var nodeWidget in m_nodeWidgets)
				nodeWidget.DrawLinks(e.Graphics);

			foreach (var nodeWidget in m_nodeWidgets)
				nodeWidget.DrawNode(e.Graphics);

			if (m_selectionRubberBand != null)
				m_selectionRubberBand.Draw(e.Graphics);

			if (m_linking != null)
				m_linking.Draw(e.Graphics);
		}

		#endregion

		#region SelectionRubberBand

		class SelectionRubberBand
		{
			GraphView m_view;
			Point m_startPoint;
			Point m_currentPoint;
			HashSet<NodeWidget> m_originalSelectionForAdditive;

			public SelectionRubberBand(GraphView view, Point startPoint, bool additiveMode)
			{
				m_view = view;
				m_startPoint = startPoint;
				m_currentPoint = startPoint;

				if (additiveMode)
					m_originalSelectionForAdditive = new HashSet<NodeWidget>(view.Selection);
			}

			Rectangle Rect => new Rectangle(
				Math.Min(m_startPoint.X, m_currentPoint.X),
				Math.Min(m_startPoint.Y, m_currentPoint.Y),
				Math.Abs(m_startPoint.X - m_currentPoint.X),
				Math.Abs(m_startPoint.Y - m_currentPoint.Y));

			public void Update(Point location)
			{
				m_currentPoint = location;

				var rect = Rect;
				var selected = from nodeWidget in m_view.m_nodeWidgets
							   where nodeWidget.Intersect(rect)
							   select nodeWidget;

				bool additiveMode = m_originalSelectionForAdditive != null;
				if (additiveMode)
				{
					var finalSelected = new HashSet<NodeWidget>(selected);
					finalSelected.SymmetricExceptWith(m_originalSelectionForAdditive);

					m_view.Selection.Set(finalSelected);
				}
				else
					m_view.Selection.Set(selected);
			}

			public void Draw(Graphics g)
			{
				if (m_startPoint == m_currentPoint)
					return;

				var rect = Rect;

				g.FillRectangle(m_view.m_selectionRubberBandBrush, rect);
				g.DrawRectangle(m_view.m_selectionRubberBandPen, rect);
			}
		}

		#endregion

		#region Moving

		bool StartMoving(Point location)
		{
			var hitNode = SelectionHitTest(location) || TrySelectNonSelected(location);

			if (hitNode)
			{
				m_movingStartPoint = location;

				var startPoints = from nodeWidget in Selection
								  select KeyValuePair.Create(nodeWidget, nodeWidget.Location);

				m_selectionStartPoints = new Dictionary<NodeWidget, Point>(startPoints);

				return true;
			}
			else
			{
				m_movingStartPoint = null;
				m_selectionStartPoints = null;

				return false;
			}
		}

		void Moving(Point location)
		{
			var offset = new Size(
				location.X - m_movingStartPoint.Value.X,
				location.Y - m_movingStartPoint.Value.Y);

			foreach (var s in Selection)
				s.Location = m_selectionStartPoints[s] + offset;
		}

		void EndMoving(Point location)
		{
			Moving(location);

			m_movingStartPoint = null;
			m_selectionStartPoints = null;
		}

		#endregion

		#region Linking

		class LinkingOperation
		{
			GraphView m_view;
			PinInfo m_srcPin;
			Point m_currentLocation;
			bool m_linkable;

			public static LinkingOperation Create(GraphView view, Point location)
			{
				var pin = view.PinHitTest(location, NodeWidget.PinType.Out);
				if (pin != null)
					return new LinkingOperation(view, pin);
				else
					return null;
			}

			LinkingOperation(GraphView view, PinInfo pinInfo)
			{
				m_view = view;
				m_srcPin = pinInfo;
			}

			public void Update(Point location)
			{
				m_currentLocation = location;

				var destPin = m_view.PinHitTest(location, NodeWidget.PinType.In);
				if (destPin != null)
				{
					var outPort = (OutPort)m_srcPin.Port;
					var inPort = (InPort)destPin.Port;

					m_linkable = m_srcPin.Node.IsLinkable(outPort, inPort);
				}
				else
					m_linkable = false;

			}

			public void End(Point location)
			{
				var destPin = m_view.PinHitTest(location, NodeWidget.PinType.In);
				if (destPin == null)
					return;

				try
				{
					var outPort = (OutPort)m_srcPin.Port;
					var inPort = (InPort)destPin.Port;

					m_srcPin.Node.Link(outPort, inPort);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "Invalid link", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}

			public void Draw(Graphics g)
			{
				var pen = m_linkable ? m_view.LinkPen : m_view.LinkPendingPen;

				g.DrawLine(pen, m_srcPin.PinLocation, m_currentLocation);
			}
		}

		class PinInfo
		{
			public Port Port { get; private set; }
			public PointF PinLocation { get; private set; }

			public Node Node => Port.Owner;

			public PinInfo(Port hitPort, PointF pinLocation)
			{
				Port = hitPort;
				PinLocation = pinLocation;
			}
		}

		PinInfo PinHitTest(Point location, NodeWidget.PinType pinType)
		{
			foreach (var nodeWidget in m_nodeWidgets)
			{
				PointF pinLocation;
				var hitPort = nodeWidget.PinHitTest(location, pinType, out pinLocation);
				if (hitPort != null)
					return new PinInfo(hitPort, pinLocation);
			}

			return null;
		}

		bool StartLinking(Point location)
		{
			m_linking = LinkingOperation.Create(this, location);

			return m_linking != null;
		}

		void Linking(Point location)
		{
			m_linking.Update(location);
		}

		void EndLinking(Point location)
		{
			m_linking.End(location);
			m_linking = null;
		}

		bool Unlink(Point location)
		{
			var pin = PinHitTest(location, NodeWidget.PinType.In);
			if (pin == null)
				return false;

			var inPort = (InPort)pin.Port;
			var outPort = inPort.EndPort;

			if (outPort == null)
				return false;

			outPort.Owner.Unlink(outPort, inPort);

			return true;
		}

		#endregion

		#region Load & Save

		public void SaveGraph(Stream stream)
		{
			using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

			writer.WriteStartObject();

			// Graph nodes
			{
				writer.WriteStartArray("Nodes");

				m_graph.Save(writer);

				writer.WriteEndArray();
			}

			// Graph node locations
			{
				writer.WriteStartArray("NodeLocations");

				foreach (var nodeWidget in m_nodeWidgets)
				{
					var locationText = PointToString(nodeWidget.Location);
					writer.WriteStringValue(locationText);
				}

				writer.WriteEndArray();
			}

			writer.WriteEndObject();
		}

		public void LoadGraph(Stream stream, ILoader loader)
		{
			using var document = JsonDocument.Parse(stream);
			var rootElement = document.RootElement;

			// Graph nodes
			var graph = new Graph();
			{
				var nodesElement = rootElement.GetProperty("Nodes");

				if (loader == null)
					loader = DefaultLoader.Instance;

				graph.Load(nodesElement, loader);
			}

			// Graph node locations
			var locations = new List<Point?>();
			{
				var locationsElement = rootElement.GetProperty("NodeLocations");

				foreach (var e in locationsElement.EnumerateArray())
				{
					var locationText = e.GetString();
					var location = StringToPoint(locationText);

					locations.Add(location);
				}
			}

			// Error info
			string locationsErrorInfo = null;
			{
				if (graph.Nodes.Count != locations.Count)
					locationsErrorInfo = $"Nodes count ({graph.Nodes.Count}) doesn't match locations count ({locations.Count})";
				else
				{
					var emptyIndexes = from i in Enumerable.Range(0, locations.Count)
									   where locations[i] == null
									   select i;

					if (emptyIndexes.Any())
					{
						var indexesText = string.Join(", ", emptyIndexes);
						locationsErrorInfo = $"Bad location indexes: {indexesText}";
					}
				}
			}

			if (locationsErrorInfo != null)
			{
				MessageBox.Show(
					locationsErrorInfo,
					"Locations Error",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
			}

			Reload(graph, locations);
		}

		void Reload(Graph graph, IReadOnlyList<Point?> locations)
		{
			var autoEvaluation = AutoEvaluation;

			try
			{
				AutoEvaluation = false;

				m_graph = graph;
				m_nodeWidgets.Clear();
				Selection.Clear();
				m_selectionRubberBand = null;
				m_lastRightClickLocation = Point.Empty;
				m_movingStartPoint = null;
				m_selectionStartPoints = null;
				m_linking = null;

				for (int i = 0; i < m_graph.Nodes.Count; i++)
				{
					var node = m_graph.Nodes[i];
					var nodeWidget = new NodeWidget(this, node);

					if (i < locations.Count)
					{
						var location = locations[i];
						if (location != null)
							nodeWidget.Location = location.Value;
					}

					m_nodeWidgets.Add(nodeWidget);
				}
			}
			finally
			{
				AutoEvaluation = autoEvaluation;
			}

			if (m_graph.Nodes.Count > 0)
				Evaluate();
			else
				Invalidate();
		}

		static string PointToString(Point point) => $"{point.X} {point.Y}";

		static Point? StringToPoint(string text)
		{
			var components = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (components.Length != 2)
				return null;

			int x, y;
			if (!int.TryParse(components[0], out x) || !int.TryParse(components[1], out y))
				return null;

			return new Point(x, y);
		}

		#endregion
	}
}
