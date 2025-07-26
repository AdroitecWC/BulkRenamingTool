using System;
using System.Drawing;
using System.Windows.Forms;

namespace RenaimingToolCS.Helpers
{
    public class ProgressInfoForm : Form
    {
        private Label lblMessage;
        private CustomProgressBar progressBar;

        public ProgressInfoForm(string message)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 400;
            Height = 120;
            BackColor = Color.Black;
            ShowInTaskbar = true;
            TopMost = true;

            Resize += ProgressInfoForm_Resize;

            lblMessage = new Label
            {
                Text = message,
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.Black
            };

            progressBar = new CustomProgressBar
            {
                Width = 350,
                Height = 30,
                Left = (Width - 350) / 2,
                Top = (Height / 2) + 10, // Adjust +10 to shift further down if needed
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            lblMessage = new Label
            {
                Text = message,
                Width = Width,
                Height = 50,
                Top = 10,
                Left = 0,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.Black
            };

            Controls.Add(lblMessage);
            Controls.Add(progressBar);


            Controls.Add(lblMessage);
            Controls.Add(progressBar);

            lblMessage.MouseDown += Form_MouseDown;
        }

        private void ProgressInfoForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                Hide();
            else if (WindowState == FormWindowState.Normal)
                Show();
        }

        public void UpdateProgress(int percent, string newMessage = null)
        {
            if (!string.IsNullOrEmpty(newMessage))
                lblMessage.Text = newMessage;

            progressBar.Value = Math.Min(100, Math.Max(0, percent));
            progressBar.Invalidate(); // Redraw
            Application.DoEvents();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }
    }

    public class CustomProgressBar : ProgressBar
    {
        public CustomProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle rect = ClientRectangle;

            // Draw black background
            using (SolidBrush bgBrush = new SolidBrush(Color.Black))
                g.FillRectangle(bgBrush, rect);

            // Draw green fill
            double percent = (double)(Value - Minimum) / (Maximum - Minimum);
            Rectangle fill = new Rectangle(0, 0, (int)(rect.Width * percent), rect.Height);
            using (SolidBrush barBrush = new SolidBrush(Color.LimeGreen))
                g.FillRectangle(barBrush, fill);

            // Optional border
            ControlPaint.DrawBorder(g, rect, Color.DarkGreen, ButtonBorderStyle.Solid);
        }
    }
}