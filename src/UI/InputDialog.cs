using System;
using System.Windows.Forms;

namespace StockDataMQClient
{
    /// <summary>
    /// 输入对话框
    /// </summary>
    public partial class InputDialog : Form
    {
        private TextBox textBox;
        private Button btnOK;
        private Button btnCancel;
        private Label label;

        private string _inputText;
        public string InputText
        {
            get { return _inputText; }
            private set { _inputText = value; }
        }

        public InputDialog(string prompt, string title)
        {
            InitializeComponent(prompt, title);
        }

        private void InitializeComponent(string prompt, string title)
        {
            this.Text = title;
            this.Size = new System.Drawing.Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            label = new Label();
            label.Text = prompt;
            label.Location = new System.Drawing.Point(12, 20);
            label.Size = new System.Drawing.Size(360, 20);
            this.Controls.Add(label);

            textBox = new TextBox();
            textBox.Location = new System.Drawing.Point(12, 50);
            textBox.Size = new System.Drawing.Size(360, 20);
            this.Controls.Add(textBox);

            btnOK = new Button();
            btnOK.Text = "确定";
            btnOK.DialogResult = DialogResult.OK;
            btnOK.Location = new System.Drawing.Point(216, 85);
            btnOK.Size = new System.Drawing.Size(75, 23);
            this.Controls.Add(btnOK);

            btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(297, 85);
            btnCancel.Size = new System.Drawing.Size(75, 23);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            btnOK.Click += (s, e) =>
            {
                InputText = textBox.Text;
            };
        }
    }
}

