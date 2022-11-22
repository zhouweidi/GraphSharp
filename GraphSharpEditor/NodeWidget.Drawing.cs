using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace GraphSharp.Editor
{
	partial class NodeWidget
	{
		class Drawing
		{
			NodeWidget m_widget;

			// Bounds
			float m_width;
			float m_height;

			// Title
			float m_titleTextHeight;

			// Ports
			float m_maxOutPortItemWidth;
			int m_portLineHeight;
			int m_portsHeight;

			// Port pins
			RectangleF m_pinsBounds;
			PointF[] m_inPinLocations;
			PointF[] m_outPinLocations;

			// Shortcuts
			PointF Location => m_widget.Location;
			GraphView View => m_widget.View;
			Node Node => m_widget.Node;

			public RectangleF Rect => new RectangleF(Location.X, Location.Y, m_width, m_height);

			const int TitleStartingSpace = 5;
			const int SeparatorWidth = 1;

			#region Initialization

			public Drawing(NodeWidget widget, Graphics g)
			{
				m_widget = widget;

				CalculateLayout(g);
			}

			void CalculateLayout(Graphics g)
			{
				m_width = 0;
				m_height = 0;

				// Title
				{
					var textSize = g.MeasureString(m_widget.Name, View.NodeTitleFont);

					m_titleTextHeight = textSize.Height;

					var width = textSize.Width + TitleStartingSpace;
					var height = textSize.Height + SeparatorWidth;

					m_width = Math.Max(width, m_width);
					m_height += height;
				}

				// Ports
				var portsOffsetY = m_height;
				{
					var pinSize = View.PortPinSize;
					var margin = View.PortPinMargin;
					var padding = View.PortPinPadding;
					var pinWidth = margin + pinSize + padding;

					var maxInWidth = Node.InPorts.Count == 0 ?
					  0 :
					  (from p in Node.InPorts
					   let name = p.Name.FormatName()
					   select g.MeasureString(name, View.Font).Width).Max() + pinWidth;

					var maxOutWidth = Node.OutPorts.Count == 0 ?
						0 :
						(from p in Node.OutPorts
						 let name = p.Name.FormatName()
						 select g.MeasureString(name, View.Font).Width).Max() + pinWidth;

					m_maxOutPortItemWidth = maxOutWidth;

					m_portLineHeight = Math.Max(View.Font.Height, pinSize);

					var maxPortsCount = Math.Max(Node.InPorts.Count, Node.OutPorts.Count);
					m_portsHeight =
						View.PortMargin * 2 +
						m_portLineHeight * maxPortsCount +
						View.PortPadding * (maxPortsCount > 0 ? maxPortsCount - 1 : 0);

					var width = maxInWidth + maxOutWidth + View.SpacingBetweenInOutPorts;
					m_width = Math.Max(width, m_width);

					m_height += m_portsHeight;
				}

				// Visual image
				if (m_widget.Visualization != null)
				{
					m_width = Math.Max(View.NodeVisualSize, m_width);
					m_height += SeparatorWidth + View.NodeVisualSize;
				}

				// Min size
				{
					m_width = Math.Max(m_width, View.NodeMinSize.Width);
					m_height = Math.Max(m_height, View.NodeMinSize.Height);
				}

				// Port pins layout
				m_pinsBounds = RectangleF.Empty;

				m_inPinLocations = CalculatePortPinsLayout(g, portsOffsetY, Node.InPorts, true, ref m_pinsBounds);
				m_outPinLocations = CalculatePortPinsLayout(g, portsOffsetY, Node.OutPorts, false, ref m_pinsBounds);
			}

			PointF[] CalculatePortPinsLayout(Graphics g, float offsetY, IReadOnlyList<Port> ports, bool leftAlignment, ref RectangleF bounds)
			{
				var pinLocations = new PointF[ports.Count];

				var pinSize = View.PortPinSize;
				var pinOffsetY = (m_portLineHeight - pinSize) / 2.0f;

				offsetY += View.PortMargin;

				for (int i = 0; i < ports.Count; i++)
				{
					var x = leftAlignment ? View.PortPinMargin : m_width - View.PortPinMargin - pinSize;
					var y = offsetY + pinOffsetY;
					var location = new PointF(x, y);

					pinLocations[i] = location;

					var pinRect = new RectangleF(location.X, location.Y, pinSize, pinSize);
					bounds = RectangleF.Union(bounds, pinRect);

					offsetY += m_portLineHeight + View.PortPadding;
				}

				return pinLocations;
			}

			#endregion

			#region Draw calls

			public void DrawNode(Graphics g)
			{
				Draw(g, DrawNodeEntry);
			}

			public void DrawLinks(Graphics g)
			{
				Draw(g, DrawLinksEntry);
			}

			void Draw(Graphics g, Action<Graphics> drawMethod)
			{
				var container = g.BeginContainer();
				try
				{
					g.TranslateTransform(Location.X, Location.Y);

					drawMethod(g);
				}
				finally
				{
					g.EndContainer(container);
				}
			}

			#endregion

			#region Node drawing

			void DrawNodeEntry(Graphics g)
			{
				float offsetY = 0;

				DrawTitle(g, ref offsetY);
				DrawPorts(g, ref offsetY);
				DrawVisualImage(g, ref offsetY);

				DrawBorder(g);
			}

			void DrawTitle(Graphics g, ref float offsetY)
			{
				// Text
				g.FillRectangle(View.NodeTitleBackBrush, 0, offsetY, m_width, m_titleTextHeight);
				g.DrawString(m_widget.Name, View.NodeTitleFont, View.NodeTitleBrush, TitleStartingSpace, offsetY);
				offsetY += m_titleTextHeight;

				// Separator
				g.FillRectangle(View.NodeBorderBrush, 0, offsetY, m_width, SeparatorWidth);
				offsetY += SeparatorWidth;
			}

			static readonly StringFormat StringFormat_InPort = new StringFormat()
			{
				LineAlignment = StringAlignment.Center
			};

			static readonly StringFormat StringFormat_OutPort = new StringFormat()
			{
				LineAlignment = StringAlignment.Center,
				Alignment = StringAlignment.Far
			};

			void DrawPorts(Graphics g, ref float offsetY)
			{
				g.FillRectangle(View.PortBackBrush, 0, offsetY, m_width, m_portsHeight);

				if (Node.InPorts.Count > 0)
					DrawPortsHelper(g, Node.InPorts, m_inPinLocations, StringFormat_InPort, true);

				if (Node.OutPorts.Count > 0)
					DrawPortsHelper(g, Node.OutPorts, m_outPinLocations, StringFormat_OutPort, false);

				offsetY += m_portsHeight;
			}

			void DrawPortsHelper(Graphics g, IReadOnlyList<Port> ports, IReadOnlyList<PointF> pinLocations, StringFormat stringFormat, bool isInPort)
			{
				var pinSize = View.PortPinSize;
				var pinWidth = View.PortPinMargin + pinSize + View.PortPinPadding;

				for (int i = 0; i < ports.Count; i++)
				{
					var port = ports[i];
					var pinLocation = pinLocations[i];

					// Draw pin
					{
						var linked = isInPort ?
							(port as InPort).EndPort != null :
							(port as OutPort).EndPorts.Count > 0;

						if (linked)
							g.FillRectangle(View.PortPinBrush, pinLocation.X, pinLocation.Y, pinSize, pinSize);
						else
						{
							var widthOffset = View.PortPinPen.Width / 2;

							g.DrawRectangle(
								View.PortPinPen,
								pinLocation.X + widthOffset,
								pinLocation.Y + widthOffset,
								pinSize - widthOffset * 2,
								pinSize - widthOffset * 2);
						}
					}

					// Draw text
					{
						var name = port.Name.FormatName();

						var y = pinLocation.Y + (pinSize - m_portLineHeight) / 2.0f;
						var rect = isInPort ?
							new RectangleF(pinWidth, y, 0, m_portLineHeight) :
							new RectangleF(m_width - m_maxOutPortItemWidth, y, m_maxOutPortItemWidth - pinWidth, m_portLineHeight);

						g.DrawString(name, View.Font, View.PortNameBrush, rect, stringFormat);
					}
				}
			}

			void DrawVisualImage(Graphics g, ref float offsetY)
			{
				// Separator line
				g.FillRectangle(View.NodeBorderBrush, 0, offsetY, m_width, SeparatorWidth);
				offsetY += SeparatorWidth;

				// Image
				var visualRect = new RectangleF(0, offsetY, m_width, View.NodeVisualSize);
				var image = m_widget.m_visualImage;
				if (image == null)
				{
					// Background
					g.FillRectangle(View.NodeVisualBackBrush, visualRect);
				}
				else
				{
					// Get scale
					var scale = visualRect.Width / image.Width;
					if (image.Height * scale > visualRect.Height)
						scale = visualRect.Height / image.Height;

					// Background
					var drawWidth = image.Width * scale;
					var drawHeight = image.Height * scale;

					if (drawWidth < visualRect.Width || drawHeight < visualRect.Height)
						g.FillRectangle(View.NodeVisualBackBrush, visualRect);

					// Image
					var srcRect = new RectangleF(PointF.Empty, image.Size);
					var destRect = new RectangleF(
						visualRect.Width / 2 - drawWidth / 2,
						offsetY + (visualRect.Height - drawHeight) / 2,
						drawWidth,
						drawHeight);

					g.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);
					offsetY += visualRect.Height;
				}
			}

			void DrawBorder(Graphics g)
			{
				g.DrawRectangle(View.NodeBorderPen, 0, 0, m_width, m_height);

				if (m_widget.Selected)
				{
					var pen = View.NodeSelectionPen;
					g.DrawRectangle(pen, -pen.Width / 2, -pen.Width / 2, m_width + pen.Width, m_height + pen.Width);
				}
			}

			#endregion

			#region Links drawing

			void DrawLinksEntry(Graphics g)
			{
				var halfPinSize = View.PortPinSize / 2.0f;

				for (int i = 0; i < Node.OutPorts.Count; i++)
				{
					var port = Node.OutPorts[i];
					if (port.EndPorts.Count > 0)
					{
						var pin = new PointF(
							m_outPinLocations[i].X + halfPinSize,
							m_outPinLocations[i].Y + halfPinSize);

						foreach (var endPort in port.EndPorts)
						{
							NodeWidget otherWidget;
							var targetPin = GetTargetPinLocation(endPort, out otherWidget);

							targetPin.X -= Location.X - halfPinSize;
							targetPin.Y -= Location.Y - halfPinSize;

							var linkPen = (m_widget.Selected || otherWidget.Selected) ? View.LinkSelectionPen : View.LinkPen;
							g.DrawLine(linkPen, pin, targetPin);
						}
					}
				}
			}

			static PointF GetTargetPinLocation(InPort endPort, out NodeWidget nodeWidget)
			{
				var node = endPort.Owner;

				for (int i = 0; i < node.InPorts.Count; i++)
				{
					if (node.InPorts[i] == endPort)
					{
						nodeWidget = GetNodeWidgetFromNode(node);

						var pinLocation = nodeWidget.m_drawing.m_inPinLocations[i];
						pinLocation.X += nodeWidget.Location.X;
						pinLocation.Y += nodeWidget.Location.Y;

						return pinLocation;
					}
				}

				throw new Exception($"No target in port found in the target node '{node}'");
			}

			#endregion

			#region Hit test

			public Port PinHitTest(Point location, PinType type, out PointF pinLocation)
			{
				var localLocation = new PointF(
					location.X - Location.X,
					location.Y - Location.Y);

				if (m_pinsBounds.Contains(localLocation))
				{
					if (type == PinType.In || type == PinType.All)
					{
						var port = PinHitTestHelper(m_inPinLocations, Node.InPorts, localLocation, out pinLocation);
						if (port != null)
							return port;
					}

					if (type == PinType.Out || type == PinType.All)
					{
						var port = PinHitTestHelper(m_outPinLocations, Node.OutPorts, localLocation, out pinLocation);
						if (port != null)
							return port;
					}
				}

				pinLocation = PointF.Empty;
				return null;
			}

			Port PinHitTestHelper(PointF[] pinLocations, IReadOnlyList<Port> ports, PointF location, out PointF pinLocation)
			{
				var pinRect = new RectangleF(0, 0, View.PortPinSize, View.PortPinSize);

				for (int i = 0; i < pinLocations.Length; i++)
				{
					pinRect.Location = pinLocations[i];
					if (pinRect.Contains(location))
					{
						pinLocation = new PointF(
							Location.X + pinRect.X + pinRect.Width / 2,
							Location.Y + pinRect.Y + pinRect.Height / 2);

						return ports[i];
					}
				}

				pinLocation = PointF.Empty;
				return null;
			}

			#endregion
		}
	}
}