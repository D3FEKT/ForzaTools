namespace ForzaTools.ModelBinEditor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        // In MainForm.Designer.cs or InitializeComponent() inside MainForm.cs

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripTextBox searchTextBox;
        private System.Windows.Forms.ToolStripButton searchButton;
        private System.Windows.Forms.ToolStripButton groupByTagButton;
        private System.Windows.Forms.TabControl rightTabControl;
        private System.Windows.Forms.TabPage propertyPage;
        private System.Windows.Forms.TabPage hexPage;
        // Define the HexControl variable (Reuse the control from HexEditorForm.cs)
        private ForzaTools.ModelBinEditor.HexViewControl embeddedHexView;

        private void InitializeEnhancedUI()
        {
            // 1. ToolStrip Setup
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.searchTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.searchButton = new System.Windows.Forms.ToolStripButton();
            this.groupByTagButton = new System.Windows.Forms.ToolStripButton();

            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
        new System.Windows.Forms.ToolStripLabel("Search:"),
        this.searchTextBox,
        this.searchButton,
        new System.Windows.Forms.ToolStripSeparator(),
        this.groupByTagButton
    });

            this.searchTextBox.Size = new System.Drawing.Size(150, 23);
            this.searchButton.Text = "Find";
            this.searchButton.Click += (s, e) => PopulateTree(); // Reload tree with filter
            this.searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PopulateTree(); };

            this.groupByTagButton.Text = "Group by Type";
            this.groupByTagButton.CheckOnClick = true;
            this.groupByTagButton.Checked = true; // Default to grouped
            this.groupByTagButton.Click += (s, e) => PopulateTree();

            // Add ToolStrip to Panel1 (Top)
            this.splitContainer.Panel1.Controls.Add(this.toolStrip);
            this.treeView.Top = 25; // Push tree down
            this.treeView.Height -= 25;

            // 2. TabControl Setup (Right Side)
            this.rightTabControl = new System.Windows.Forms.TabControl();
            this.propertyPage = new System.Windows.Forms.TabPage("Properties");
            this.hexPage = new System.Windows.Forms.TabPage("Hex View");

            this.rightTabControl.Dock = DockStyle.Fill;
            this.rightTabControl.Controls.Add(this.propertyPage);
            this.rightTabControl.Controls.Add(this.hexPage);

            // Move PropertyGrid into Tab 1
            this.propertyGrid.Parent = this.propertyPage;
            this.propertyGrid.Dock = DockStyle.Fill;

            // Add HexView into Tab 2 (Using the control class from your HexEditorForm.cs)
            this.embeddedHexView = new HexViewControl();
            this.embeddedHexView.Dock = DockStyle.Fill;
            this.hexPage.Controls.Add(this.embeddedHexView);

            this.splitContainer.Panel2.Controls.Clear();
            this.splitContainer.Panel2.Controls.Add(this.rightTabControl);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.openMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.treeView = new System.Windows.Forms.TreeView();
            this.treeContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.viewHexMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();

            // Bottom Panel Controls
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.convertButton = new System.Windows.Forms.Button();
            this.targetVersionComboBox = new System.Windows.Forms.ComboBox();
            this.convertLabel = new System.Windows.Forms.Label();

            this.menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.treeContextMenu.SuspendLayout();
            this.bottomPanel.SuspendLayout();
            this.SuspendLayout();

            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenu}); // Add Label
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1000, 24);
            this.menuStrip.TabIndex = 0;
            this.menuStrip.Text = "menuStrip1";

            // 
            // fileMenu
            // 
            this.fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openMenuItem,
            this.saveMenuItem,
            this.toolStripSeparator1,
            this.exitMenuItem});
            this.fileMenu.Name = "fileMenu";
            this.fileMenu.Size = new System.Drawing.Size(37, 20);
            this.fileMenu.Text = "File";

            // 
            // openMenuItem
            // 
            this.openMenuItem.Name = "openMenuItem";
            this.openMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openMenuItem.Size = new System.Drawing.Size(163, 22);
            this.openMenuItem.Text = "Open...";
            this.openMenuItem.Click += new System.EventHandler(this.OpenFile_Click);

            // 
            // saveMenuItem
            // 
            this.saveMenuItem.Name = "saveMenuItem";
            this.saveMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveMenuItem.Size = new System.Drawing.Size(163, 22);
            this.saveMenuItem.Text = "Save As...";
            this.saveMenuItem.Click += new System.EventHandler(this.SaveFile_Click);

            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(160, 6);

            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
            this.exitMenuItem.Size = new System.Drawing.Size(163, 22);
            this.exitMenuItem.Text = "Exit";
            this.exitMenuItem.Click += new System.EventHandler(this.Exit_Click);

            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 24);
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.treeView);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.propertyGrid);
            this.splitContainer.Size = new System.Drawing.Size(1000, 631); // Reduced height for bottom panel
            this.splitContainer.SplitterDistance = 300;
            this.splitContainer.TabIndex = 1;

            // 
            // treeView
            // 
            this.treeView.ContextMenuStrip = this.treeContextMenu;
            this.treeView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView.HideSelection = false;
            this.treeView.Location = new System.Drawing.Point(0, 0);
            this.treeView.Name = "treeView";
            this.treeView.Size = new System.Drawing.Size(300, 631);
            this.treeView.TabIndex = 0;
            this.treeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.TreeView_AfterSelect);
            this.treeView.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.TreeView_NodeMouseClick);

            // 
            // treeContextMenu
            // 
            this.treeContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewHexMenuItem});
            this.treeContextMenu.Name = "treeContextMenu";
            this.treeContextMenu.Size = new System.Drawing.Size(171, 26);

            // 
            // viewHexMenuItem
            // 
            this.viewHexMenuItem.Name = "viewHexMenuItem";
            this.viewHexMenuItem.Size = new System.Drawing.Size(170, 22);
            this.viewHexMenuItem.Text = "View in Hex Editor";
            this.viewHexMenuItem.Click += new System.EventHandler(this.ViewHex_Click);

            // 
            // propertyGrid
            // 
            this.propertyGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.Size = new System.Drawing.Size(696, 631);
            this.propertyGrid.TabIndex = 0;
            this.propertyGrid.ToolbarVisible = false;

            // 
            // bottomPanel
            // 
            this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.bottomPanel.Height = 45;
            this.bottomPanel.BackColor = System.Drawing.SystemColors.Control;
            this.bottomPanel.Controls.Add(this.convertButton);
            this.bottomPanel.Controls.Add(this.targetVersionComboBox);
            this.bottomPanel.Controls.Add(this.convertLabel);
            this.bottomPanel.Padding = new System.Windows.Forms.Padding(10);

            // 
            // convertLabel
            // 
            this.convertLabel.AutoSize = true;
            this.convertLabel.Location = new System.Drawing.Point(12, 15);
            this.convertLabel.Name = "convertLabel";
            this.convertLabel.Size = new System.Drawing.Size(67, 15);
            this.convertLabel.Text = "Convert To:";

            // 
            // targetVersionComboBox
            // 
            this.targetVersionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.targetVersionComboBox.FormattingEnabled = true;
            this.targetVersionComboBox.Location = new System.Drawing.Point(85, 12);
            this.targetVersionComboBox.Name = "targetVersionComboBox";
            this.targetVersionComboBox.Size = new System.Drawing.Size(250, 23);

            // 
            // convertButton
            // 
            this.convertButton.Location = new System.Drawing.Point(350, 11);
            this.convertButton.Name = "convertButton";
            this.convertButton.Size = new System.Drawing.Size(120, 25);
            this.convertButton.Text = "Convert && Reload";
            this.convertButton.UseVisualStyleBackColor = true;
            this.convertButton.Click += new System.EventHandler(this.ConvertButton_Click);

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 700);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.bottomPanel); // Add
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "MainForm";
            this.Text = "Forza ModelBin Editor";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.treeContextMenu.ResumeLayout(false);
            this.bottomPanel.ResumeLayout(false);
            this.bottomPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileMenu;
        private System.Windows.Forms.ToolStripMenuItem openMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TreeView treeView;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.ContextMenuStrip treeContextMenu;
        private System.Windows.Forms.ToolStripMenuItem viewHexMenuItem;

        // New Controls
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.Label convertLabel;
        private System.Windows.Forms.ComboBox targetVersionComboBox;
        private System.Windows.Forms.Button convertButton;
    }
}