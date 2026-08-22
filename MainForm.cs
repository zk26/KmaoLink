using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KmaoLink
{
    public class MainForm : Form
    {
        private readonly BluetoothManager _bt;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly System.Windows.Forms.Timer _animTimer;
        private readonly NotifyIcon _trayIcon;

        // UI
        private Panel _headerPanel = null!;
        private Button _toggleBtn = null!;
        private Button _pinBtn = null!;
        private VerticalScrollPanel _listPanel = null!;
        private Label _statusLbl = null!;
        private Panel _recentPanel = null!;
        private Label _recentName = null!;
        private Button _quickBtn = null!;

        // 动画
        private float _fade = 0f;
        private int _targetH = 0;

        // 状态
        private bool _isOn;
        private bool _alwaysOnTop = true;
        private bool _dragging;
        private Point _dragStart;
        private List<BluetoothDevice> _devices = new();
        private string _lastDevice = "";
        private bool _busy = false;

        // 尺寸
        private const int W = 380;
        private const int HeaderH = 56;
        private const int ItemH = 72;
        private const int RecentH = 76;
        private const int MaxH = 520;
        private const int R = 12;

        public MainForm()
        {
            _bt = new BluetoothManager();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _refreshTimer.Tick += async (s, e) => await RefreshStatus();

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (s, e) => Animate();

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示窗口", null, (s, e) => ShowWin());
            menu.Items.Add("刷新设备", null, async (s, e) => await RefreshDevices());
            menu.Items.Add("-");
            menu.Items.Add("开机自启", null, (s, e) => ToggleAutoStart());
            menu.Items.Add("-");
            menu.Items.Add("退出", null, (s, e) => ExitApp());

            _trayIcon = new NotifyIcon
            {
                Icon = MakeIcon(false),
                ContextMenuStrip = menu,
                Text = "蓝牙管理",
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => ShowWin();

            BuildUI();
            LoadCfg();

            _ = RefreshStatus();
            _refreshTimer.Start();
            _animTimer.Start();
        }

        private void BuildUI()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = _alwaysOnTop;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(W, HeaderH);
            BackColor = Theme.BgMain;
            Icon = MakeIcon(false);
            Opacity = 0;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            Paint += PaintForm;

            // ===== 标题栏 =====
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderH,
                BackColor = Theme.BgHeader,
                Cursor = Cursors.SizeAll
            };
            _headerPanel.MouseDown += DragStart;
            _headerPanel.MouseMove += DragMove;
            _headerPanel.MouseUp += DragEnd;

            // 蓝牙图标（使用 logo.png，垂直居中）
            var icon = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(14, (HeaderH - 32) / 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = LoadLogo(32)
            };

            // 标题（高度 = 标题栏，垂直居中，不会被裁切）
            var title = new TransparentLabel
            {
                Text = "蓝牙设备",
                ForeColor = Theme.Text1,
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
                Location = new Point(50, 0),
                Size = new Size(126, HeaderH),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 置顶按钮使用自绘图形，避免 Emoji 字体基线造成视觉偏移。
            _pinBtn = new Button
            {
                Text = "",
                Size = new Size(32, 36),
                Location = new Point(294, (HeaderH - 36) / 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.Accent,
                Padding = Padding.Empty,
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            _pinBtn.FlatAppearance.BorderSize = 0;
            _pinBtn.FlatAppearance.MouseOverBackColor = Theme.BgCardHover;
            _pinBtn.FlatAppearance.MouseDownBackColor = Theme.Border;
            _pinBtn.Paint += PaintPinButton;
            _pinBtn.Click += (s, e) => TogglePin();
            UpdatePinButton();

            // 开关按钮
            _toggleBtn = MakeButton("开启", new Point(218, (HeaderH - 36) / 2), new Size(72, 36), Theme.Accent);
            _toggleBtn.Click += async (s, e) => await ToggleBT();

            // 关闭按钮（垂直居中）
            var closeBtn = new Button
            {
                Text = "✕",
                Size = new Size(36, 36),
                Location = new Point(W - 50, (HeaderH - 36) / 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI Symbol", 11),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Theme.Danger;
            closeBtn.FlatAppearance.MouseDownBackColor = Theme.DangerHover;
            closeBtn.Click += (s, e) => Hide();
            closeBtn.MouseEnter += (s, e) => { closeBtn.ForeColor = Theme.TextWhite; };
            closeBtn.MouseLeave += (s, e) => { closeBtn.ForeColor = Theme.Text3; };

            _headerPanel.Controls.AddRange(new Control[] { icon, title, _pinBtn, _toggleBtn, closeBtn });

            // ===== 最近设备 =====
            _recentPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = RecentH,
                BackColor = Theme.BgRecent,
                Visible = false
            };

            var recentIcon = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(16, (RecentH - 32) / 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = LoadLogo(32)
            };

            var recentTitle = new TransparentLabel
            {
                Text = "最近连接",
                ForeColor = Theme.Text2,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Location = new Point(56, 7),
                Size = new Size(150, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _recentName = new TransparentLabel
            {
                Text = "",
                ForeColor = Theme.Text1,
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Location = new Point(56, 28),
                Size = new Size(214, 34),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _quickBtn = MakeButton("连接", new Point(W - 100, (RecentH - 34) / 2), new Size(70, 34), Theme.Accent);
            _quickBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _quickBtn.Click += async (s, e) => await QuickConnect();

            _recentPanel.Controls.AddRange(new Control[] { recentIcon, recentTitle, _recentName, _quickBtn });

            // ===== 设备列表 =====
            _listPanel = new VerticalScrollPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BgMain
            };

            _statusLbl = new Label
            {
                Text = "正在搜索设备...",
                ForeColor = Theme.Text2,
                Font = new Font("Microsoft YaHei UI", 10.5f),
                Width = W - 44,
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _listPanel.AddContent(_statusLbl, 14, 10);

            Controls.Add(_listPanel);
            Controls.Add(_recentPanel);
            Controls.Add(_headerPanel);

            Load += (s, e) => { _targetH = HeaderH; _fade = 0; };
        }

        private Button MakeButton(string text, Point loc, Size size, Color bg)
        {
            var btn = new Button
            {
                Text = text,
                Size = size,
                Location = loc,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Theme.TextWhite,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Region = Theme.RoundedRegion(size, R);
            return btn;
        }

        #region 置顶切换

        private void TogglePin()
        {
            _alwaysOnTop = !_alwaysOnTop;
            TopMost = _alwaysOnTop;
            SettingsManager.Update(s => s.AlwaysOnTop = _alwaysOnTop);
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            _pinBtn.ForeColor = _alwaysOnTop ? Theme.Accent : Theme.Text3;
            _pinBtn.Invalidate();
        }

        private void PaintPinButton(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var color = _alwaysOnTop ? Theme.Accent : Theme.Text3;
            using var pen = new Pen(color, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            g.TranslateTransform(_pinBtn.ClientSize.Width / 2f, _pinBtn.ClientSize.Height / 2f);
            g.RotateTransform(-42f);
            g.DrawLine(pen, -5f, -5f, 5f, -5f);
            g.DrawLine(pen, -3f, -5f, -3f, 2f);
            g.DrawLine(pen, 3f, -5f, 3f, 2f);
            g.DrawLine(pen, -6f, 2f, 6f, 2f);
            g.DrawLine(pen, 0f, 2f, 0f, 9f);
            g.ResetTransform();
        }

        #endregion

        #region 拖动

        private void DragStart(object? s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
        }
        private void DragMove(object? s, MouseEventArgs e)
        {
            if (_dragging) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        }
        private void DragEnd(object? s, MouseEventArgs e)
        {
            if (_dragging) { _dragging = false; SettingsManager.SaveWindowPosition(Location); }
        }

        #endregion

        #region 动画

        private void Animate()
        {
            if (_fade < 1) { _fade = Math.Min(1, _fade + 0.1f); Opacity = _fade; }
            if (Height != _targetH)
            {
                int d = _targetH - Height;
                Height += Math.Abs(d) < 2 ? d : d / 3;
            }
        }

        #endregion

        #region 蓝牙操作

        private async Task ToggleBT()
        {
            if (_busy) return;
            _busy = true;
            _toggleBtn.Enabled = false;
            _toggleBtn.Text = "...";
            try
            {
                if (await _bt.SetBluetoothStateAsync(!_isOn))
                {
                    _isOn = !_isOn;
                    UpdateHeader();
                    if (_isOn) await RefreshDevices();
                    else { ClearList(); CalcH(0); }
                }
                else
                {
                    _toggleBtn.Text = "失败";
                    await Task.Delay(800);
                    _toggleBtn.Text = _isOn ? "关闭" : "开启";
                }
            }
            finally
            {
                _toggleBtn.Enabled = true;
                _busy = false;
            }
        }

        private async Task RefreshStatus()
        {
            if (_busy) return;
            try
            {
                bool was = _isOn;
                _isOn = await _bt.IsBluetoothEnabledAsync();
                if (was != _isOn)
                {
                    UpdateHeader();
                    if (_isOn) await RefreshDevices();
                    else { ClearList(); CalcH(0); }
                }
            }
            catch { }
        }

        private async Task RefreshDevices()
        {
            if (!_isOn) { SetStatus("蓝牙已关闭"); ClearList(); CalcH(0); return; }
            SetStatus("正在刷新...");

            _devices = await _bt.GetPairedDevicesAsync();

            if (_devices.Count == 0)
            {
                SetStatus("未找到已配对设备");
                ClearList();
                CalcH(0);
            }
            else
            {
                _statusLbl.Visible = false;
                BuildList();
                UpdateRecent();
                CalcH(_devices.Count);
            }
        }

        private void SetStatus(string text)
        {
            _statusLbl.Text = text;
            _statusLbl.Visible = true;
        }

        private async Task QuickConnect()
        {
            if (string.IsNullOrEmpty(_lastDevice) || _busy) return;
            _busy = true;
            _quickBtn.Enabled = false;
            _quickBtn.Text = "...";
            try
            {
                var dev = _devices.Find(d => d.Name == _lastDevice);
                if (dev != null)
                {
                    bool ok = dev.IsConnected
                        ? await _bt.DisconnectDeviceAsync(dev.Name)
                        : await _bt.ConnectDeviceAsync(dev.Name);
                    if (ok) await RefreshDevices();
                }
            }
            finally
            {
                _quickBtn.Enabled = true;
                _busy = false;
            }
        }

        #endregion

        #region 列表UI

        private void UpdateHeader()
        {
            _toggleBtn.Text = _isOn ? "关闭" : "开启";
            _toggleBtn.BackColor = _isOn ? Theme.Accent : Theme.Text3;
            _trayIcon.Icon = MakeIcon(_isOn);
            _trayIcon.Text = _isOn ? "蓝牙已开启" : "蓝牙已关闭";
        }

        private void UpdateRecent()
        {
            var connected = _devices.Find(d => d.IsConnected);
            if (connected != null)
            {
                _lastDevice = connected.Name;
                var (icon, _) = Theme.GetDeviceIcon(connected.Name);
                _recentName.Text = $"{icon}  {connected.Name}";
                _quickBtn.Text = "断开";
                _quickBtn.BackColor = Theme.Danger;
            }
            else if (!string.IsNullOrEmpty(_lastDevice))
            {
                var (icon, _) = Theme.GetDeviceIcon(_lastDevice);
                _recentName.Text = $"{icon}  {_lastDevice}";
                _quickBtn.Text = "连接";
                _quickBtn.BackColor = Theme.Accent;
            }
            _recentPanel.Visible = !string.IsNullOrEmpty(_lastDevice);
        }

        private void ClearList()
        {
            _listPanel.ClearContent(_statusLbl);
        }

        private void BuildList()
        {
            SuspendLayout();
            ClearList();

            for (int i = 0; i < _devices.Count; i++)
            {
                var card = MakeCard(_devices[i]);
                _listPanel.AddContent(card, 14, 10 + i * ItemH);
            }

            ResumeLayout();
        }

        private Panel MakeCard(BluetoothDevice dev)
        {
            var (icon, deviceType) = Theme.GetDeviceIcon(dev.Name);

            var card = new DoubleBufferedPanel
            {
                Size = new Size(W - 32, ItemH - 8),
                BackColor = Theme.BgCard,
                Tag = dev
            };

            // 图标（小字号 emoji，不会溢出）
            var iconLbl = new TransparentLabel
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 12),
                Location = new Point(12, 12),
                Size = new Size(36, 36),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 名称（高度充足 + 垂直居中）
            var nameLbl = new TransparentLabel
            {
                Text = dev.Name,
                ForeColor = Theme.Text1,
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Location = new Point(54, 8),
                Size = new Size(168, 26),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 类型 · 状态（高度充足 + 垂直居中）
            var statusColor = dev.IsConnected ? Theme.Success : Theme.Text2;
            var typeLbl = new TransparentLabel
            {
                Text = $"{deviceType} · {(dev.IsConnected ? "已连接" : "未连接")}",
                ForeColor = statusColor,
                Font = new Font("Microsoft YaHei UI", 9),
                Location = new Point(54, 34),
                Size = new Size(168, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 删除按钮 🗑
            var delBtn = new Button
            {
                Text = "🗑",
                Size = new Size(28, 28),
                Location = new Point(card.Width - 122, 18),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.Text3,
                Font = new Font("Segoe UI Emoji", 9),
                Cursor = Cursors.Hand,
                TabStop = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            delBtn.FlatAppearance.BorderSize = 0;
            delBtn.Click += async (s, e) => await DeleteDevice(dev);
            delBtn.MouseEnter += (s, e) => { delBtn.ForeColor = Theme.Danger; delBtn.BackColor = Theme.BgCardHover; };
            delBtn.MouseLeave += (s, e) => { delBtn.ForeColor = Theme.Text3; delBtn.BackColor = Color.Transparent; };

            // 连接/断开按钮
            var btn = MakeButton(
                dev.IsConnected ? "断开" : "连接",
                new Point(card.Width - 86, 15),
                new Size(70, 34),
                dev.IsConnected ? Theme.Danger : Theme.Accent);
            btn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btn.Click += async (s, e) => await DoAction(dev, btn);
            btn.MouseEnter += (s, e) => btn.BackColor = dev.IsConnected ? Theme.DangerHover : Theme.AccentHover;
            btn.MouseLeave += (s, e) => btn.BackColor = dev.IsConnected ? Theme.Danger : Theme.Accent;

            // 卡片绘制
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Theme.RoundRect(0, 0, card.Width - 1, card.Height - 1, R);
                using var br = new SolidBrush(card.BackColor);
                g.FillPath(br, path);
                using var pen = new Pen(Theme.BorderCard, 1);
                g.DrawPath(pen, path);
            };

            // 悬停效果
            card.MouseEnter += (s, e) => { card.BackColor = Theme.BgCardHover; card.Invalidate(); };
            card.MouseLeave += (s, e) => { card.BackColor = Theme.BgCard; card.Invalidate(); };

            card.Controls.AddRange(new Control[] { iconLbl, nameLbl, typeLbl, delBtn, btn });
            return card;
        }

        private async Task DeleteDevice(BluetoothDevice dev)
        {
            var result = MessageBox.Show(this,
                $"确定要删除设备「{dev.Name}」吗？\n删除后需要重新配对才能使用。",
                "删除设备",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            _busy = true;
            try
            {
                bool ok = await _bt.UnpairDeviceAsync(dev.Address);
                if (ok)
                {
                    await RefreshDevices();
                }
                else
                {
                    MessageBox.Show(this, "删除失败，请重试。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task DoAction(BluetoothDevice dev, Button btn)
        {
            if (_busy) return;
            _busy = true;
            btn.Enabled = false;
            btn.Text = "...";
            try
            {
                bool ok = dev.IsConnected
                    ? await _bt.DisconnectDeviceAsync(dev.Name)
                    : await _bt.ConnectDeviceAsync(dev.Name);
                if (ok)
                {
                    _lastDevice = dev.Name;
                    SettingsManager.Update(s => s.LastDevice = dev.Name);
                    await RefreshDevices();
                }
                else
                {
                    btn.Text = "失败";
                    await Task.Delay(800);
                    await RefreshDevices();
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private void CalcH(int count)
        {
            int recent = _recentPanel.Visible ? RecentH : 0;
            int content = count > 0 ? count * ItemH + 20 : 60;
            _targetH = HeaderH + recent + Math.Min(content, MaxH - HeaderH - recent);
        }

        #endregion

        #region 绘制

        private void PaintForm(object? s, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var bg = Theme.RoundRect(0, 0, Width - 1, Height - 1, R);
            using var bgBrush = new SolidBrush(Theme.BgMain);
            g.FillPath(bgBrush, bg);

            // 标题栏（只圆顶部两角）
            using var topPath = new GraphicsPath();
            topPath.AddArc(0, 0, R * 2, R * 2, 180, 90);
            topPath.AddArc(Width - 1 - R * 2, 0, R * 2, R * 2, 270, 90);
            topPath.AddLine(Width - 1, R, Width - 1, HeaderH);
            topPath.AddLine(0, HeaderH, 0, R);
            topPath.CloseFigure();
            using var hdrBrush = new SolidBrush(Theme.BgHeader);
            g.FillPath(hdrBrush, topPath);

            // 分隔线
            using var linePen = new Pen(Theme.Border, 1);
            g.DrawLine(linePen, 0, HeaderH, Width, HeaderH);

            // 外边框
            using var borderPen = new Pen(Theme.Border, 1);
            g.DrawPath(borderPen, bg);
        }

        #endregion

        #region 工具

        private static Image LoadLogo(int size)
        {
            var bmp = new Bitmap(size, size);
            // 使用矢量绘制，避免外部图片缺失导致标题栏图标为空。

            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var pen = new Pen(Theme.Accent, 2.5f);
                int c = size / 2;
                g.DrawLine(pen, c, 4, c, size - 4);
                g.DrawLine(pen, c - 5, c - 3, c + 5, c + 3);
                g.DrawLine(pen, c - 5, c + 1, c + 5, c - 5);
                g.DrawLine(pen, c, 4, c + 5, c - 5);
                g.DrawLine(pen, c, size - 4, c + 5, c + 3);
            }
            return bmp;
        }

        private static Bitmap? _logoBmp;

        private static Bitmap LogoBmp => _logoBmp ??= LoadLogo();

        private static Bitmap LoadLogo()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream("KmaoLink.logo.png");
            return s != null ? new Bitmap(s) : new Bitmap(32, 32);
        }

        private Icon MakeIcon(bool on)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                if (on)
                {
                    g.DrawImage(LogoBmp, 0, 0, 32, 32);
                }
                else
                {
                    // 蓝牙关闭：灰色半透明显示
                    using var ia = new ImageAttributes();
                    ia.SetColorMatrix(new ColorMatrix(new[]
                    {
                        new float[] { .3f,  .3f,  .3f,  0, 0 },
                        new float[] { .59f, .59f, .59f, 0, 0 },
                        new float[] { .11f, .11f, .11f, 0, 0 },
                        new float[] { 0,    0,    0,    .45f, 0 },
                        new float[] { 0,    0,    0,    0, 1 }
                    }));
                    g.DrawImage(LogoBmp, new Rectangle(0, 0, 32, 32), 0, 0, 32, 32, GraphicsUnit.Pixel, ia);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        #endregion

        #region 窗口管理

        private void ShowWin() { Show(); WindowState = FormWindowState.Normal; BringToFront(); _fade = 0; }

        private void ToggleAutoStart()
        {
            if (AutoStartManager.ToggleAutoStart())
            {
                bool on = AutoStartManager.IsAutoStartEnabled();
                SettingsManager.Update(s => s.AutoStart = on);
                _trayIcon.ShowBalloonTip(1500, "蓝牙管理", on ? "已设置开机自启" : "已取消开机自启", ToolTipIcon.Info);
            }
        }

        private void ExitApp()
        {
            _refreshTimer.Stop(); _animTimer.Stop();
            _trayIcon.Visible = false; _trayIcon.Dispose();
            Application.Exit();
        }

        private void LoadCfg()
        {
            var cfg = SettingsManager.Load();
            _lastDevice = cfg.LastDevice ?? "";
            _alwaysOnTop = cfg.AlwaysOnTop;
            TopMost = _alwaysOnTop;

            var pos = SettingsManager.GetSavedPosition();
            if (pos.HasValue) Location = pos.Value;
            else
            {
                var s = Screen.PrimaryScreen;
                if (s != null) Location = new Point(s.WorkingArea.Width - W - 20, s.WorkingArea.Height - HeaderH - 20);
            }
            if (cfg.AutoStart) AutoStartManager.SetAutoStart(true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            else base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refreshTimer?.Dispose(); _animTimer?.Dispose(); _trayIcon?.Dispose(); }
            base.Dispose(disposing);
        }

        #endregion
    }

    // ===== 鼠标穿透标签 =====
    public class TransparentLabel : Label
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTTRANSPARENT = -1;

        public TransparentLabel()
        {
            BackColor = Color.Transparent;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }
    }

    // ===== 垂直滚动面板 =====
    public class VerticalScrollPanel : Panel
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int ScrollBarW = 6;
        private const int ScrollBarMargin = 4;

        private readonly Dictionary<Control, int> _baseTop = new();
        private int _scrollY = 0;
        private int _scrollTarget = 0;
        private int _contentH = 0;
        private int MaxScroll => Math.Max(0, _contentH - Height);

        private bool _thumbDrag = false;
        private int _thumbDragStartY = 0;
        private int _thumbDragStartScroll = 0;
        private bool _thumbHover = false;

        private readonly System.Windows.Forms.Timer _smoothTimer;

        private static readonly Color ThumbColor = Color.FromArgb(205, 205, 205);
        private static readonly Color ThumbHoverColor = Color.FromArgb(166, 166, 166);

        public VerticalScrollPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            AutoScroll = false;

            _smoothTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _smoothTimer.Tick += (s, e) =>
            {
                if (_scrollY == _scrollTarget) { _smoothTimer.Stop(); return; }
                int d = _scrollTarget - _scrollY;
                int step = Math.Abs(d) < 2 ? d : d / 4;
                if (step == 0) step = d > 0 ? 1 : -1;
                SetScroll(_scrollY + step);
            };
        }

        public void AddContent(Control c, int x, int y)
        {
            c.Location = new Point(x, y);
            _baseTop[c] = y;
            Controls.Add(c);
            RecalcContent();
        }

        public void ClearContent(Control? keep = null)
        {
            var rm = new List<Control>();
            foreach (Control c in Controls)
                if (c != keep) rm.Add(c);
            foreach (var c in rm)
            {
                Controls.Remove(c);
                _baseTop.Remove(c);
                c.Dispose();
            }
            _scrollY = 0;
            _scrollTarget = 0;
            RecalcContent();
        }

        private void RecalcContent()
        {
            _contentH = 0;
            foreach (var kv in _baseTop)
                _contentH = Math.Max(_contentH, kv.Value + kv.Key.Height + 10);

            _scrollY = Math.Min(_scrollY, MaxScroll);
            _scrollTarget = Math.Min(_scrollTarget, MaxScroll);
            ApplyScroll();
            Invalidate();
        }

        private void ApplyScroll()
        {
            foreach (var kv in _baseTop)
            {
                int newTop = kv.Value - _scrollY;
                if (kv.Key.Top != newTop)
                    kv.Key.Top = newTop;
            }
        }

        private void SetScroll(int value)
        {
            int v = Math.Max(0, Math.Min(MaxScroll, value));
            if (v == _scrollY) return;
            _scrollY = v;
            ApplyScroll();
            Invalidate();
        }

        private void ScrollTo(int target)
        {
            _scrollTarget = Math.Max(0, Math.Min(MaxScroll, target));
            if (!_smoothTimer.Enabled) _smoothTimer.Start();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                ScrollTo(_scrollTarget - delta / 2);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalcContent();
        }

        private Rectangle GetThumbRect()
        {
            if (MaxScroll <= 0 || _contentH <= 0) return Rectangle.Empty;
            int trackH = Height - ScrollBarMargin * 2;
            int thumbH = Math.Max(28, (int)((float)Height / _contentH * trackH));
            int thumbY = ScrollBarMargin + (int)((float)_scrollY / MaxScroll * (trackH - thumbH));
            return new Rectangle(Width - ScrollBarW - ScrollBarMargin, thumbY, ScrollBarW, thumbH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (MaxScroll <= 0) return;
            if (GetThumbRect().Contains(e.Location))
            {
                _thumbDrag = true;
                _thumbDragStartY = e.Y;
                _thumbDragStartScroll = _scrollY;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_thumbDrag)
            {
                int trackH = Height - ScrollBarMargin * 2;
                int thumbH = GetThumbRect().Height;
                int movable = trackH - thumbH;
                if (movable > 0)
                {
                    float ratio = (float)(e.Y - _thumbDragStartY) / movable;
                    int newScroll = _thumbDragStartScroll + (int)(ratio * MaxScroll);
                    _smoothTimer.Stop();
                    _scrollTarget = Math.Max(0, Math.Min(MaxScroll, newScroll));
                    SetScroll(_scrollTarget);
                }
            }
            else
            {
                bool hover = GetThumbRect().Contains(e.Location);
                if (hover != _thumbHover) { _thumbHover = hover; Invalidate(); }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _thumbDrag = false;
            Capture = false;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_thumbDrag && _thumbHover) { _thumbHover = false; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            if (MaxScroll > 0)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var thumb = GetThumbRect();
                var color = (_thumbHover || _thumbDrag) ? ThumbHoverColor : ThumbColor;
                using var brush = new SolidBrush(color);
                using var path = Theme.RoundRect(thumb.X, thumb.Y, thumb.Width, thumb.Height, ScrollBarW / 2);
                g.FillPath(brush, path);
            }
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}
