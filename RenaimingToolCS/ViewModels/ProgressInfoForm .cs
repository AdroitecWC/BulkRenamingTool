using System;
using System.Drawing;
using System.Windows.Forms;

namespace RenaimingToolCS.Helpers
{
    public class ProgressInfoForm : Form
    {
        private Label lblMessage;
        private ProgressBar progressBar;

        public ProgressInfoForm(string message)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 300;
            Height = 120;
            MinimizeBox = true;
            MaximizeBox = false;
            Text = "Processing";
            TopMost = true;

            lblMessage = new Label
            {
                Text = message,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            Controls.Add(lblMessage);
            Controls.Add(progressBar);
        }

        public void UpdateProgress(int percent, string newMessage = null)
        {
            if (!string.IsNullOrEmpty(newMessage))
            {
                lblMessage.Text = newMessage;
            }
            progressBar.Value = Math.Min(100, Math.Max(0, percent));
            Application.DoEvents(); // Refresh UI
        }
    }
}
