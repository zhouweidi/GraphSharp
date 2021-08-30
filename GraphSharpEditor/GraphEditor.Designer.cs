namespace GraphSharp.Editor
{
	partial class GraphEditor
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Windows.Forms.SplitContainer splitContainer1;
			this.m_view = new GraphSharp.Editor.GraphView();
			this.m_properties = new System.Windows.Forms.PropertyGrid();
			splitContainer1 = new System.Windows.Forms.SplitContainer();
			((System.ComponentModel.ISupportInitialize)(splitContainer1)).BeginInit();
			splitContainer1.Panel1.SuspendLayout();
			splitContainer1.Panel2.SuspendLayout();
			splitContainer1.SuspendLayout();
			this.SuspendLayout();
			// 
			// splitContainer1
			// 
			splitContainer1.Cursor = System.Windows.Forms.Cursors.VSplit;
			splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			splitContainer1.Location = new System.Drawing.Point(0, 0);
			splitContainer1.Name = "splitContainer1";
			// 
			// splitContainer1.Panel1
			// 
			splitContainer1.Panel1.Controls.Add(this.m_view);
			// 
			// splitContainer1.Panel2
			// 
			splitContainer1.Panel2.Controls.Add(this.m_properties);
			splitContainer1.Size = new System.Drawing.Size(1387, 773);
			splitContainer1.SplitterDistance = 1000;
			splitContainer1.SplitterWidth = 5;
			splitContainer1.TabIndex = 1;
			// 
			// m_view
			// 
			this.m_view.AutoEvaluation = false;
			this.m_view.BackColor = System.Drawing.Color.Gray;
			this.m_view.Cursor = System.Windows.Forms.Cursors.Default;
			this.m_view.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_view.LinkColor = System.Drawing.Color.Purple;
			this.m_view.LinkSelectionColor = System.Drawing.Color.Yellow;
			this.m_view.Location = new System.Drawing.Point(0, 0);
			this.m_view.Name = "m_view";
			this.m_view.NodeBorderColor = System.Drawing.Color.Black;
			this.m_view.NodeMinSize = new System.Drawing.Size(100, 0);
			this.m_view.NodeSelectionColor = System.Drawing.Color.Yellow;
			this.m_view.NodeTitleBackColor = System.Drawing.Color.Cyan;
			this.m_view.NodeTitleColor = System.Drawing.Color.Black;
			this.m_view.NodeTitleFont = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
			this.m_view.NodeVisualBackColor = System.Drawing.Color.DarkGray;
			this.m_view.NodeVisualSize = 128;
			this.m_view.PortBackColor = System.Drawing.Color.LightCyan;
			this.m_view.PortMargin = 3;
			this.m_view.PortNameColor = System.Drawing.Color.Blue;
			this.m_view.PortPadding = 10;
			this.m_view.PortPinColor = System.Drawing.Color.Red;
			this.m_view.PortPinMargin = 3;
			this.m_view.PortPinPadding = 3;
			this.m_view.PortPinSize = 15;
			this.m_view.SelectionRubberBandBackAlpha = 127;
			this.m_view.SelectionRubberBandColor = System.Drawing.Color.Blue;
			this.m_view.Size = new System.Drawing.Size(1000, 773);
			this.m_view.SpacingBetweenInOutPorts = 20;
			this.m_view.TabIndex = 0;
			// 
			// m_properties
			// 
			this.m_properties.Dock = System.Windows.Forms.DockStyle.Fill;
			this.m_properties.Location = new System.Drawing.Point(0, 0);
			this.m_properties.Name = "m_properties";
			this.m_properties.Size = new System.Drawing.Size(382, 773);
			this.m_properties.TabIndex = 0;
			this.m_properties.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.OnPropertyValueChanged);
			// 
			// GraphEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1387, 773);
			this.Controls.Add(splitContainer1);
			this.KeyPreview = true;
			this.Name = "GraphEditor";
			this.Text = "Graph Editor";
			this.Load += new System.EventHandler(this.OnLoad);
			this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnKeyDown);
			splitContainer1.Panel1.ResumeLayout(false);
			splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(splitContainer1)).EndInit();
			splitContainer1.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.PropertyGrid m_properties;
		private GraphView m_view;
	}
}