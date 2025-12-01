using System;
using System.Windows.Forms;
using System.Drawing;

namespace ForzaTools.ModelBinEditor
{
    public class BatchConversionDialog : Form
    {
        private ComboBox versionComboBox;
        private Button btnOk;
        private Button btnCancel;
        private Label label;

        public string SelectedVersion => versionComboBox.SelectedItem?.ToString();

        public BatchConversionDialog()
        {
            this.Text = "Batch Conversion";
            this.Size = new Size(300, 180);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            label = new Label { Text = "Select Target Forza Version:", Location = new Point(20, 20), AutoSize = true };

            versionComboBox = new ComboBox { Location = new Point(20, 50), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            versionComboBox.Items.Add("Forza Horizon 5");
            // Add more here if future logic supports it
            versionComboBox.SelectedIndex = 0;

            btnOk = new Button { Text = "Start", DialogResult = DialogResult.OK, Location = new Point(100, 90) };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(185, 90) };

            this.Controls.Add(label);
            this.Controls.Add(versionComboBox);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}