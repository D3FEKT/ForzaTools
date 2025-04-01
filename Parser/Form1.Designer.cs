using System;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace ModelbinParser
{
    public partial class Form1
    {
        private TreeView treeView;
        private ListView vectorListView;
        private MenuStrip menuStrip;
        private ToolStripMenuItem fileMenu;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem saveMenuItem;
        private Button replaceScaleButton;
        private Button replacePositionButton;
        private Button replaceModelButton;

        private void InitializeComponent()
        {
            menuStrip = new MenuStrip();
            fileMenu = new ToolStripMenuItem();
            openMenuItem = new ToolStripMenuItem();
            saveMenuItem = new ToolStripMenuItem();
            treeView = new TreeView();
            vectorListView = new ListView();
            replaceScaleButton = new Button();
            replacePositionButton = new Button();
            replaceModelButton = new Button();
            menuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(28, 28);
            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Size = new Size(1223, 38);
            menuStrip.TabIndex = 0;
            // 
            // fileMenu
            // 
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { openMenuItem, saveMenuItem });
            fileMenu.Name = "fileMenu";
            fileMenu.Size = new Size(62, 34);
            fileMenu.Text = "File";
            // 
            // openMenuItem
            // 
            openMenuItem.Name = "openMenuItem";
            openMenuItem.Size = new Size(164, 34);
            openMenuItem.Text = "Open";
            openMenuItem.Click += OpenMenuItem_Click;
            // 
            // saveMenuItem
            // 
            saveMenuItem.Name = "saveMenuItem";
            saveMenuItem.Size = new Size(164, 34);
            saveMenuItem.Text = "Save";
            saveMenuItem.Click += SaveMenuItem_Click;
            // 
            // treeView
            // 
            treeView.Dock = DockStyle.Left;
            treeView.Location = new Point(0, 38);
            treeView.Name = "treeView";
            treeView.Size = new Size(575, 465);
            treeView.TabIndex = 1;
            // 
            // vectorListView
            // 
            vectorListView.Dock = DockStyle.Fill;
            vectorListView.FullRowSelect = true;
            vectorListView.GridLines = true;
            vectorListView.Location = new Point(575, 38);
            vectorListView.Name = "vectorListView";
            vectorListView.Size = new Size(648, 465);
            vectorListView.TabIndex = 2;
            vectorListView.UseCompatibleStateImageBehavior = false;
            vectorListView.View = View.Details;
            // Add columns to the ListView
            vectorListView.Columns.Add("Mesh Name", 200);
            vectorListView.Columns.Add("Vertex Scale", 200);
            vectorListView.Columns.Add("Vertex Position", 200);
            // 
            // replaceScaleButton
            // 
            replaceScaleButton.Dock = DockStyle.Bottom;
            replaceScaleButton.Location = new Point(0, 503);
            replaceScaleButton.Name = "replaceScaleButton";
            replaceScaleButton.Size = new Size(1223, 23);
            replaceScaleButton.TabIndex = 3;
            replaceScaleButton.Text = "Replace All VertexScale";
            // 
            // replacePositionButton
            // 
            replacePositionButton.Dock = DockStyle.Bottom;
            replacePositionButton.Location = new Point(0, 526);
            replacePositionButton.Name = "replacePositionButton";
            replacePositionButton.Size = new Size(1223, 23);
            replacePositionButton.TabIndex = 4;
            replacePositionButton.Text = "Replace All VertexPosition";
            // 
            // replaceModelButton
            // 
            replaceModelButton.Dock = DockStyle.Bottom;
            replaceModelButton.Location = new Point(0, 549);
            replaceModelButton.Name = "replaceModelButton";
            replaceModelButton.Size = new Size(1223, 23);
            replaceModelButton.TabIndex = 5;
            replaceModelButton.Text = "Replace Model";
            // 
            // Form1
            // 
            ClientSize = new Size(1223, 572);
            Controls.Add(vectorListView);
            Controls.Add(treeView);
            Controls.Add(menuStrip);
            Controls.Add(replaceScaleButton);
            Controls.Add(replacePositionButton);
            Controls.Add(replaceModelButton);
            MainMenuStrip = menuStrip;
            Name = "Form1";
            Text = "Forza Modelbin Parser";
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
