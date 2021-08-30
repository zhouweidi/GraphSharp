
namespace GraphSharp.Editor
{
	partial class GraphView
	{
		/// <summary> 
		/// 必需的设计器变量。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 清理所有正在使用的资源。
		/// </summary>
		/// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region 组件设计器生成的代码

		/// <summary> 
		/// 设计器支持所需的方法 - 不要修改
		/// 使用代码编辑器修改此方法的内容。
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.ToolStripMenuItem evaluateToolStripMenuItem;
			System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
			this.m_contextMenuDeleteNode = new System.Windows.Forms.ToolStripMenuItem();
			this.m_contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.m_contextMenuCreateNode = new System.Windows.Forms.ToolStripMenuItem();
			evaluateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.m_contextMenu.SuspendLayout();
			this.SuspendLayout();
			// 
			// evaluateToolStripMenuItem
			// 
			evaluateToolStripMenuItem.Name = "evaluateToolStripMenuItem";
			evaluateToolStripMenuItem.Size = new System.Drawing.Size(171, 24);
			evaluateToolStripMenuItem.Text = "Evaluate";
			evaluateToolStripMenuItem.Click += new System.EventHandler(this.OnEvaluate);
			// 
			// toolStripSeparator1
			// 
			toolStripSeparator1.Name = "toolStripSeparator1";
			toolStripSeparator1.Size = new System.Drawing.Size(168, 6);
			// 
			// m_contextMenuDeleteNode
			// 
			this.m_contextMenuDeleteNode.Name = "m_contextMenuDeleteNode";
			this.m_contextMenuDeleteNode.Size = new System.Drawing.Size(171, 24);
			this.m_contextMenuDeleteNode.Text = "Delete Node";
			this.m_contextMenuDeleteNode.Click += new System.EventHandler(this.OnDeleteNode);
			// 
			// m_contextMenu
			// 
			this.m_contextMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.m_contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.m_contextMenuCreateNode,
            this.m_contextMenuDeleteNode,
            toolStripSeparator1,
            evaluateToolStripMenuItem});
			this.m_contextMenu.Name = "contextMenuStrip1";
			this.m_contextMenu.Size = new System.Drawing.Size(172, 82);
			// 
			// m_contextMenuCreateNode
			// 
			this.m_contextMenuCreateNode.Name = "m_contextMenuCreateNode";
			this.m_contextMenuCreateNode.Size = new System.Drawing.Size(171, 24);
			this.m_contextMenuCreateNode.Text = "Create Node";
			// 
			// GraphView
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.DoubleBuffered = true;
			this.Name = "GraphView";
			this.Load += new System.EventHandler(this.OnLoad);
			this.Paint += new System.Windows.Forms.PaintEventHandler(this.OnPaint);
			this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.OnMouseDown);
			this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnMouseMove);
			this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.OnMouseUp);
			this.m_contextMenu.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ContextMenuStrip m_contextMenu;
		private System.Windows.Forms.ToolStripMenuItem m_contextMenuCreateNode;
		private System.Windows.Forms.ToolStripMenuItem m_contextMenuDeleteNode;
	}
}
