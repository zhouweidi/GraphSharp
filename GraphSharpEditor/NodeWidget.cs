using System;
using System.Drawing;
using System.Text.Json;

namespace GraphSharp.Editor
{
	public partial class NodeWidget : INodeHandler
	{
		public GraphView View { get; private set; }
		public Node Node { get; private set; }
		public string Name { get; private set; }
		public IVisualization Visualization { get; private set; }

		public Point Location { get; internal set; }
		public bool Selected { get; internal set; }
		Drawing m_drawing;
		Bitmap m_visualImage;

		internal void Initialize(GraphView view, Node node)
		{
			View = view;
			Node = node;
			Name = node.GetType().Name.FormatName("Node");

			var visualNode = node as IVisualNode;
			if (visualNode != null)
				Visualization = visualNode.CreateVisualization();

			if (View.AutoEvaluation)
				TryEvalute();
		}

		#region INodeHandler

		public void OnSave(Node node, Utf8JsonWriter writer)
		{
			writer.WriteString("Location", PointToString(Location));
		}

		public void OnLoad(Node node, JsonElement element)
		{
			var locationText = element.GetProperty("Location").GetString();

			Location = StringToPoint(locationText);
		}

		static string PointToString(Point point) => $"{point.X} {point.Y}";

		static Point StringToPoint(string text)
		{
			var components = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (components.Length != 2)
				throw new ArgumentException($"Bad point text: {text}", nameof(text));

			int x, y;
			if (!int.TryParse(components[0], out x) || !int.TryParse(components[1], out y))
				throw new ArgumentException($"Bad point text: {text}", nameof(text));

			return new Point(x, y);
		}

		public void OnLink(OutPort selfOut, InPort otherIn)
		{
			OnLinkChanged(selfOut, otherIn);
		}

		public void OnUnlink(OutPort selfOut, InPort otherIn)
		{
			OnLinkChanged(selfOut, otherIn);
		}

		void OnLinkChanged(OutPort selfOut, InPort otherIn)
		{
			if (View != null && View.AutoEvaluation)
			{
				var nodeWidget = GetNodeWidgetFromNode(otherIn.Owner);
				if (nodeWidget != null)
					nodeWidget.TryEvalute();
			}
		}

		public void OnOutValuesChanged(Node node)
		{
			if (Visualization != null)
				m_visualImage = Visualization.Draw(node, m_visualImage);
		}

		#endregion

		void TryEvalute()
		{
			try
			{
				Node.Evaluate();
			}
			catch
			{
				m_visualImage?.Dispose();
				m_visualImage = null;
			}

			foreach (var port in Node.OutPorts)
			{
				foreach (var ep in port.EndPorts)
				{
					var nodeWidget = GetNodeWidgetFromNode(ep.Owner);
					nodeWidget.TryEvalute();
				}
			}
		}

		public static NodeWidget GetNodeWidgetFromNode(Node node)
		{
			return (NodeWidget)node.Handler;
		}

		#region Draw

		internal void PrepareDrawing(Graphics g)
		{
			if (m_drawing == null)
				m_drawing = new Drawing(this, g);
		}

		public void DrawLinks(Graphics g)
		{
			m_drawing.DrawLinks(g);
		}

		public void DrawNode(Graphics g)
		{
			m_drawing.DrawNode(g);
		}

		#endregion

		#region Helper

		internal bool HitTest(Point location) => m_drawing.Rect.Contains(location);

		internal bool Intersect(Rectangle rect) => m_drawing.Rect.IntersectsWith(rect);

		internal enum PinType
		{
			In, Out, All
		}

		internal Port PinHitTest(Point location, PinType pinType, out PointF pinLocation) => m_drawing.PinHitTest(location, pinType, out pinLocation);

		#endregion
	}
}
