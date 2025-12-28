namespace StockDataMQClient
{
    partial class Form1
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.menuConnection = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemStartReceive = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemStopReceive = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuItemExit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuData = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemLoadBasicData = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.menuItemCreateBoard = new System.Windows.Forms.ToolStripMenuItem();
            this.menuView = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemShowDataPanel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemShowLogPanel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTheme = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemDarkTheme = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemLightTheme = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFilter = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemGlobalFilter = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.splitContainerRight = new System.Windows.Forms.SplitContainer();
            this.panelData = new System.Windows.Forms.Panel();
            this.panelF10 = new System.Windows.Forms.Panel();
            this.lblF10Title = new System.Windows.Forms.Label();
            this.txtF10Content = new System.Windows.Forms.RichTextBox();
            this.panelLog = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.btnStartReceive = new System.Windows.Forms.Button();
            this.btnLoadBasicData = new System.Windows.Forms.Button();
            this.btnLoadMinuteData = new System.Windows.Forms.Button();
            this.btnLoadF10Data = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.RichTextBox();
            this.btnClearLog = new System.Windows.Forms.Button();
            this.lblMQStatus = new System.Windows.Forms.Label();
            this.lblLogTitle = new System.Windows.Forms.Label();
            this.dgvStockData = new System.Windows.Forms.DataGridView();
            this.lblStockDataTitle = new System.Windows.Forms.Label();
            this.txtSearchStock = new System.Windows.Forms.TextBox();
            this.btnSearchStock = new System.Windows.Forms.Button();
            this.lblSearchStock = new System.Windows.Forms.Label();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStockData)).BeginInit();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuConnection});
            // 只保留"连接"菜单，移除其他菜单项
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1332, 24);
            this.menuStrip1.TabIndex = 24;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // menuConnection
            // 
            this.menuConnection.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemStartReceive,
            this.menuItemStopReceive,
            this.toolStripSeparator1,
            this.menuItemExit});
            this.menuConnection.ForeColor = System.Drawing.Color.White;
            this.menuConnection.Name = "menuConnection";
            this.menuConnection.Size = new System.Drawing.Size(44, 20);
            this.menuConnection.Text = "连接";
            // 
            // menuItemStartReceive
            // 
            this.menuItemStartReceive.Name = "menuItemStartReceive";
            this.menuItemStartReceive.Size = new System.Drawing.Size(124, 22);
            this.menuItemStartReceive.Text = "启动接收";
            this.menuItemStartReceive.Click += new System.EventHandler(this.button1_Click);
            // 
            // menuItemStopReceive
            // 
            this.menuItemStopReceive.Name = "menuItemStopReceive";
            this.menuItemStopReceive.Size = new System.Drawing.Size(124, 22);
            this.menuItemStopReceive.Text = "停止接收";
            this.menuItemStopReceive.Click += new System.EventHandler(this.button2_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(121, 6);
            // 
            // menuItemExit
            // 
            this.menuItemExit.Name = "menuItemExit";
            this.menuItemExit.Size = new System.Drawing.Size(124, 22);
            this.menuItemExit.Text = "退出应用";
            this.menuItemExit.Click += new System.EventHandler(this.button3_Click);
            // 
            // menuData
            // 
            this.menuData.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemLoadBasicData,
            this.toolStripSeparator4,
            this.menuItemCreateBoard});
            this.menuData.ForeColor = System.Drawing.Color.White;
            this.menuData.Name = "menuData";
            this.menuData.Size = new System.Drawing.Size(44, 20);
            this.menuData.Text = "数据";
            // 
            // menuItemLoadBasicData
            // 
            this.menuItemLoadBasicData.Name = "menuItemLoadBasicData";
            this.menuItemLoadBasicData.Size = new System.Drawing.Size(152, 22);
            this.menuItemLoadBasicData.Text = "加载基础数据";
            this.menuItemLoadBasicData.Click += new System.EventHandler(this.btnLoadBasicData_Click);
            // 
            //
            // menuView
            //
            this.menuView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemShowDataPanel,
            this.menuItemShowLogPanel});
            this.menuView.ForeColor = System.Drawing.Color.White;
            this.menuView.Name = "menuView";
            this.menuView.Size = new System.Drawing.Size(44, 20);
            this.menuView.Text = "视图";
            // 
            // menuItemShowDataPanel
            // 
            this.menuItemShowDataPanel.Checked = true;
            this.menuItemShowDataPanel.CheckOnClick = true;
            this.menuItemShowDataPanel.CheckState = System.Windows.Forms.CheckState.Checked;
            this.menuItemShowDataPanel.Name = "menuItemShowDataPanel";
            this.menuItemShowDataPanel.Size = new System.Drawing.Size(152, 22);
            this.menuItemShowDataPanel.Text = "显示数据面板";
            this.menuItemShowDataPanel.Click += new System.EventHandler(this.menuItemShowDataPanel_Click);
            // 
            // menuItemShowLogPanel
            //
            this.menuItemShowLogPanel.Checked = true;
            this.menuItemShowLogPanel.CheckOnClick = true;
            this.menuItemShowLogPanel.CheckState = System.Windows.Forms.CheckState.Checked;
            this.menuItemShowLogPanel.Name = "menuItemShowLogPanel";
            this.menuItemShowLogPanel.Size = new System.Drawing.Size(152, 22);
            this.menuItemShowLogPanel.Text = "显示日志面板";
            this.menuItemShowLogPanel.Click += new System.EventHandler(this.menuItemShowLogPanel_Click);
            //
            // menuTheme
            //
            this.menuTheme.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemDarkTheme,
            this.menuItemLightTheme});
            this.menuTheme.ForeColor = System.Drawing.Color.White;
            this.menuTheme.Name = "menuTheme";
            this.menuTheme.Size = new System.Drawing.Size(44, 20);
            this.menuTheme.Text = "主题";
            //
            // menuItemDarkTheme
            //
            this.menuItemDarkTheme.Checked = true;
            this.menuItemDarkTheme.CheckOnClick = true;
            this.menuItemDarkTheme.CheckState = System.Windows.Forms.CheckState.Checked;
            this.menuItemDarkTheme.Name = "menuItemDarkTheme";
            this.menuItemDarkTheme.Size = new System.Drawing.Size(152, 22);
            this.menuItemDarkTheme.Text = "深色主题";
            this.menuItemDarkTheme.Click += new System.EventHandler(this.menuItemDarkTheme_Click);
            //
            // menuItemLightTheme
            //
            this.menuItemLightTheme.CheckOnClick = true;
            this.menuItemLightTheme.Name = "menuItemLightTheme";
            this.menuItemLightTheme.Size = new System.Drawing.Size(152, 22);
            this.menuItemLightTheme.Text = "浅色主题";
            this.menuItemLightTheme.Click += new System.EventHandler(this.menuItemLightTheme_Click);
            //
            // menuFilter
            //
            this.menuFilter.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemGlobalFilter});
            this.menuFilter.ForeColor = System.Drawing.Color.White;
            this.menuFilter.Name = "menuFilter";
            this.menuFilter.Size = new System.Drawing.Size(44, 20);
            this.menuFilter.Text = "筛选";
            //
            // menuItemGlobalFilter
            //
            this.menuItemGlobalFilter.Name = "menuItemGlobalFilter";
            this.menuItemGlobalFilter.Size = new System.Drawing.Size(152, 22);
            this.menuItemGlobalFilter.Text = "全局筛选设置";
            this.menuItemGlobalFilter.Click += new System.EventHandler(this.menuItemGlobalFilter_Click);
            //
            // splitContainerMain (主分割容器：左侧数据面板，右侧F10+日志)
            // 
            this.splitContainerMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainerMain.Location = new System.Drawing.Point(0, 25);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainerMain.Size = new System.Drawing.Size(1332, 792);
            this.splitContainerMain.SplitterDistance = 400;
            this.splitContainerMain.SplitterWidth = 8;
            this.splitContainerMain.TabIndex = 25;
            // 
            // splitContainerRight (右侧分割容器：F10面板和日志面板)
            // 
            this.splitContainerRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerRight.Location = new System.Drawing.Point(0, 0);
            this.splitContainerRight.Name = "splitContainerRight";
            this.splitContainerRight.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainerRight.Size = new System.Drawing.Size(1332, 430);
            this.splitContainerRight.SplitterDistance = 200;
            this.splitContainerRight.SplitterWidth = 8;
            this.splitContainerRight.TabIndex = 0;
            // 
            // panelData
            // 
            this.panelData.Controls.Add(this.lblStockDataTitle);
            this.panelData.Controls.Add(this.dgvStockData);
            this.panelData.Controls.Add(this.lblSearchStock);
            this.panelData.Controls.Add(this.txtSearchStock);
            this.panelData.Controls.Add(this.btnSearchStock);
            this.panelData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelData.Location = new System.Drawing.Point(0, 0);
            this.panelData.Name = "panelData";
            this.panelData.Size = new System.Drawing.Size(1332, 400);
            this.panelData.TabIndex = 0;
            // 
            // panelF10
            // 
            this.panelF10.Controls.Add(this.lblF10Title);
            this.panelF10.Controls.Add(this.txtF10Content);
            this.panelF10.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelF10.Location = new System.Drawing.Point(0, 0);
            this.panelF10.Name = "panelF10";
            this.panelF10.Size = new System.Drawing.Size(1332, 200);
            this.panelF10.TabIndex = 0;
            // 
            // lblF10Title
            // 
            this.lblF10Title.AutoSize = true;
            this.lblF10Title.ForeColor = System.Drawing.Color.White;
            this.lblF10Title.Location = new System.Drawing.Point(0, 4);
            this.lblF10Title.Name = "lblF10Title";
            this.lblF10Title.Size = new System.Drawing.Size(65, 12);
            this.lblF10Title.TabIndex = 0;
            this.lblF10Title.Text = "F10个股资料";
            // 
            // txtF10Content
            // 
            this.txtF10Content.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.txtF10Content.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtF10Content.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtF10Content.ForeColor = System.Drawing.Color.White;
            this.txtF10Content.Location = new System.Drawing.Point(0, 20);
            this.txtF10Content.Name = "txtF10Content";
            this.txtF10Content.ReadOnly = true;
            this.txtF10Content.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtF10Content.Size = new System.Drawing.Size(1332, 180);
            this.txtF10Content.TabIndex = 1;
            this.txtF10Content.Text = "";
            this.txtF10Content.WordWrap = true;
            // 
            // panelLog
            // 
            this.panelLog.Controls.Add(this.lblMQStatus);
            this.panelLog.Controls.Add(this.lblLogTitle);
            this.panelLog.Controls.Add(this.btnClearLog);
            this.panelLog.Controls.Add(this.txtLog);
            this.panelLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLog.Location = new System.Drawing.Point(0, 24);
            this.panelLog.Name = "panelLog";
            this.panelLog.Size = new System.Drawing.Size(1332, 838);
            this.panelLog.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(36, 3);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "启动接收";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2 (保留用于兼容，但隐藏)
            // 
            this.button2.Location = new System.Drawing.Point(-100, -100);
            this.button2.Visible = false;
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 1;
            this.button2.Text = "停止接收";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button3 (保留用于兼容，但隐藏)
            // 
            this.button3.Location = new System.Drawing.Point(-100, -100);
            this.button3.Visible = false;
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 2;
            this.button3.Text = "退出应用";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // btnStartReceive (保留用于兼容，但隐藏)
            // 
            this.btnStartReceive.Location = new System.Drawing.Point(-100, -100);
            this.btnStartReceive.Visible = false;
            this.btnStartReceive.Name = "btnStartReceive";
            this.btnStartReceive.Size = new System.Drawing.Size(100, 28);
            this.btnStartReceive.TabIndex = 18;
            this.btnStartReceive.Text = "开启接收数据";
            this.btnStartReceive.UseVisualStyleBackColor = true;
            this.btnStartReceive.Click += new System.EventHandler(this.btnStartReceive_Click);
            // 
            // btnLoadBasicData (保留用于兼容，但隐藏)
            // 
            this.btnLoadBasicData = new System.Windows.Forms.Button();
            this.btnLoadBasicData.Location = new System.Drawing.Point(-100, -100);
            this.btnLoadBasicData.Visible = false;
            this.btnLoadBasicData.Name = "btnLoadBasicData";
            this.btnLoadBasicData.Size = new System.Drawing.Size(100, 28);
            this.btnLoadBasicData.TabIndex = 16;
            this.btnLoadBasicData.Text = "加载基础数据";
            this.btnLoadBasicData.UseVisualStyleBackColor = true;
            this.btnLoadBasicData.Click += new System.EventHandler(this.btnLoadBasicData_Click);
            // 
            // btnLoadMinuteData (保留用于兼容，但隐藏)
            // 
            this.btnLoadMinuteData = new System.Windows.Forms.Button();
            this.btnLoadMinuteData.Location = new System.Drawing.Point(-100, -100);
            this.btnLoadMinuteData.Visible = false;
            this.btnLoadMinuteData.Name = "btnLoadMinuteData";
            this.btnLoadMinuteData.Size = new System.Drawing.Size(100, 28);
            this.btnLoadMinuteData.TabIndex = 17;
            this.btnLoadMinuteData.Text = "加载分钟数据";
            this.btnLoadMinuteData.UseVisualStyleBackColor = true;
            this.btnLoadMinuteData.Click += new System.EventHandler(this.btnLoadMinuteData_Click);
            // 
            // btnLoadF10Data (保留用于兼容，但隐藏)
            // 
            this.btnLoadF10Data = new System.Windows.Forms.Button();
            this.btnLoadF10Data.Location = new System.Drawing.Point(-100, -100);
            this.btnLoadF10Data.Visible = false;
            this.btnLoadF10Data.Name = "btnLoadF10Data";
            this.btnLoadF10Data.Size = new System.Drawing.Size(100, 28);
            this.btnLoadF10Data.TabIndex = 19;
            this.btnLoadF10Data.Text = "查看F10数据";
            this.btnLoadF10Data.UseVisualStyleBackColor = true;
            // F10功能已禁用
            // this.btnLoadF10Data.Click += new System.EventHandler(this.btnLoadF10Data_Click);
            // 
            // 
            // txtLog (现在在panelLog中)
            // 
            this.txtLog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLog.ForeColor = System.Drawing.Color.White;
            this.txtLog.Location = new System.Drawing.Point(0, 45);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new System.Drawing.Size(1332, 410);
            this.txtLog.TabIndex = 13;
            this.txtLog.Text = "";
            // 
            // btnClearLog (现在在panelLog中)
            // 
            this.btnClearLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearLog.Location = new System.Drawing.Point(1232, 0);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(100, 20);
            this.btnClearLog.TabIndex = 14;
            this.btnClearLog.Text = "清空日志";
            this.btnClearLog.UseVisualStyleBackColor = true;
            this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);
            // 
            // lblMQStatus (MQ状态显示)
            // 
            this.lblMQStatus.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.lblMQStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblMQStatus.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblMQStatus.ForeColor = System.Drawing.Color.White;
            this.lblMQStatus.Location = new System.Drawing.Point(0, 0);
            this.lblMQStatus.Name = "lblMQStatus";
            this.lblMQStatus.Padding = new System.Windows.Forms.Padding(5, 3, 5, 3);
            this.lblMQStatus.Size = new System.Drawing.Size(1332, 25);
            this.lblMQStatus.TabIndex = 16;
            this.lblMQStatus.Text = "MQ同步 | 日线: 0条 | 实时: 0条 | 除权: 0条";
            this.lblMQStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLogTitle (现在在panelLog中)
            // 
            this.lblLogTitle.AutoSize = true;
            this.lblLogTitle.ForeColor = System.Drawing.Color.White;
            this.lblLogTitle.Location = new System.Drawing.Point(0, 29);
            this.lblLogTitle.Name = "lblLogTitle";
            this.lblLogTitle.Size = new System.Drawing.Size(53, 12);
            this.lblLogTitle.TabIndex = 15;
            this.lblLogTitle.Text = "系统日志";
            // 
            // dgvStockData (现在在panelData中)
            // 
            this.dgvStockData.AllowUserToAddRows = false;
            this.dgvStockData.AllowUserToDeleteRows = false;
            this.dgvStockData.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvStockData.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvStockData.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.dgvStockData.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvStockData.Location = new System.Drawing.Point(0, 20);
            this.dgvStockData.Name = "dgvStockData";
            this.dgvStockData.ReadOnly = true;
            this.dgvStockData.RowHeadersVisible = false;
            this.dgvStockData.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvStockData.Size = new System.Drawing.Size(1332, 350);
            this.dgvStockData.TabIndex = 19;
            this.dgvStockData.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.dgvStockData.DefaultCellStyle.ForeColor = System.Drawing.Color.White;
            this.dgvStockData.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.dgvStockData.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            // 
            // lblStockDataTitle (现在在panelData中)
            // 
            this.lblStockDataTitle.AutoSize = true;
            this.lblStockDataTitle.ForeColor = System.Drawing.Color.White;
            this.lblStockDataTitle.Location = new System.Drawing.Point(0, 4);
            this.lblStockDataTitle.Name = "lblStockDataTitle";
            this.lblStockDataTitle.Size = new System.Drawing.Size(89, 12);
            this.lblStockDataTitle.TabIndex = 20;
            this.lblStockDataTitle.Text = "基础数据（码表）";
            // 
            // txtSearchStock (现在在panelData中)
            // 
            this.txtSearchStock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txtSearchStock.Location = new System.Drawing.Point(90, 375);
            this.txtSearchStock.Name = "txtSearchStock";
            this.txtSearchStock.Size = new System.Drawing.Size(150, 21);
            this.txtSearchStock.TabIndex = 21;
            this.txtSearchStock.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSearchStock_KeyDown);
            // 
            // btnSearchStock (现在在panelData中)
            // 
            this.btnSearchStock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSearchStock.Location = new System.Drawing.Point(250, 373);
            this.btnSearchStock.Name = "btnSearchStock";
            this.btnSearchStock.Size = new System.Drawing.Size(75, 25);
            this.btnSearchStock.TabIndex = 22;
            this.btnSearchStock.Text = "查询股票";
            this.btnSearchStock.UseVisualStyleBackColor = true;
            this.btnSearchStock.Click += new System.EventHandler(this.btnSearchStock_Click);
            // 
            // lblSearchStock (现在在panelData中)
            // 
            this.lblSearchStock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblSearchStock.AutoSize = true;
            this.lblSearchStock.ForeColor = System.Drawing.Color.White;
            this.lblSearchStock.Location = new System.Drawing.Point(0, 378);
            this.lblSearchStock.Name = "lblSearchStock";
            this.lblSearchStock.Size = new System.Drawing.Size(89, 12);
            this.lblSearchStock.TabIndex = 23;
            this.lblSearchStock.Text = "查询股票代码：";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1332, 862);
            // 只添加日志面板和菜单栏
            this.Controls.Add(this.panelLog);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "股票数据日志系统";
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStockData)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem menuConnection;
        private System.Windows.Forms.ToolStripMenuItem menuItemStartReceive;
        private System.Windows.Forms.ToolStripMenuItem menuItemStopReceive;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem menuItemExit;
        private System.Windows.Forms.ToolStripMenuItem menuData;
        private System.Windows.Forms.ToolStripMenuItem menuItemLoadBasicData;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem menuItemCreateBoard;
        private System.Windows.Forms.ToolStripMenuItem menuView;
        private System.Windows.Forms.ToolStripMenuItem menuItemShowDataPanel;
        private System.Windows.Forms.ToolStripMenuItem menuItemShowLogPanel;
        private System.Windows.Forms.ToolStripMenuItem menuTheme;
        private System.Windows.Forms.ToolStripMenuItem menuItemDarkTheme;
        private System.Windows.Forms.ToolStripMenuItem menuItemLightTheme;
        private System.Windows.Forms.ToolStripMenuItem menuFilter;
        private System.Windows.Forms.ToolStripMenuItem menuItemGlobalFilter;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.SplitContainer splitContainerRight;
        private System.Windows.Forms.Panel panelData;
        private System.Windows.Forms.Panel panelF10;
        private System.Windows.Forms.Label lblF10Title;
        private System.Windows.Forms.RichTextBox txtF10Content;
        private System.Windows.Forms.Panel panelLog;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button btnStartReceive;
        private System.Windows.Forms.Button btnLoadBasicData;
        private System.Windows.Forms.Button btnLoadMinuteData;
        private System.Windows.Forms.Button btnLoadF10Data;
        private System.Windows.Forms.RichTextBox txtLog;
        private System.Windows.Forms.Button btnClearLog;
        private System.Windows.Forms.Label lblMQStatus;
        private System.Windows.Forms.Label lblLogTitle;
        private System.Windows.Forms.DataGridView dgvStockData;
        private System.Windows.Forms.Label lblStockDataTitle;
        private System.Windows.Forms.TextBox txtSearchStock;
        private System.Windows.Forms.Button btnSearchStock;
        private System.Windows.Forms.Label lblSearchStock;
    }
}

