using System;
using System.Windows.Forms;

namespace CoddCtTools
{
    /// <summary>
    /// Dialog to configure minimum and maximum values for tag simulation
    /// </summary>
    public partial class SimulateTagDialog : Form
    {
        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }
        public bool Cancelled { get; private set; } = true;

        private TextBox txtMinValue;
        private TextBox txtMaxValue;
        private Button btnOK;
        private Button btnCancel;
        private Label lblTagName;

        public SimulateTagDialog(string tagName, double? currentMin = null, double? currentMax = null)
            : this($"Simulate Tag: {tagName}", $"Tag: {tagName}", currentMin, currentMax)
        {
        }

        public SimulateTagDialog(string dialogTitle, string description, double? currentMin = null, double? currentMax = null)
        {
            InitializeComponent(dialogTitle, description, currentMin, currentMax);
        }

        private void InitializeComponent(string dialogTitle, string description, double? currentMin, double? currentMax)
        {
            this.Text = dialogTitle;
            this.Size = new System.Drawing.Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblTagName = new Label
            {
                Text = description,
                Location = new System.Drawing.Point(10, 15),
                Size = new System.Drawing.Size(360, 23),
                Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold)
            };

            var lblMinValue = new Label
            {
                Text = "Minimum Value:",
                Location = new System.Drawing.Point(10, 50),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtMinValue = new TextBox
            {
                Location = new System.Drawing.Point(120, 47),
                Size = new System.Drawing.Size(250, 23),
                Text = currentMin?.ToString() ?? "0"
            };

            var lblMaxValue = new Label
            {
                Text = "Maximum Value:",
                Location = new System.Drawing.Point(10, 85),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            txtMaxValue = new TextBox
            {
                Location = new System.Drawing.Point(120, 82),
                Size = new System.Drawing.Size(250, 23),
                Text = currentMax?.ToString() ?? "100"
            };

            btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(195, 120),
                Size = new System.Drawing.Size(75, 30)
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(280, 120),
                Size = new System.Drawing.Size(90, 30)
            };
            btnCancel.Click += (s, e) => { Cancelled = true; };

            this.Controls.Add(lblTagName);
            this.Controls.Add(lblMinValue);
            this.Controls.Add(txtMinValue);
            this.Controls.Add(lblMaxValue);
            this.Controls.Add(txtMaxValue);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (!double.TryParse(txtMinValue.Text, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double min))
            {
                MessageBox.Show("Please enter a valid numeric value for the minimum.", "Invalid Value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMinValue.Focus();
                return;
            }

            if (!double.TryParse(txtMaxValue.Text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double max))
            {
                MessageBox.Show("Please enter a valid numeric value for the maximum.", "Invalid Value",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMaxValue.Focus();
                return;
            }

            if (min >= max)
            {
                MessageBox.Show("The minimum value must be less than the maximum value.", "Invalid Values",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMinValue.Focus();
                return;
            }

            MinValue = min;
            MaxValue = max;
            Cancelled = false;
            this.DialogResult = DialogResult.OK;
        }
    }
}

