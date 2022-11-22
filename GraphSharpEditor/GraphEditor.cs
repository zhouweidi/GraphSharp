using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GraphSharp.Editor
{
	public partial class GraphEditor : Form
	{
		public GraphView View => m_view;
		string m_title;
		string m_fileName;

		string FileName
		{
			set
			{
				var fileNameInTitle = string.IsNullOrEmpty(value) ? "<new graph>" : value;
				Text = $"{m_title} - {fileNameInTitle}";

				m_fileName = value;
			}

			get => m_fileName;
		}

		public GraphEditor()
		{
			InitializeComponent();
		}

		void OnLoad(object sender, EventArgs e)
		{
			m_title = Text;
			m_view.Selection.OnChanged += OnSelectionChanged;

			FileName = null;
		}

		void OnSelectionChanged(Selection selection)
		{
			if (selection.Count == 1)
				m_properties.SelectedObject = selection.First().Node;
			else
				m_properties.SelectedObject = null;
		}

		void OnPropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			m_view.Evaluate();
		}

		const string FileDialogFilter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

		void SaveFile()
		{
			if (string.IsNullOrEmpty(FileName))
			{
				using var dialog = new SaveFileDialog()
				{
					Filter = FileDialogFilter,
					RestoreDirectory = true,
					AddExtension = true,
				};

				if (dialog.ShowDialog() != DialogResult.OK)
					return;

				FileName = dialog.FileName;
			}

			using var stream = new FileStream(FileName, FileMode.Create);
			m_view.SaveGraph(stream);
		}

		void LoadFile()
		{
			using var dialog = new OpenFileDialog()
			{
				Filter = FileDialogFilter,
				RestoreDirectory = true,
				AddExtension = true,
			};

			if (dialog.ShowDialog() != DialogResult.OK)
				return;

			using var stream = dialog.OpenFile();
			m_view.LoadGraph(stream);

			FileName = dialog.FileName;
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.N:
					if (e.Control)
					{
						var result = MessageBox.Show("Save the current graph before creating a new one?", "Save", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
						switch (result)
						{
							case DialogResult.Yes:
								SaveFile();
								break;

							case DialogResult.No:
								break;

							case DialogResult.Cancel:
							default:
								return;
						}

						m_view.Reset();
						FileName = null;
					}
					break;

				case Keys.S:
					if (e.Control)
						SaveFile();
					break;

				case Keys.O:
					if (e.Control)
						LoadFile();
					break;

				case Keys.Delete:
					m_view.RemoveSelectedNodes();
					break;

				case Keys.F5:
					m_view.Evaluate();
					break;

				case Keys.A:
					if (e.Control)
						m_view.SelectAllNodes();
					break;
			}
		}
	}
}
