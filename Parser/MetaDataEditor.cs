using System;
using System.Text;
using System.Windows.Forms;
using ForzaTools.Bundles;
using ForzaTools.Bundles.Metadata;

namespace ModelbinParser
{
    public static class MetadataEditor
    {
        public static void EditMetadata(TreeNode node, byte[] fileData)
        {
            if (node.Tag is BundleMetadata md) // ✅ FIXED: Ensure `BundleMetadata` is used
            {
                string currentValue = Encoding.UTF8.GetString(md.GetContents());
                string newValue = Prompt.ShowDialog("Edit Metadata", "Modify value:", currentValue);

                if (!string.IsNullOrEmpty(newValue))
                {
                    byte[] newBytes = Encoding.ASCII.GetBytes(newValue);
                    byte[] fixedBytes = new byte[md.Size];
                    Array.Copy(newBytes, fixedBytes, Math.Min(newBytes.Length, md.Size));

                    node.Text = md.ToString();
                }
            }
        }
    }

    // ✅ ADDED: Fix for missing `Prompt` class
    public static class Prompt
    {
        public static string ShowDialog(string title, string promptText, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Text = promptText, AutoSize = true };
            TextBox textBox = new TextBox() { Left = 20, Top = 50, Width = 340, Text = defaultValue };
            Button confirmation = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };

            confirmation.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
